namespace FileTools;

/// <summary>
/// 실행기에서 처리할 작업 유형을 구분하는 단계 종류.
/// </summary>
internal enum WorkPlanStepKind
{
    FileNameCorrection,
    FolderWrap,
    FolderUnwrap,
    AutoRelocation,
    ArchiveMerge,
    DuplicateDelete
}

/// <summary>
/// 단일 대상(파일/폴더) 기준의 전체 작업 계획을 담는다.
/// </summary>
internal sealed class WorkTargetPlan
{
    /// <summary>
    /// 대상 경로를 정규화해서 저장한다.
    /// </summary>
    /// <param name="path">실행 대상의 파일/폴더 경로</param>
    public WorkTargetPlan(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
    }

    /// <summary>
    /// 실행 대상의 절대 경로.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// 순차 실행될 단계 목록.
    /// </summary>
    public List<WorkPlanStep> Steps { get; } = [];

    /// <summary>
    /// UI/로그에서 보여줄 식별명으로 원본 파일명 또는 전체 경로를 반환한다.
    /// </summary>
    public override string ToString()
    {
        var name = System.IO.Path.GetFileName(Path);
        return string.IsNullOrWhiteSpace(name) ? Path : name;
    }
}

/// <summary>
/// 대상별로 적용할 개별 단계 정보를 가진 실행 정의.
/// </summary>
internal sealed class WorkPlanStep
{
    /// <summary>
    /// 단계 기본값은 해제/이름 복원 없는 래핑-해제 형태를 제외하고
    /// 각 동작의 설정을 포함한다.
    /// </summary>
    public WorkPlanStepKind Kind { get; set; }

    /// <summary>
    /// 폴더 구조 작업 타입.
    /// </summary>
    public FolderStructureOperation FolderOperation { get; set; } = FolderStructureOperation.UnwrapSameNameSingleFile;

    /// <summary>
    /// 폴더 언랩 시 이름 불일치 처리 모드.
    /// </summary>
    public FolderUnwrapNameMismatchMode FolderUnwrapNameMismatchMode { get; set; } = FolderUnwrapNameMismatchMode.KeepFileName;

    /// <summary>
    /// 수동 파일명 변경값.
    /// </summary>
    public string? ManualRenameFileName { get; set; }

    /// <summary>
    /// 자동 재배치 템플릿 식별자.
    /// </summary>
    public string? AutoRelocationTemplateId { get; set; }

    /// <summary>
    /// 자동 재배치 타깃 루트 오버라이드 경로.
    /// </summary>
    public string? ManualTargetRootPath { get; set; }

    /// <summary>
    /// 아카이브 병합에 필요한 실행 옵션.
    /// </summary>
    public ArchiveMergeOptions? ArchiveMergeOptions { get; set; }

    /// <summary>
    /// 중복 삭제 단계에서 함께 삭제될 경로 목록.
    /// </summary>
    public IReadOnlyList<string> DuplicateDeleteGroupPaths { get; set; } = [];

    /// <summary>
    /// UI/로그에서 표시할 단계 이름.
    /// </summary>
    public string DisplayName => Kind switch
    {
        WorkPlanStepKind.FileNameCorrection => ToolModeText.GetDisplayName(ToolMode.FileNameCorrection),
        WorkPlanStepKind.FolderWrap => ToolModeText.GetDisplayName(FolderStructureOperation.WrapFiles),
        WorkPlanStepKind.FolderUnwrap => FormatFolderUnwrapName(),
        WorkPlanStepKind.AutoRelocation => FormatAutoRelocationName(),
        WorkPlanStepKind.ArchiveMerge => FormatArchiveMergeName(),
        WorkPlanStepKind.DuplicateDelete => Localizer.Get("PlanActionDuplicateDelete"),
        _ => Kind.ToString()
    };

    /// <summary>
    /// 자동이동 대상 경로 템플릿 문자열을 포함해 표시명을 구성한다.
    /// </summary>
    public WorkPlanStep Clone()
    {
        return new WorkPlanStep
        {
            Kind = Kind,
            FolderOperation = FolderOperation,
            FolderUnwrapNameMismatchMode = FolderUnwrapNameMismatchMode,
            ManualRenameFileName = ManualRenameFileName,
            AutoRelocationTemplateId = AutoRelocationTemplateId,
            ManualTargetRootPath = ManualTargetRootPath,
            ArchiveMergeOptions = ArchiveMergeOptions?.Clone(),
            DuplicateDeleteGroupPaths = DuplicateDeleteGroupPaths.ToArray()
        };
    }

    /// <summary>
    /// 단계 표현 문자열을 간단 표기한다.
    /// </summary>
    public override string ToString()
    {
        return DisplayName;
    }

    /// <summary>
    /// 자동 재배치 단계 표시명을 템플릿/수동 경로 정보까지 반영해 만든다.
    /// </summary>
    private string FormatAutoRelocationName()
    {
        var template = string.IsNullOrWhiteSpace(AutoRelocationTemplateId)
            ? AutoRelocationTemplateDefaults.DefaultTemplateId
            : AutoRelocationTemplateId;
        return string.IsNullOrWhiteSpace(ManualTargetRootPath)
                ? $"{ToolModeText.GetDisplayName(ToolMode.AutoRelocation)} ({template})"
                : $"{ToolModeText.GetDisplayName(ToolMode.AutoRelocation)} ({template} -> {ManualTargetRootPath})";
    }

    /// <summary>
    /// 아카이브 병합 단계 표시에 원본 소스/옵션 정보를 반영한다.
    /// </summary>
    private string FormatArchiveMergeName()
    {
        if (ArchiveMergeOptions is null)
        {
            return ToolModeText.GetDisplayName(ToolMode.ArchiveMerge);
        }

        return Localizer.Format(
            "ArchiveMergeStepDisplayFormat",
            ArchiveMergeOptions.SourcePaths.Count,
            Path.GetFileName(ArchiveMergeOptions.OutputPath),
            ArchiveMergeText.GetDisplayName(ArchiveMergeOptions.Layout));
    }

    /// <summary>
    /// Unwrap 모드에 따라 단일 파일/폴더 불일치 규칙 이름을 표시한다.
    /// </summary>
    private string FormatFolderUnwrapName()
    {
        if (FolderOperation != FolderStructureOperation.UnwrapSingleFileFolder)
        {
            return ToolModeText.GetDisplayName(FolderOperation);
        }

        return $"{ToolModeText.GetDisplayName(FolderOperation)} ({ToolModeText.GetDisplayName(FolderUnwrapNameMismatchMode)})";
    }
}
