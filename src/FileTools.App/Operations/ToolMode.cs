namespace FileTools;

/// <summary>
/// 명령 실행 모드와 정책 타입을 정의한다.
/// </summary>
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
    FolderMergeSelectedTargets,
    AutoRelocation,
    AutoRelocationCurrentFolder,
    AutoRelocationChooseTarget,
    ArchiveMergeGroupByArchiveName,
    ArchiveMergePreserveInternalPaths,
    FileCompare
}

/// <summary>
/// 열거형 값에 대한 UI 표시명을 제공한다.
/// </summary>
internal static class ToolModeText
{
    /// <summary>
    /// 앱 진입 메뉴 표시명.
    /// </summary>
    public static string OpenAppDisplayName => Localizer.Get("ToolOpenApp");

    /// <summary>
    /// 도구 모드 표시 문자열.
    /// </summary>
    public static string GetDisplayName(ToolMode mode) => mode switch
    {
        ToolMode.FileNameCorrection => Localizer.Get("ToolFileNameCorrection"),
        ToolMode.FolderStructure => Localizer.Get("ToolFolderStructure"),
        ToolMode.AutoRelocation => Localizer.Get("ToolAutoRelocation"),
        ToolMode.ArchiveMerge => Localizer.Get("ToolArchiveMerge"),
        _ => mode.ToString()
    };

    /// <summary>
    /// 폴더 구조 동작 표시 문자열.
    /// </summary>
    public static string GetDisplayName(FolderStructureOperation operation) => operation switch
    {
        FolderStructureOperation.Auto => Localizer.Get("FolderOperationAuto"),
        FolderStructureOperation.WrapFiles => Localizer.Get("FolderOperationWrapFiles"),
        FolderStructureOperation.UnwrapSameNameSingleFile => Localizer.Get("FolderOperationUnwrapSameName"),
        FolderStructureOperation.UnwrapSingleFileFolder => Localizer.Get("FolderOperationUnwrapSingleFile"),
        FolderStructureOperation.MoveInnerFilesUp => Localizer.Get("FolderOperationMoveInnerFilesUp"),
        _ => operation.ToString()
    };

    /// <summary>
    /// Unwrap 충돌 분기 모드 표시 문자열.
    /// </summary>
    public static string GetDisplayName(FolderUnwrapNameMismatchMode mode) => mode switch
    {
        FolderUnwrapNameMismatchMode.KeepFileName => Localizer.Get("FolderUnwrapMismatchKeepFileName"),
        FolderUnwrapNameMismatchMode.UseFolderName => Localizer.Get("FolderUnwrapMismatchUseFolderName"),
        FolderUnwrapNameMismatchMode.PrefixFolderName => Localizer.Get("FolderUnwrapMismatchPrefixFolderName"),
        FolderUnwrapNameMismatchMode.CustomTemplate => Localizer.Get("FolderUnwrapMismatchCustomTemplate"),
        _ => mode.ToString()
    };

    /// <summary>
    /// 컨텍스트 메뉴 배치 방식 표시 문자열.
    /// </summary>
    public static string GetDisplayName(ContextMenuLayout layout) => layout switch
    {
        ContextMenuLayout.Grouped => Localizer.Get("ContextMenuLayoutGrouped"),
        ContextMenuLayout.Expanded => Localizer.Get("ContextMenuLayoutExpanded"),
        _ => layout.ToString()
    };

    /// <summary>
    /// 이름 변경 결과 검토 모드 표시 문자열.
    /// </summary>
    public static string GetDisplayName(RenameReviewMode mode) => mode switch
    {
        RenameReviewMode.Always => Localizer.Get("RenameReviewModeAlways"),
        RenameReviewMode.IssuesOnly => Localizer.Get("RenameReviewModeIssuesOnly"),
        _ => mode.ToString()
    };

    /// <summary>
    /// 컨텍스트 메뉴 커맨드 표시 문자열.
    /// </summary>
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
        ContextMenuCommand.FolderMergeSelectedTargets => Localizer.Get("ContextCommandFolderMergeSelectedTargets"),
        ContextMenuCommand.AutoRelocation => GetDisplayName(ToolMode.AutoRelocation),
        ContextMenuCommand.AutoRelocationCurrentFolder => Localizer.Get("ContextCommandAutoRelocationCurrentFolder"),
        ContextMenuCommand.AutoRelocationChooseTarget => Localizer.Get("ContextCommandAutoRelocationChooseTarget"),
        ContextMenuCommand.ArchiveMergeGroupByArchiveName => Localizer.Get("ContextCommandArchiveMergeGroupByArchiveName"),
        ContextMenuCommand.ArchiveMergePreserveInternalPaths => Localizer.Get("ContextCommandArchiveMergePreserveInternalPaths"),
        ContextMenuCommand.FileCompare => Localizer.Get("ContextCommandFileCompare"),
        _ => command.ToString()
    };
}
