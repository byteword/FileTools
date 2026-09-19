namespace FileTools;

internal sealed record EmptyFolderCleanupOptions(bool IncludeSubfolders = true, bool IncludeSelectedRoots = false);
internal sealed record EmptyFolderCandidate(string Path, string RootPath, bool AfterChildren);
internal sealed record EmptyFolderScanResult(
    IReadOnlyList<string> Roots,
    IReadOnlyList<EmptyFolderCandidate> Candidates,
    IReadOnlyList<string> Errors);

/// <summary>사용자가 확정한 경로만 보관한다. 성공한 후보를 제거해 부분 실패 재시도를 지원한다.</summary>
internal sealed class EmptyFolderCleanupPlan
{
    public string RootPath { get; init; } = "";
    public EmptyFolderCleanupOptions Options { get; init; } = new();
    public IReadOnlyList<string> ScanRoots { get; init; } = [];
    public List<string> CandidatePaths { get; set; } = [];
    public EmptyFolderCleanupPlan Clone() => new()
    {
        RootPath = RootPath, Options = Options, ScanRoots = ScanRoots.ToArray(), CandidatePaths = CandidatePaths.ToList()
    };
}

/// <summary>루트 경계와 연결 경로 검증을 실제 파일 작업 직전에도 사용한다.</summary>
internal static class FileOperationPathGuard
{
    public static StringComparer Comparer { get; } = StringComparer.OrdinalIgnoreCase;
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    public static bool IsWithin(string path, string root) => Comparer.Equals(path, root) ||
        path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static void EnsureNoLinkedDirectory(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
        {
            var attributes = File.GetAttributes(directory.FullName);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException(Localizer.Format("FileOperationLinkedPath", directory.FullName));
            if ((attributes & FileAttributes.Directory) == 0)
                throw new IOException(Localizer.Format("FileOperationNotDirectory", directory.FullName));
        }
    }
}

internal static class EmptyFolderCleanupOperations
{
    /// <summary>파일은 숨김 여부와 크기에 상관없이 내용으로 센다. 반복형 후위 순회로 깊은 트리도 처리한다.</summary>
    public static EmptyFolderScanResult Scan(IEnumerable<string> paths, EmptyFolderCleanupOptions options,
        CancellationToken cancellationToken = default)
    {
        var selectedRoots = paths.Select(FileOperationPathGuard.Normalize)
            .Distinct(FileOperationPathGuard.Comparer).OrderBy(static path => path.Length)
            .ThenBy(static path => path, FileOperationPathGuard.Comparer).ToArray();
        var roots = selectedRoots.Where(path => !selectedRoots.Any(other =>
            !FileOperationPathGuard.Comparer.Equals(path, other) && FileOperationPathGuard.IsWithin(path, other))).ToArray();
        var protectedRoots = selectedRoots.ToHashSet(FileOperationPathGuard.Comparer);
        var candidates = new List<EmptyFolderCandidate>();
        var errors = new List<string>();
        var removable = new HashSet<string>(FileOperationPathGuard.Comparer);

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { FileOperationPathGuard.EnsureNoLinkedDirectory(root); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { errors.Add(root + " | " + ex.Message); continue; }

            var pending = new Stack<(string Path, string[]? Entries)>();
            // 직접 하위만 탐색하더라도 사용자가 별도로 선택한 깊은 루트의 범위는 유지한다.
            foreach (var seed in options.IncludeSubfolders ? [root] : selectedRoots.Where(path => FileOperationPathGuard.IsWithin(path, root)))
            {
                try { FileOperationPathGuard.EnsureNoLinkedDirectory(seed); pending.Push((seed, null)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { errors.Add(seed + " | " + ex.Message); }
            }
            while (pending.TryPop(out var node))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (node.Entries is null)
                {
                    try
                    {
                        var attributes = File.GetAttributes(node.Path);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                        var entries = Directory.EnumerateFileSystemEntries(node.Path).Order(FileOperationPathGuard.Comparer).ToArray();
                        pending.Push((node.Path, entries));
                        if (options.IncludeSubfolders || protectedRoots.Contains(node.Path))
                        {
                            foreach (var entry in entries.Reverse())
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                try
                                {
                                    var entryAttributes = File.GetAttributes(entry);
                                    if ((entryAttributes & FileAttributes.Directory) != 0 &&
                                        (entryAttributes & FileAttributes.ReparsePoint) == 0)
                                        pending.Push((entry, null));
                                }
                                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                                { errors.Add(entry + " | " + ex.Message); }
                            }
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    { errors.Add(node.Path + " | " + ex.Message); }
                    continue;
                }

                if (FileOperationPathGuard.Comparer.Equals(node.Path, Path.GetPathRoot(node.Path)) ||
                    (!options.IncludeSelectedRoots && protectedRoots.Contains(node.Path)) ||
                    !node.Entries.All(removable.Contains)) continue;
                if (removable.Add(node.Path))
                    candidates.Add(new EmptyFolderCandidate(node.Path, root, node.Entries.Length > 0));
            }
        }
        return new EmptyFolderScanResult(selectedRoots, candidates, errors);
    }

    public static IReadOnlyList<EmptyFolderCleanupPlan> CreatePlans(EmptyFolderScanResult scan,
        EmptyFolderCleanupOptions options, IEnumerable<string> selectedPaths)
    {
        var selected = selectedPaths.ToHashSet(FileOperationPathGuard.Comparer);
        // 제외한 자식을 남기면 그 부모도 후보에서 제외한다. UI 밖의 호출에도 같은 정책을 적용한다.
        var blocked = new HashSet<string>(FileOperationPathGuard.Comparer);
        foreach (var child in scan.Candidates.Where(candidate => !selected.Contains(candidate.Path)))
        {
            for (var path = child.Path; path is not null && FileOperationPathGuard.IsWithin(path, child.RootPath); path = Path.GetDirectoryName(path))
                blocked.Add(path);
        }
        var eligible = scan.Candidates.Where(candidate => selected.Contains(candidate.Path) && !blocked.Contains(candidate.Path)).ToArray();
        return eligible.GroupBy(static candidate => candidate.RootPath, FileOperationPathGuard.Comparer)
            .Select(group => new EmptyFolderCleanupPlan
            {
                RootPath = group.Key, Options = options,
                ScanRoots = scan.Roots.Where(root => FileOperationPathGuard.IsWithin(root, group.Key)).ToArray(),
                CandidatePaths = group.Select(static candidate => candidate.Path).ToList()
            }).ToArray();
    }

    /// <summary>후보를 새로 탐색하지 않는다. 실패한 항목만 남기고 항상 휴지통 실행기를 사용한다.</summary>
    public static OperationResult Apply(EmptyFolderCleanupPlan plan, CancellationToken cancellationToken = default,
        Action<string>? recycle = null)
    {
        recycle ??= WindowsRecycleBin.RecycleEmptyDirectory;
        var result = new OperationResult();
        var root = FileOperationPathGuard.Normalize(plan.RootPath);
        foreach (var rawPath in plan.CandidatePaths.Distinct(FileOperationPathGuard.Comparer)
                     .OrderByDescending(static path => path.Length).ToArray())
        {
            if (cancellationToken.IsCancellationRequested) break;
            result.AddCandidate();
            try
            {
                var path = FileOperationPathGuard.Normalize(rawPath);
                if (!FileOperationPathGuard.IsWithin(path, root) ||
                    (!plan.Options.IncludeSelectedRoots && FileOperationPathGuard.Comparer.Equals(path, root)) ||
                    FileOperationPathGuard.Comparer.Equals(path, Path.GetPathRoot(path)))
                    throw new IOException(Localizer.Format("EmptyFolderOutsideScope", path));

                try { File.GetAttributes(path); }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    plan.CandidatePaths.RemoveAll(candidate => FileOperationPathGuard.Comparer.Equals(candidate, rawPath));
                    result.AddSkipped(Localizer.Format("EmptyFolderMissing", path));
                    continue;
                }

                FileOperationPathGuard.EnsureNoLinkedDirectory(path);
                if (Directory.EnumerateFileSystemEntries(path).Any())
                {
                    result.AddSkipped(Localizer.Format("EmptyFolderNotEmpty", path));
                    continue;
                }
                recycle(path);
                if (Directory.Exists(path)) throw new IOException(Localizer.Get("EmptyFolderRecycleIncomplete"));
                plan.CandidatePaths.RemoveAll(candidate => FileOperationPathGuard.Comparer.Equals(candidate, rawPath));
                result.AddApplied(Localizer.Format("EmptyFolderRecycled", path));
                FileToolsEnvironment.Log("RECYCLE-EMPTY", path);
            }
            catch (OperationCanceledException) { result.AddSkipped(Localizer.Get("EmptyFolderCancelled")); break; }
            catch (Exception ex) { result.AddError(rawPath + " | " + ex.Message); }
        }
        return result;
    }
}
