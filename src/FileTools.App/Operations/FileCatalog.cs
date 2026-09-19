namespace FileTools;

internal sealed record FileCatalogEntry(string FullPath, string RootPath, string RelativePath,
    string Name, string Extension, string Kind, long Size, DateTimeOffset CreatedUtc, DateTimeOffset ModifiedUtc);
internal sealed record FileCatalogError(string Path, string Message);
internal sealed record FileCatalogResult(IReadOnlyList<FileCatalogEntry> Entries, IReadOnlyList<FileCatalogError> Errors, int Skipped, int ScannedFiles);
internal sealed record FileCatalogProgress(int Files, int Errors, int Skipped);
internal sealed record FileCatalogOptions
{
    public bool Recursive { get; init; } = true;
    public bool IncludeHidden { get; init; }
    public bool IncludeSystem { get; init; }
    public IReadOnlyList<string> ExcludedPaths { get; init; } = [];
}
internal enum FileCatalogSort { Name, FullPath, Kind, Size, Created, Modified }

/// <summary>파일 목록과 조건 수집이 공유하는 읽기 전용 탐색. 링크를 따라가지 않는다.</summary>
internal static class FileCatalog
{
    public static FileCatalogResult Scan(IEnumerable<string> paths, FileToolsSettings settings, FileCatalogOptions options,
        CancellationToken cancellationToken = default, IProgress<FileCatalogProgress>? progress = null,
        Func<FileCatalogEntry, bool>? predicate = null)
    {
        var comparer = FileOperationPathGuard.Comparer;
        var excluded = options.ExcludedPaths.Select(path =>
        {
            if (!Path.IsPathFullyQualified(path)) throw new ArgumentException(Localizer.Get("CatalogAbsoluteExclude"));
            return FileOperationPathGuard.Normalize(path);
        }).Distinct(comparer).ToArray();
        var roots = new HashSet<string>(comparer);
        var files = new HashSet<string>(comparer);
        var directories = new HashSet<string>(comparer);
        var entries = new List<FileCatalogEntry>();
        var errors = new List<FileCatalogError>();
        var skipped = 0;
        var visited = 0;
        var scannedFiles = 0;
        var kinds = new Dictionary<string, string>(comparer);
        foreach (var rule in AutoRelocationFileTypeClassifier.NormalizeExtensionRules(settings.FileKindExtensionRules))
            foreach (var extension in rule.Extensions) kinds.TryAdd(extension, rule.Kind);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { roots.Add(FileOperationPathGuard.Normalize(path)); }
            catch (Exception ex) when (IsPathError(ex)) { errors.Add(new(path, ex.Message)); }
        }
        // 바깥 루트를 우선해 입력 순서와 무관하게 상대 경로를 고정한다.
        foreach (var root in roots.OrderBy(static path => path.Length).ThenBy(static path => path, comparer))
        {
            var pending = new Stack<(string Path, string? Root)>();
            pending.Push((root, null));
            while (pending.TryPop(out var node))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++visited % 128 == 0) progress?.Report(new(entries.Count, errors.Count, skipped));
                if (excluded.Any(path => FileOperationPathGuard.IsWithin(node.Path, path))) { skipped++; continue; }
                try
                {
                    var attributes = File.GetAttributes(node.Path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                        (!options.IncludeHidden && (attributes & FileAttributes.Hidden) != 0) ||
                        (!options.IncludeSystem && (attributes & FileAttributes.System) != 0)) { skipped++; continue; }
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (!directories.Add(node.Path)) continue;
                        FileOperationPathGuard.EnsureNoLinkedDirectory(node.Path);
                        var scope = node.Root ?? node.Path;
                        // 열거 중 오류가 나면 이 디렉터리 전체를 읽기 실패로 보고한다.
                        var children = Directory.EnumerateFileSystemEntries(node.Path).Order(comparer).ToArray();
                        foreach (var child in children.Reverse())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!options.Recursive)
                            {
                                try
                                {
                                    if ((File.GetAttributes(child) & FileAttributes.Directory) != 0) continue;
                                }
                                catch (Exception ex) when (IsPathError(ex)) { errors.Add(new(child, ex.Message)); continue; }
                            }
                            pending.Push((child, scope));
                        }
                    }
                    else
                    {
                        if (!files.Add(node.Path)) continue;
                        FileOperationPathGuard.EnsureNoLinkedDirectory(Path.GetDirectoryName(node.Path)!);
                        var info = new FileInfo(node.Path);
                        info.Refresh();
                        if (!info.Exists) throw new FileNotFoundException(Localizer.Get("CatalogMissingFile"), node.Path);
                        if ((info.Attributes & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                        var scope = node.Root ?? Path.GetDirectoryName(node.Path)!;
                        var extension = Path.GetExtension(node.Path).ToLowerInvariant();
                        var kind = kinds.GetValueOrDefault(extension, AutoRelocationFileTypeClassifier.OtherKind);
                        var entry = new FileCatalogEntry(node.Path, scope, Path.GetRelativePath(scope, node.Path), info.Name, extension,
                            kind, info.Length, new DateTimeOffset(info.CreationTimeUtc), new DateTimeOffset(info.LastWriteTimeUtc));
                        scannedFiles++;
                        if (predicate is null || predicate(entry)) entries.Add(entry);
                    }
                }
                catch (Exception ex) when (IsPathError(ex)) { errors.Add(new(node.Path, ex.Message)); }
            }
        }
        progress?.Report(new(entries.Count, errors.Count, skipped));
        return new(entries, errors, skipped, scannedFiles);
    }

    public static IReadOnlyList<FileCatalogEntry> Sort(IEnumerable<FileCatalogEntry> entries, FileCatalogSort sort, bool descending = false)
    {
        IOrderedEnumerable<FileCatalogEntry> ordered = sort switch
        {
            FileCatalogSort.Name => Order(static item => item.Name, NaturalFileNameComparer.Instance),
            FileCatalogSort.Kind => Order(static item => item.Kind, StringComparer.OrdinalIgnoreCase),
            FileCatalogSort.Size => Order(static item => item.Size, Comparer<long>.Default),
            FileCatalogSort.Created => Order(static item => item.CreatedUtc, Comparer<DateTimeOffset>.Default),
            FileCatalogSort.Modified => Order(static item => item.ModifiedUtc, Comparer<DateTimeOffset>.Default),
            _ => Order(static item => item.FullPath, FileOperationPathGuard.Comparer)
        };
        return ordered.ThenBy(static item => item.FullPath, FileOperationPathGuard.Comparer).ToArray();
        IOrderedEnumerable<FileCatalogEntry> Order<T>(Func<FileCatalogEntry, T> key, IComparer<T> comparer) =>
            descending ? entries.OrderByDescending(key, comparer) : entries.OrderBy(key, comparer);
    }

    private static bool IsPathError(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    internal static bool IsRegularFile(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) return false;
            FileOperationPathGuard.EnsureNoLinkedDirectory(Path.GetDirectoryName(FileOperationPathGuard.Normalize(path))!);
            return true;
        }
        catch (Exception ex) when (IsPathError(ex)) { return false; }
    }
}
