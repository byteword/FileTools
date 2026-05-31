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

internal static class ToolModeText
{
    public static string GetDisplayName(ToolMode mode) => mode switch
    {
        ToolMode.FileNameCorrection => "파일이름 자동 교정",
        ToolMode.FolderStructure => "폴더 wrapping / unwrapping",
        ToolMode.AutoRelocation => "폴더 자동 재배치",
        _ => mode.ToString()
    };

    public static string GetDisplayName(FolderStructureOperation operation) => operation switch
    {
        FolderStructureOperation.Auto => "자동: 파일은 wrapping, 폴더는 unwrapping",
        FolderStructureOperation.WrapFiles => "파일 wrapping",
        FolderStructureOperation.UnwrapSameNameSingleFile => "같은 이름 단일 파일 폴더 unwrapping",
        FolderStructureOperation.UnwrapSingleFileFolder => "단일 파일 폴더 unwrapping",
        FolderStructureOperation.MoveInnerFilesUp => "폴더 내부 파일 상위로 이동",
        _ => operation.ToString()
    };
}
