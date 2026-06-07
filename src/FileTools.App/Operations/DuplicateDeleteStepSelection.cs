namespace FileTools;

internal sealed record DuplicateDeleteStepCandidate(string Path, bool Delete);

internal static class DuplicateDeleteStepSelection
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static IReadOnlyList<DuplicateDeleteStepCandidate> CreateCandidates(
        IEnumerable<WorkTargetPlan> targets,
        IEnumerable<string>? scopePaths = null)
    {
        var scopeSet = CreatePathSet(scopePaths);
        return targets
            .Where(static target => File.Exists(target.Path))
            .Where(target => scopeSet is null || scopeSet.Contains(Path.GetFullPath(target.Path)))
            .Select(static target => new DuplicateDeleteStepCandidate(
                target.Path,
                target.Steps.Any(static step => step.Kind == WorkPlanStepKind.DuplicateDelete)))
            .ToArray();
    }

    public static int Apply(
        IEnumerable<WorkTargetPlan> targets,
        IEnumerable<string> deletePaths,
        IEnumerable<string>? scopePaths = null)
    {
        var deleteSet = CreatePathSet(deletePaths) ?? new HashSet<string>(PathComparer);
        var scopeSet = CreatePathSet(scopePaths);
        IReadOnlyList<string> groupPaths = scopeSet is null ? [] : scopeSet.ToArray();
        var changedTargets = 0;

        foreach (var target in targets
            .Where(static target => File.Exists(target.Path))
            .Where(target => scopeSet is null || scopeSet.Contains(Path.GetFullPath(target.Path))))
        {
            var shouldDelete = deleteSet.Contains(Path.GetFullPath(target.Path));
            var existingSteps = target.Steps
                .Where(static step => step.Kind == WorkPlanStepKind.DuplicateDelete)
                .ToArray();

            foreach (var duplicateStep in existingSteps)
            {
                target.Steps.Remove(duplicateStep);
            }

            if (shouldDelete)
            {
                target.Steps.Add(new WorkPlanStep
                {
                    Kind = WorkPlanStepKind.DuplicateDelete,
                    DuplicateDeleteGroupPaths = groupPaths
                });
            }

            if (existingSteps.Length > 0 || shouldDelete)
            {
                changedTargets++;
            }
        }

        return changedTargets;
    }

    private static HashSet<string>? CreatePathSet(IEnumerable<string>? paths)
    {
        return paths?
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToHashSet(PathComparer);
    }
}
