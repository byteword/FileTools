namespace FileTools;

internal sealed record DuplicateDeleteStepCandidate(string Path, bool Delete);

/// <summary>
/// 중복 파일 삭제 후보를 만들고, 선택한 대상에 대해 삭제 스텝을 반영하는 유틸리티.
/// </summary>
internal static class DuplicateDeleteStepSelection
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// 대상 워크플랜 목록을 대상으로 삭제 대상 플래그를 묶은 후보 리스트를 생성한다.
    /// </summary>
    /// <param name="targets">중복 분석 결과를 포함한 대상 플랜 목록</param>
    /// <param name="scopePaths">필터링할 범위 경로(선택 입력)</param>
    /// <returns>경로별 삭제 대상 여부 후보 목록</returns>
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

    /// <summary>
    /// 스코프/삭제 후보를 반영해 기존 step에서 삭제 step을 갱신하고 변경된 대상 수를 반환한다.
    /// </summary>
    /// <param name="targets">중복 분석 대상 플랜 컬렉션</param>
    /// <param name="deletePaths">삭제로 표시된 실제 경로 목록</param>
    /// <param name="scopePaths">범위 제한 경로</param>
    /// <returns>스텝이 변경된 대상 개수</returns>
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

    /// <summary>
    /// 비교/생성 편의를 위해 문자열 경로 집합을 정규화해 반환한다.
    /// </summary>
    /// <param name="paths">경로 입력 모음</param>
    /// <returns>정규화된 full path 집합, 입력이 없으면 null</returns>
    private static HashSet<string>? CreatePathSet(IEnumerable<string>? paths)
    {
        return paths?
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToHashSet(PathComparer);
    }
}
