using System.Collections.ObjectModel;

namespace FileTools;

/// <summary>비교 시점의 파일 식별 정보와 보존 선택을 실행까지 유지한다.</summary>
internal sealed class DuplicateDeleteVerification
{
    public IReadOnlyDictionary<string, RenameFileSnapshot> Snapshots { get; }
    public IReadOnlyList<string> KeepPaths { get; }
    public IReadOnlyList<string> DeletePaths { get; }

    private DuplicateDeleteVerification(Dictionary<string, RenameFileSnapshot> snapshots, string[] keep, string[] delete)
    {
        Snapshots = new ReadOnlyDictionary<string, RenameFileSnapshot>(snapshots);
        KeepPaths = Array.AsReadOnly(keep);
        DeletePaths = Array.AsReadOnly(delete);
    }

    public static DuplicateDeleteVerification Create(IEnumerable<string> paths, IEnumerable<string> deletePaths,
        IReadOnlyDictionary<string, RenameFileSnapshot>? snapshots)
    {
        var all = paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var delete = deletePaths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var keep = all.Except(delete, StringComparer.OrdinalIgnoreCase).ToArray();
        if (keep.Length == 0 || delete.Except(all, StringComparer.OrdinalIgnoreCase).Any())
            throw new InvalidOperationException(Localizer.Get("DuplicateKeepRequired"));
        if (snapshots is null || all.Any(path => !snapshots.TryGetValue(path, out var state) || state.LinkCount != 1))
            throw new InvalidOperationException(Localizer.Get("DuplicateRecheckRequired"));
        return new(all.ToDictionary(path => path, path => snapshots[path], StringComparer.OrdinalIgnoreCase), keep, delete);
    }

    public void CheckSnapshot(string path)
    {
        if (!Snapshots.TryGetValue(path, out var expected) || expected != RenameFileSnapshot.Capture(path))
            throw new IOException(Localizer.Get("DuplicateRecheckRequired"));
    }

    /// <summary>보존본은 이동이 끝날 때까지 쓰기/삭제를 막고 후보는 Shell 이동을 허용한다.</summary>
    public void WithVerifiedFiles(string path, CancellationToken cancellationToken, Action<Action> recycle)
    {
        if (!DeletePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            throw new IOException(Localizer.Get("DuplicateRecheckRequired"));
        cancellationToken.ThrowIfCancellationRequested();
        var keep = KeepPaths[0];
        CheckSnapshot(keep);
        CheckSnapshot(path);
        using var keptFile = new FileStream(keep, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var candidate = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        void Verify()
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var kept in KeepPaths) CheckSnapshot(kept);
            CheckSnapshot(path);
            keptFile.Position = candidate.Position = 0;
            if (keptFile.Length != candidate.Length) throw new IOException(Localizer.Get("DuplicateRecheckRequired"));
            var left = new byte[128 * 1024];
            var right = new byte[left.Length];
            long remaining = keptFile.Length;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = (int)Math.Min(remaining, left.Length);
                keptFile.ReadExactly(left.AsSpan(0, count));
                candidate.ReadExactly(right.AsSpan(0, count));
                if (!left.AsSpan(0, count).SequenceEqual(right.AsSpan(0, count)))
                    throw new IOException(Localizer.Get("DuplicateRecheckRequired"));
                remaining -= count;
            }
            CheckSnapshot(keep);
            CheckSnapshot(path);
        }
        Verify();
        recycle(Verify);
    }

    public bool ConflictsWith(IEnumerable<WorkTargetPlan> targets, Func<WorkPlanStep, string, string?>? predictPath = null)
    {
        foreach (var target in targets)
        {
            var currentPath = target.Path;
            foreach (var step in target.Steps.Where(step => step.Kind != WorkPlanStepKind.DuplicateDelete))
            {
                var nextPath = predictPath?.Invoke(step, currentPath);
                var relatedPaths = (step.ArchiveMergeOptions?.SourcePaths ?? [])
                    .Concat(new[] { nextPath, step.ArchiveMergeOptions?.OutputPath, step.ManualTargetRootPath,
                        step.BatchRenameItem?.TargetPath }).Where(path => !string.IsNullOrWhiteSpace(path));
                if (relatedPaths.Any(related => Snapshots.Keys.Any(path =>
                    FileOperationPathGuard.IsWithin(path, related!) || FileOperationPathGuard.IsWithin(related!, path)))) return true;
                if (nextPath is not null) currentPath = nextPath;
            }
            var overlaps = Snapshots.Keys.Any(path => FileOperationPathGuard.IsWithin(path, target.Path) ||
                FileOperationPathGuard.IsWithin(target.Path, path));
            if (!overlaps) continue;
            if (target.Steps.Any(step => step.Kind != WorkPlanStepKind.DuplicateDelete ||
                !ReferenceEquals(step.DuplicateDeleteVerification, this) ||
                KeepPaths.Contains(target.Path, StringComparer.OrdinalIgnoreCase))) return true;
        }
        return false;
    }
}
