namespace FileTools;

internal enum WorkPlanDisplayFilter
{
    All,
    SelectedTargets,
    Warnings
}

internal enum WorkPlanDisplayRowKind
{
    Step,
    OperationGroup,
    Input
}

/// <summary>
/// 하단 작업목록 UI가 사용할 표시 전용 행 모델.
/// </summary>
internal sealed record WorkPlanDisplayRow(
    int Order,
    WorkPlanDisplayRowKind Kind,
    string OperationKey,
    WorkTargetPlan? Target,
    WorkPlanStep? Step,
    WorkPlanStepPreview? Preview,
    string ActionText,
    string InputText,
    string OutputText,
    bool HasWarning,
    bool MatchesFilter);

internal sealed class WorkPlanDisplayBuilder
{
    /// <summary>
    /// 선택 대상 필터와 공유 작업 dedupe에 쓰는 OS별 경로 비교 규칙.
    /// </summary>
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly FileToolsSettings _settings;

    public WorkPlanDisplayBuilder(FileToolsSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// 대상별 step 목록을 실행 순서 기준의 표시 행으로 투영한다.
    /// </summary>
    public IReadOnlyList<WorkPlanDisplayRow> Build(
        IEnumerable<WorkTargetPlan> targets,
        WorkPlanDisplayFilter filter = WorkPlanDisplayFilter.All,
        IEnumerable<WorkTargetPlan>? selectedTargets = null)
    {
        var selectedTargetArray = selectedTargets?.ToArray() ?? [];
        var selectedPaths = selectedTargetArray
            .Select(static target => target.Path)
            .ToHashSet(PathComparer);
        var rows = new List<WorkPlanDisplayRow>();
        var emittedArchiveMergePlanIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previewBuilder = new WorkPlanPreviewBuilder(_settings);
        var order = 1;

        foreach (var target in targets)
        {
            var previews = previewBuilder.Build(target);
            foreach (var preview in previews)
            {
                if (preview.Step.Kind == WorkPlanStepKind.ArchiveMerge &&
                    preview.Step.ArchiveMergeOptions is { } archiveOptions)
                {
                    AddArchiveMergeRows(
                        rows,
                        target,
                        preview,
                        archiveOptions,
                        emittedArchiveMergePlanIds,
                        filter,
                        selectedTargetArray,
                        selectedPaths,
                        ref order);
                    continue;
                }

                if (preview.Step.Kind == WorkPlanStepKind.DuplicateDelete)
                {
                    AddDuplicateDeleteRows(
                        rows,
                        target,
                        preview,
                        filter,
                        selectedTargetArray,
                        selectedPaths,
                        ref order);
                    continue;
                }

                var relatedPaths = GetRelatedPaths(target, preview.Step);
                var matchesFilter = MatchesSelectedTargets(target, relatedPaths, selectedTargetArray, selectedPaths);
                if (!ShouldInclude(filter, preview.HasWarning, matchesFilter))
                {
                    order++;
                    continue;
                }

                rows.Add(new WorkPlanDisplayRow(
                    order,
                    WorkPlanDisplayRowKind.Step,
                    CreateStepOperationKey(target, preview),
                    target,
                    preview.Step,
                    preview,
                    preview.Step.DisplayName,
                    target.Path,
                    preview.PreviewText,
                    preview.HasWarning,
                    matchesFilter || filter == WorkPlanDisplayFilter.All));
                order++;
            }
        }

        return rows;
    }

    /// <summary>
    /// 공유 archive merge는 실행기와 동일하게 PlanId 기준으로 한 번만 표시한다.
    /// </summary>
    private static void AddArchiveMergeRows(
        List<WorkPlanDisplayRow> rows,
        WorkTargetPlan target,
        WorkPlanStepPreview preview,
        ArchiveMergeOptions archiveOptions,
        HashSet<string> emittedArchiveMergePlanIds,
        WorkPlanDisplayFilter filter,
        IReadOnlyList<WorkTargetPlan> selectedTargets,
        HashSet<string> selectedPaths,
        ref int order)
    {
        var planId = string.IsNullOrWhiteSpace(archiveOptions.PlanId)
            ? CreateStepOperationKey(target, preview)
            : archiveOptions.PlanId;
        var operationKey = $"archive:{planId}";
        if (!emittedArchiveMergePlanIds.Add(operationKey))
        {
            return;
        }

        var relatedPaths = archiveOptions.SourcePaths.Count > 0
            ? archiveOptions.SourcePaths
            : [target.Path];
        var matchesFilter = MatchesSelectedTargets(target, relatedPaths, selectedTargets, selectedPaths);
        if (!ShouldInclude(filter, preview.HasWarning, matchesFilter))
        {
            order++;
            return;
        }

        var currentOrder = order++;
        rows.Add(new WorkPlanDisplayRow(
            currentOrder,
            WorkPlanDisplayRowKind.OperationGroup,
            operationKey,
            target,
            preview.Step,
            preview,
            preview.Step.DisplayName,
            Localizer.Format("PlanDisplayInputSummaryFormat", relatedPaths.Count),
            archiveOptions.OutputPath,
            preview.HasWarning,
            matchesFilter || filter == WorkPlanDisplayFilter.All));

        foreach (var sourcePath in relatedPaths)
        {
            var sourceMatchesFilter = selectedPaths.Contains(sourcePath);
            rows.Add(new WorkPlanDisplayRow(
                currentOrder,
                WorkPlanDisplayRowKind.Input,
                operationKey,
                target,
                preview.Step,
                preview,
                Localizer.Get("PlanDisplayInputRow"),
                sourcePath,
                "",
                preview.HasWarning,
                sourceMatchesFilter || filter == WorkPlanDisplayFilter.All));
        }
    }

    /// <summary>
    /// 중복 삭제는 관련 경로를 모두 보여줘야 하므로 그룹 행으로 펼쳐 표시한다.
    /// </summary>
    private static void AddDuplicateDeleteRows(
        List<WorkPlanDisplayRow> rows,
        WorkTargetPlan target,
        WorkPlanStepPreview preview,
        WorkPlanDisplayFilter filter,
        IReadOnlyList<WorkTargetPlan> selectedTargets,
        HashSet<string> selectedPaths,
        ref int order)
    {
        var relatedPaths = preview.Step.DuplicateDeleteGroupPaths.Count > 0
            ? preview.Step.DuplicateDeleteGroupPaths
            : [target.Path];
        var matchesFilter = MatchesSelectedTargets(target, relatedPaths, selectedTargets, selectedPaths);
        if (!ShouldInclude(filter, preview.HasWarning, matchesFilter))
        {
            order++;
            return;
        }

        var currentOrder = order++;
        var operationKey = CreateStepOperationKey(target, preview);
        rows.Add(new WorkPlanDisplayRow(
            currentOrder,
            WorkPlanDisplayRowKind.OperationGroup,
            operationKey,
            target,
            preview.Step,
            preview,
            preview.Step.DisplayName,
            Localizer.Format("PlanDisplayInputSummaryFormat", relatedPaths.Count),
            "",
            preview.HasWarning,
            matchesFilter || filter == WorkPlanDisplayFilter.All));

        foreach (var sourcePath in relatedPaths)
        {
            var sourceMatchesFilter = selectedPaths.Contains(sourcePath);
            rows.Add(new WorkPlanDisplayRow(
                currentOrder,
                WorkPlanDisplayRowKind.Input,
                operationKey,
                target,
                preview.Step,
                preview,
                Localizer.Get("PlanDisplayInputRow"),
                sourcePath,
                "",
                preview.HasWarning,
                sourceMatchesFilter || filter == WorkPlanDisplayFilter.All));
        }
    }

    private static bool ShouldInclude(WorkPlanDisplayFilter filter, bool hasWarning, bool matchesSelectedTargets)
    {
        return filter switch
        {
            WorkPlanDisplayFilter.All => true,
            WorkPlanDisplayFilter.SelectedTargets => matchesSelectedTargets,
            WorkPlanDisplayFilter.Warnings => hasWarning,
            _ => true
        };
    }

    private static bool MatchesSelectedTargets(
        WorkTargetPlan target,
        IEnumerable<string> relatedPaths,
        IReadOnlyList<WorkTargetPlan> selectedTargets,
        HashSet<string> selectedPaths)
    {
        return selectedTargets.Contains(target) ||
               relatedPaths.Any(selectedPaths.Contains);
    }

    private static IReadOnlyList<string> GetRelatedPaths(WorkTargetPlan target, WorkPlanStep step)
    {
        if (step.Kind == WorkPlanStepKind.DuplicateDelete &&
            step.DuplicateDeleteGroupPaths.Count > 0)
        {
            return step.DuplicateDeleteGroupPaths;
        }

        return [target.Path];
    }

    private static string CreateStepOperationKey(WorkTargetPlan target, WorkPlanStepPreview preview)
    {
        return $"{target.Path}|{preview.Number}|{preview.Step.Kind}";
    }
}
