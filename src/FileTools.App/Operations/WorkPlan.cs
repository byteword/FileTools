namespace FileTools;

internal enum WorkPlanStepKind
{
    FileNameCorrection,
    FolderWrap,
    FolderUnwrap,
    AutoRelocation,
    ArchiveMerge,
    DuplicateDelete
}

internal sealed class WorkTargetPlan
{
    public WorkTargetPlan(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

    public List<WorkPlanStep> Steps { get; } = [];

    public override string ToString()
    {
        var name = System.IO.Path.GetFileName(Path);
        return string.IsNullOrWhiteSpace(name) ? Path : name;
    }
}

internal sealed class WorkPlanStep
{
    public WorkPlanStepKind Kind { get; set; }

    public FolderStructureOperation FolderOperation { get; set; } = FolderStructureOperation.UnwrapSameNameSingleFile;

    public FolderUnwrapNameMismatchMode FolderUnwrapNameMismatchMode { get; set; } = FolderUnwrapNameMismatchMode.KeepFileName;

    public string? ManualRenameFileName { get; set; }

    public string? AutoRelocationTemplateId { get; set; }

    public string? ManualTargetRootPath { get; set; }

    public ArchiveMergeOptions? ArchiveMergeOptions { get; set; }

    public IReadOnlyList<string> DuplicateDeleteGroupPaths { get; set; } = [];

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

    public override string ToString()
    {
        return DisplayName;
    }

    private string FormatAutoRelocationName()
    {
        var template = string.IsNullOrWhiteSpace(AutoRelocationTemplateId)
            ? AutoRelocationTemplateDefaults.DefaultTemplateId
            : AutoRelocationTemplateId;
        return string.IsNullOrWhiteSpace(ManualTargetRootPath)
            ? $"{ToolModeText.GetDisplayName(ToolMode.AutoRelocation)} ({template})"
            : $"{ToolModeText.GetDisplayName(ToolMode.AutoRelocation)} ({template} -> {ManualTargetRootPath})";
    }

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

    private string FormatFolderUnwrapName()
    {
        if (FolderOperation != FolderStructureOperation.UnwrapSingleFileFolder)
        {
            return ToolModeText.GetDisplayName(FolderOperation);
        }

        return $"{ToolModeText.GetDisplayName(FolderOperation)} ({ToolModeText.GetDisplayName(FolderUnwrapNameMismatchMode)})";
    }
}
