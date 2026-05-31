namespace FileTools;

internal enum ToolMode
{
    FileNameCorrection,
    FolderStructure,
    AutoRelocation
}

internal enum FolderStructureOperation
{
    Auto,
    WrapFiles,
    UnwrapSameNameSingleFile,
    UnwrapSingleFileFolder,
    MoveInnerFilesUp
}

internal enum ContextMenuLayout
{
    Grouped,
    Expanded
}

internal static class ToolModeText
{
    public static string OpenAppDisplayName => Localizer.Get("ToolOpenApp");

    public static string GetDisplayName(ToolMode mode) => mode switch
    {
        ToolMode.FileNameCorrection => Localizer.Get("ToolFileNameCorrection"),
        ToolMode.FolderStructure => Localizer.Get("ToolFolderStructure"),
        ToolMode.AutoRelocation => Localizer.Get("ToolAutoRelocation"),
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

    public static string GetDisplayName(ContextMenuLayout layout) => layout switch
    {
        ContextMenuLayout.Grouped => Localizer.Get("ContextMenuLayoutGrouped"),
        ContextMenuLayout.Expanded => Localizer.Get("ContextMenuLayoutExpanded"),
        _ => layout.ToString()
    };
}
