namespace FileTools;

internal enum ToolMode
{
    FileNameCorrection,
    FolderStructure,
    AutoRelocation,
    ArchiveMerge
}

internal enum FolderStructureOperation
{
    Auto,
    WrapFiles,
    UnwrapSameNameSingleFile,
    UnwrapSingleFileFolder,
    MoveInnerFilesUp
}

internal enum FolderUnwrapNameMismatchMode
{
    KeepFileName,
    UseFolderName,
    PrefixFolderName,
    CustomTemplate
}

internal enum ContextMenuLayout
{
    Grouped,
    Expanded
}

internal enum RenameReviewMode
{
    Always,
    IssuesOnly
}

internal enum ContextMenuCommand
{
    OpenApp,
    FileNameCorrection,
    FolderStructure,
    FolderWrapFiles,
    FolderUnwrapSameNameSingleFile,
    FolderUnwrapSingleFile,
    FolderUnwrapUseFolderName,
    FolderUnwrapKeepFileName,
    FolderMoveInnerFilesUp,
    AutoRelocation,
    AutoRelocationCurrentFolder,
    AutoRelocationChooseTarget,
    ArchiveMergeGroupByArchiveName,
    ArchiveMergePreserveInternalPaths,
    FileCompare
}

internal static class ToolModeText
{
    public static string OpenAppDisplayName => Localizer.Get("ToolOpenApp");

    public static string GetDisplayName(ToolMode mode) => mode switch
    {
        ToolMode.FileNameCorrection => Localizer.Get("ToolFileNameCorrection"),
        ToolMode.FolderStructure => Localizer.Get("ToolFolderStructure"),
        ToolMode.AutoRelocation => Localizer.Get("ToolAutoRelocation"),
        ToolMode.ArchiveMerge => Localizer.Get("ToolArchiveMerge"),
        _ => mode.ToString()
    };

    public static string GetDisplayName(FolderStructureOperation operation) => operation switch
    {
        FolderStructureOperation.Auto => Localizer.Get("FolderOperationAuto"),
        FolderStructureOperation.WrapFiles => Localizer.Get("FolderOperationWrapFiles"),
        FolderStructureOperation.UnwrapSameNameSingleFile => Localizer.Get("FolderOperationUnwrapSameName"),
        FolderStructureOperation.UnwrapSingleFileFolder => Localizer.Get("FolderOperationUnwrapSingleFile"),
        FolderStructureOperation.MoveInnerFilesUp => Localizer.Get("FolderOperationMoveInnerFilesUp"),
        _ => operation.ToString()
    };

    public static string GetDisplayName(FolderUnwrapNameMismatchMode mode) => mode switch
    {
        FolderUnwrapNameMismatchMode.KeepFileName => Localizer.Get("FolderUnwrapMismatchKeepFileName"),
        FolderUnwrapNameMismatchMode.UseFolderName => Localizer.Get("FolderUnwrapMismatchUseFolderName"),
        FolderUnwrapNameMismatchMode.PrefixFolderName => Localizer.Get("FolderUnwrapMismatchPrefixFolderName"),
        FolderUnwrapNameMismatchMode.CustomTemplate => Localizer.Get("FolderUnwrapMismatchCustomTemplate"),
        _ => mode.ToString()
    };

    public static string GetDisplayName(ContextMenuLayout layout) => layout switch
    {
        ContextMenuLayout.Grouped => Localizer.Get("ContextMenuLayoutGrouped"),
        ContextMenuLayout.Expanded => Localizer.Get("ContextMenuLayoutExpanded"),
        _ => layout.ToString()
    };

    public static string GetDisplayName(RenameReviewMode mode) => mode switch
    {
        RenameReviewMode.Always => Localizer.Get("RenameReviewModeAlways"),
        RenameReviewMode.IssuesOnly => Localizer.Get("RenameReviewModeIssuesOnly"),
        _ => mode.ToString()
    };

    public static string GetDisplayName(ContextMenuCommand command) => command switch
    {
        ContextMenuCommand.OpenApp => OpenAppDisplayName,
        ContextMenuCommand.FileNameCorrection => GetDisplayName(ToolMode.FileNameCorrection),
        ContextMenuCommand.FolderStructure => GetDisplayName(ToolMode.FolderStructure),
        ContextMenuCommand.FolderWrapFiles => GetDisplayName(FolderStructureOperation.WrapFiles),
        ContextMenuCommand.FolderUnwrapSameNameSingleFile => GetDisplayName(FolderStructureOperation.UnwrapSameNameSingleFile),
        ContextMenuCommand.FolderUnwrapSingleFile => GetDisplayName(FolderStructureOperation.UnwrapSingleFileFolder),
        ContextMenuCommand.FolderUnwrapUseFolderName => Localizer.Get("ContextCommandFolderUnwrapUseFolderName"),
        ContextMenuCommand.FolderUnwrapKeepFileName => Localizer.Get("ContextCommandFolderUnwrapKeepFileName"),
        ContextMenuCommand.FolderMoveInnerFilesUp => GetDisplayName(FolderStructureOperation.MoveInnerFilesUp),
        ContextMenuCommand.AutoRelocation => GetDisplayName(ToolMode.AutoRelocation),
        ContextMenuCommand.AutoRelocationCurrentFolder => Localizer.Get("ContextCommandAutoRelocationCurrentFolder"),
        ContextMenuCommand.AutoRelocationChooseTarget => Localizer.Get("ContextCommandAutoRelocationChooseTarget"),
        ContextMenuCommand.ArchiveMergeGroupByArchiveName => Localizer.Get("ContextCommandArchiveMergeGroupByArchiveName"),
        ContextMenuCommand.ArchiveMergePreserveInternalPaths => Localizer.Get("ContextCommandArchiveMergePreserveInternalPaths"),
        ContextMenuCommand.FileCompare => Localizer.Get("ContextCommandFileCompare"),
        _ => command.ToString()
    };
}
