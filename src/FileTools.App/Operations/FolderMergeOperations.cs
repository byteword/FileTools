namespace FileTools;

/// <summary>
/// 폴더 병합 또는 선택 항목 씌우기의 대상 경로와 실행 결과.
/// </summary>
internal sealed record FolderMergeResult(string? TargetFolderPath, OperationResult OperationResult);

internal enum FolderMergeMode
{
    /// <summary>
    /// 선택한 폴더를 통째로 하나씩 병합 대상 폴더 아래로 이동합니다.
    /// </summary>
    MergeFolderUnits,

    /// <summary>
    /// 선택한 폴더의 최상위 내용을 병합 대상 폴더로 이동해 폴더 레벨은 유지하지 않습니다.
    /// </summary>
    MergeFolderContentsOnly
}

internal sealed record FolderMergeOptions(
    string? TargetFolderName,
    FolderMergeMode Mode,
    NameCollisionPolicy CollisionPolicy = NameCollisionPolicy.AutoNumber)
{
    // 확인창을 닫은 뒤 목적지가 달라지면 새 경로로 임의 실행하지 않는다.
    public string? ConfirmedTargetFolderPath { get; init; }
}

internal static class FolderMergeOptionDefaults
{
    public static readonly FolderMergeOptions MergeFolders = new(null, FolderMergeMode.MergeFolderContentsOnly);
    public static readonly FolderMergeOptions WrapSelection = new(null, FolderMergeMode.MergeFolderUnits);
}

internal enum FolderMergePlanPreviewFailureKind
{
    None,
    InsufficientTargets,
    MissingParent,
    TargetFolderCollision,
    InvalidSources
}

/// <summary>
/// 병합 실행 전 필요한 사전 계산 정보를 담는다.
/// </summary>
internal sealed record FolderMergePlanPreview(
    bool IsReady,
    FolderMergePlanPreviewFailureKind FailureKind,
    string? FailureReason,
    IReadOnlyList<string> SourcePaths,
    string? TargetParentPath,
    string TargetFolderName,
    string? TargetFolderPath,
    bool HasMultipleParents);

/// <summary>
/// 여러 경로를 하나의 폴더로 합치는 핵심 실행 로직.
/// </summary>
internal static class FolderMergeOperations
{
    /// <summary>
    /// 경로 비교 규칙(Windows는 대소문자 무시).
    /// </summary>
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// 입력 기반으로 병합 대상 폴더를 계산한다(실제 이동 없음).
    /// </summary>
    public static FolderMergePlanPreview CreateMergePlanPreview(
        IEnumerable<string> paths,
        FileToolsSettings settings,
        FolderMergeOptions? options = null)
    {
        var mergeOptions = options ?? FolderMergeOptionDefaults.MergeFolders;
        var normalizedPaths = NormalizePaths(paths);
        var contentsOnly = mergeOptions.Mode == FolderMergeMode.MergeFolderContentsOnly;
        if (normalizedPaths.Length < (contentsOnly ? 2 : 1))
        {
            return new FolderMergePlanPreview(
                IsReady: false,
                FailureKind: FolderMergePlanPreviewFailureKind.InsufficientTargets,
                FailureReason: Localizer.Get(contentsOnly ? "FolderMergeNeedsMultipleTargets" : "NoSelectedTargetMessage"),
                SourcePaths: normalizedPaths,
                TargetParentPath: null,
                TargetFolderName: string.Empty,
                TargetFolderPath: null,
                HasMultipleParents: false);
        }

        // 폴더 병합은 폴더만 허용하고, 부모/자식 동시 선택은 이동 전에 전체 거부한다.
        var invalidReason = contentsOnly && normalizedPaths.Any(path => !Directory.Exists(path))
            ? Localizer.Get("FolderMergeNeedsMultipleTargets")
            : normalizedPaths.Any(path => !File.Exists(path) && !Directory.Exists(path))
                ? Localizer.Get("FolderSelectionMissingSource")
                : normalizedPaths.Any(parent => Directory.Exists(parent) &&
                    normalizedPaths.Any(child => !PathComparer.Equals(parent, child) && IsSubPathOf(child, parent)))
                    ? Localizer.Get("FolderSelectionOverlappingSources")
                    : null;
        if (invalidReason is not null)
        {
            return new FolderMergePlanPreview(false, FolderMergePlanPreviewFailureKind.InvalidSources,
                invalidReason, normalizedPaths, null, "", null, false);
        }

        var targetParent = ResolveTargetParent(normalizedPaths);
        if (string.IsNullOrWhiteSpace(targetParent))
        {
            return new FolderMergePlanPreview(
                IsReady: false,
                FailureKind: FolderMergePlanPreviewFailureKind.MissingParent,
                FailureReason: Localizer.Get("PlanPreviewNoParent"),
                SourcePaths: normalizedPaths,
                TargetParentPath: null,
                TargetFolderName: string.Empty,
                TargetFolderPath: null,
                HasMultipleParents: false);
        }

        var targetName = ResolveMergeFolderName(normalizedPaths, settings, mergeOptions.TargetFolderName);
        var targetCollision = NameCollisionResolver.Resolve(
            targetParent,
            targetName,
            CreateMergeCollisionOptions(settings, NameCollisionTargetKind.Folder));
        if (!targetCollision.IsReady ||
            (mergeOptions.ConfirmedTargetFolderPath is not null &&
             !PathComparer.Equals(targetCollision.TargetPath, mergeOptions.ConfirmedTargetFolderPath)))
        {
            return new FolderMergePlanPreview(
                IsReady: false,
                FailureKind: FolderMergePlanPreviewFailureKind.TargetFolderCollision,
                FailureReason: Localizer.Format("PlanPreviewTargetExistsFormat", targetCollision.TargetPath),
                SourcePaths: normalizedPaths,
                TargetParentPath: targetParent,
                TargetFolderName: targetName,
                TargetFolderPath: targetCollision.TargetPath,
                HasMultipleParents: HasMultipleParents(normalizedPaths, targetParent));
        }

        return new FolderMergePlanPreview(
            IsReady: true,
                FailureKind: FolderMergePlanPreviewFailureKind.None,
                FailureReason: null,
                SourcePaths: normalizedPaths,
                TargetParentPath: targetParent,
                TargetFolderName: targetName,
                TargetFolderPath: targetCollision.TargetPath,
                HasMultipleParents: HasMultipleParents(normalizedPaths, targetParent));
    }

    /// <summary>
    /// 최종 병합 대상 폴더 경로만 계산한다(실제 이동 없음).
    /// </summary>
    /// <remarks>
    /// 2개 미만 경로는 병합 의미가 없어 null 처리한다.
    /// </remarks>
    public static string? PreviewTargetFolderPath(IEnumerable<string> paths, FileToolsSettings settings)
    {
        var preview = CreateMergePlanPreview(paths, settings);
        return preview.IsReady ? preview.TargetFolderPath : null;
    }

    /// <summary>
    /// 여러 대상 경로를 공통 병합 폴더로 이동한다.
    /// </summary>
    /// <remarks>
    /// 파일/폴더 이동은 개별 실패를 스킵 처리하고 나머지 항목은 계속 진행한다.
    /// 병합 대상이 실제로 이동되지 않았고 폴더만 새로 생성된 경우에는 폴더를 정리한다.
    /// </remarks>
    public static FolderMergeResult MergeIntoFolder(
        IEnumerable<string> paths,
        FileToolsSettings settings,
        FolderMergeOptions? options = null)
    {
        var result = new OperationResult();
        var mergeOptions = options ?? FolderMergeOptionDefaults.MergeFolders;
        var preview = CreateMergePlanPreview(paths, settings, mergeOptions);
        if (!preview.IsReady)
        {
            ApplyPlanFailure(preview, result);
            return new FolderMergeResult(null, result);
        }

        var sourcePaths = preview.SourcePaths;
        var targetFolder = preview.TargetFolderPath;
        if (string.IsNullOrWhiteSpace(targetFolder))
        {
            result.AddError(Localizer.Get("PlanPreviewUnavailable"));
            return new FolderMergeResult(null, result);
        }

        var createdTargetFolder = !Directory.Exists(targetFolder);
        try
        {
            if (!createdTargetFolder)
            {
                result.AddError(Localizer.Format("PlanPreviewTargetExistsFormat", targetFolder));
                return new FolderMergeResult(null, result);
            }

            Directory.CreateDirectory(targetFolder);
        }
        catch (Exception ex)
        {
            result.AddError(targetFolder + " | " + ex.Message);
            return new FolderMergeResult(null, result);
        }

        foreach (var path in sourcePaths)
        {
            result.AddCandidate();
            try
            {
                if (PathComparer.Equals(path, targetFolder))
                {
                    result.AddSkipped(Localizer.Format("FolderMergeSameAsTargetFolderFormat", Path.GetFileName(path)));
                    continue;
                }

                if (File.Exists(path))
                {
                    MoveFile(path, targetFolder, settings, mergeOptions, result);
                    continue;
                }

                if (Directory.Exists(path))
                {
                    if (mergeOptions.Mode == FolderMergeMode.MergeFolderContentsOnly)
                    {
                        MoveDirectoryContents(path, targetFolder, settings, mergeOptions, result);
                    }
                    else
                    {
                        MoveDirectory(path, targetFolder, settings, mergeOptions, result);
                    }

                    continue;
                }

                result.AddSkipped(Localizer.Format("FolderMergeSourceMissingFormat", path));
            }
            catch (Exception ex)
            {
                result.AddError(path + " | " + ex.Message);
            }
        }

        if (createdTargetFolder && result.AppliedCount == 0)
        {
            TryRemoveEmptyDirectory(targetFolder, result);
        }

        return new FolderMergeResult(Directory.Exists(targetFolder) ? targetFolder : null, result);
    }

    /// <summary>
    /// 파일 이동을 충돌 해소 후 수행한다.
    /// </summary>
    private static void MoveFile(
        string sourcePath,
        string targetFolder,
        FileToolsSettings settings,
        FolderMergeOptions options,
        OperationResult result)
    {
        var collision = NameCollisionResolver.Resolve(
            targetFolder,
            Path.GetFileName(sourcePath),
            CreateMergeCollisionOptions(settings, NameCollisionTargetKind.File, options.CollisionPolicy));
        if (!collision.IsReady)
        {
            result.AddSkipped(Localizer.Format("FolderMergeFileNameCollisionFormat", Path.GetFileName(sourcePath)));
            return;
        }

        File.Move(sourcePath, collision.TargetPath);
        result.AddApplied(Path.GetFileName(sourcePath) + " -> " + Path.GetFileName(targetFolder) + "\\" + collision.TargetName);
        FileToolsEnvironment.Log("MERGE-FILE", sourcePath + " -> " + collision.TargetPath);
    }

    /// <summary>
    /// 폴더 이동을 충돌/자기 하위 이동 방지까지 검사해 수행한다.
    /// </summary>
    private static void MoveDirectory(
        string sourcePath,
        string targetFolder,
        FileToolsSettings settings,
        FolderMergeOptions options,
        OperationResult result)
    {
        if (IsSubPathOf(targetFolder, sourcePath))
        {
            result.AddSkipped(Localizer.Format("FolderMergeSourceContainsTargetFormat", Path.GetFileName(sourcePath)));
            return;
        }

        var collision = NameCollisionResolver.Resolve(
            targetFolder,
            Path.GetFileName(sourcePath),
            CreateMergeCollisionOptions(settings, NameCollisionTargetKind.Folder, options.CollisionPolicy));
        if (!collision.IsReady)
        {
            result.AddSkipped(Localizer.Format("FolderMergeFolderNameCollisionFormat", Path.GetFileName(sourcePath)));
            return;
        }

        Directory.Move(sourcePath, collision.TargetPath);
        result.AddApplied(Path.GetFileName(sourcePath) + "\\ -> " + Path.GetFileName(targetFolder) + "\\" + collision.TargetName + "\\");
        FileToolsEnvironment.Log("MERGE-FOLDER", sourcePath + " -> " + collision.TargetPath);
    }

    /// <summary>
    /// 동명 하위 폴더를 재귀적으로 합치고, 항목별 실패를 격리한 뒤 빈 원본만 정리한다.
    /// </summary>
    private static void MoveDirectoryContents(
        string sourcePath,
        string targetFolder,
        FileToolsSettings settings,
        FolderMergeOptions options,
        OperationResult result)
    {
        if (!Directory.Exists(sourcePath))
        {
            result.AddSkipped(Localizer.Format("FolderMergeSourceMissingFormat", sourcePath));
            return;
        }

        // 재귀 병합은 링크를 따라가지 않으며, 이동 중 열거 대상이 바뀌지 않도록 스냅샷을 만든다.
        if (IsSubPathOf(targetFolder, sourcePath) ||
            (File.GetAttributes(sourcePath) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(targetFolder) & FileAttributes.ReparsePoint) != 0)
        {
            result.AddSkipped(Localizer.Format("FolderMergeUnsafeDirectoryFormat", sourcePath));
            return;
        }

        var entries = Directory.GetFileSystemEntries(sourcePath);
        foreach (var sourceEntry in entries)
        {
            try
            {
                if (File.Exists(sourceEntry))
                {
                    MoveFile(sourceEntry, targetFolder, settings, options, result);
                }
                else if (Directory.Exists(sourceEntry))
                {
                    var matchingFolder = Path.Combine(targetFolder, Path.GetFileName(sourceEntry));
                    if (Directory.Exists(matchingFolder))
                    {
                        MoveDirectoryContents(sourceEntry, matchingFolder, settings, options, result);
                    }
                    else
                    {
                        // 새 하위 폴더도 항목별로 처리해 잠긴 파일이 형제 파일을 막지 않게 한다.
                        var collision = NameCollisionResolver.Resolve(targetFolder, Path.GetFileName(sourceEntry),
                            CreateMergeCollisionOptions(settings, NameCollisionTargetKind.Folder, options.CollisionPolicy));
                        if (!collision.IsReady)
                        {
                            result.AddSkipped(Localizer.Format("FolderMergeFolderNameCollisionFormat", sourceEntry));
                            continue;
                        }

                        if ((File.GetAttributes(sourceEntry) & FileAttributes.ReparsePoint) != 0)
                        {
                            result.AddSkipped(Localizer.Format("FolderMergeUnsafeDirectoryFormat", sourceEntry));
                            continue;
                        }

                        Directory.CreateDirectory(collision.TargetPath);
                        MoveDirectoryContents(sourceEntry, collision.TargetPath, settings, options, result);
                        if (Directory.Exists(sourceEntry))
                        {
                            TryRemoveEmptyDirectory(collision.TargetPath, result);
                        }
                    }
                }
                else
                {
                    result.AddError(Localizer.Format("FolderMergeSourceMissingFormat", sourceEntry));
                }
            }
            catch (Exception ex)
            {
                result.AddError(sourceEntry + " | " + ex.Message);
            }
        }

        TryRemoveEmptyDirectory(sourcePath, result);
        if (entries.Length == 0 && !Directory.Exists(sourcePath))
        {
            result.AddApplied(sourcePath + " -> " + targetFolder);
        }
    }

    /// <summary>이동 후 빈 폴더만 삭제하고, 정리 실패도 결과에 기록한다.</summary>
    private static void TryRemoveEmptyDirectory(string path, OperationResult result)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path, recursive: false);
            }
        }
        catch (Exception ex)
        {
            result.AddError(path + " | " + ex.Message);
        }
    }

    /// <summary>
    /// 충돌 정책을 병합 동작에 맞춰 기본 옵션으로 변환한다.
    /// </summary>
    private static NameCollisionOptions CreateMergeCollisionOptions(
        FileToolsSettings settings,
        NameCollisionTargetKind targetKind,
        NameCollisionPolicy policy = NameCollisionPolicy.AutoNumber)
    {
        return new NameCollisionOptions
        {
            Policy = policy == NameCollisionPolicy.Skip ? NameCollisionPolicy.Skip : NameCollisionPolicy.AutoNumber,
            TargetKind = targetKind,
            ConflictNameTemplate = settings.FolderStructureConflictNameTemplate,
            IndexStyle = settings.FolderStructureConflictIndexStyle
        };
    }

    /// <summary>
    /// 공통 stem 기반으로 병합 폴더명을 결정한다.
    /// </summary>
    private static string ResolveMergeFolderName(
        IReadOnlyList<string> paths,
        FileToolsSettings settings,
        string? overrideFolderName)
    {
        if (!string.IsNullOrWhiteSpace(overrideFolderName))
        {
            return WindowsFileNameSafety.MakeSafeFileName(overrideFolderName);
        }

        var proposal = MergeNameProposalBuilder.CreateForPaths(paths, settings, Localizer.Get("DefaultMergeFolderName"));
        var commonStem = proposal.Stem;
        var firstPath = paths[0];
        var firstFileName = Path.GetFileName(firstPath);
        var firstExtension = Directory.Exists(firstPath) ? "" : Path.GetExtension(firstFileName);
        var context = new NameTemplateContext
        {
            SourcePath = firstPath,
            FileName = firstFileName,
            FileStem = Directory.Exists(firstPath) ? firstFileName : Path.GetFileNameWithoutExtension(firstFileName),
            Extension = firstExtension,
            ExtensionNoDot = firstExtension.TrimStart('.'),
            CommonStem = commonStem,
            FirstFileStem = proposal.OriginalStems.FirstOrDefault(),
            SelectedCount = paths.Count
        };
        var template = paths.All(File.Exists)
            ? NameTemplateDefaults.MultiFileMergeFolderNameTemplate
            : NameTemplateDefaults.MultiFolderMergeFolderNameTemplate;
        var evaluation = NameTemplateResolver.CreateDefault(settings).Evaluate(template, context);
        return WindowsFileNameSafety.MakeSafeFileName(evaluation.IsReady ? evaluation.Value : commonStem);
    }

    /// <summary>
    /// 병합 타겟의 상위 폴더를 계산한다.
    /// </summary>
    private static string? ResolveTargetParent(IReadOnlyList<string> paths)
    {
        var firstParent = Path.GetDirectoryName(paths[0]);
        if (string.IsNullOrWhiteSpace(firstParent))
        {
            return null;
        }

        return firstParent;
    }

    /// <summary>
    /// 입력 경로를 정규화하고 중복을 제거한다. 존재 여부는 선택 검증에서 확인한다.
    /// </summary>
    private static string[] NormalizePaths(IEnumerable<string> paths)
    {
        return paths
            .Select(static path => path.Trim().Trim('"'))
            .Where(static path => path.Length > 0)
            .Select(static path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
            .Distinct(PathComparer)
            .ToArray();
    }

    /// <summary>
    /// preview 단계에서 서로 다른 부모 선택 여부를 판정한다.
    /// </summary>
    private static bool HasMultipleParents(IReadOnlyList<string> sourcePaths, string firstParent)
    {
        return sourcePaths
            .Select(static path => Path.GetDirectoryName(path))
            .Where(static parent => !string.IsNullOrWhiteSpace(parent))
            .Any(parent => !PathComparer.Equals(parent, firstParent));
    }

    /// <summary>
    /// 후보 경로가 지정 부모 내부인지 판정한다.
    /// </summary>
    private static bool IsSubPathOf(string candidatePath, string parentPath)
    {
        var parentFull = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidateFull = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return candidateFull.StartsWith(parentFull, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
    }

    /// <summary>
    /// 미리보기 실패를 실행 결과로 변환한다.
    /// </summary>
    private static void ApplyPlanFailure(FolderMergePlanPreview preview, OperationResult result)
    {
        var reason = preview.FailureReason ?? Localizer.Get("PlanPreviewUnavailable");
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        switch (preview.FailureKind)
        {
            case FolderMergePlanPreviewFailureKind.MissingParent:
                result.AddError(reason);
                break;

            default:
                result.AddSkipped(reason);
                break;
        }
    }
}
