namespace FileTools;

/// <summary>
/// 병합 대상 폴더/파일의 미리보기 없이 이동 경로 계산부터 적용까지 처리하는 유틸리티.
/// </summary>
internal sealed record FolderMergeResult(string? TargetFolderPath, OperationResult OperationResult);

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
    /// 최종 병합 대상 폴더 경로만 계산한다(실제 이동 없음).
    /// </summary>
    /// <remarks>
    /// 2개 미만 경로는 병합 의미가 없어 null 처리한다.
    /// </remarks>
    public static string? PreviewTargetFolderPath(IEnumerable<string> paths, FileToolsSettings settings)
    {
        var normalizedPaths = NormalizePaths(paths);
        if (normalizedPaths.Length < 2)
        {
            return null;
        }

        var parent = ResolveTargetParent(normalizedPaths);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return null;
        }

        var targetName = ResolveMergeFolderName(normalizedPaths, settings);
        var collision = NameCollisionResolver.Resolve(
            parent,
            targetName,
            CreateMergeCollisionOptions(settings, NameCollisionTargetKind.Folder));
        return collision.IsReady ? collision.TargetPath : null;
    }

    /// <summary>
    /// 여러 대상 경로를 공통 병합 폴더로 이동한다.
    /// </summary>
    /// <remarks>
    /// 파일/폴더 이동은 개별 실패를 스킵 처리하고 나머지 항목은 계속 진행한다.
    /// 병합 대상이 실제로 이동되지 않았고 폴더만 새로 생성된 경우에는 폴더를 정리한다.
    /// </remarks>
    public static FolderMergeResult MergeIntoFolder(IEnumerable<string> paths, FileToolsSettings settings)
    {
        var result = new OperationResult();
        var normalizedPaths = NormalizePaths(paths);
        if (normalizedPaths.Length < 2)
        {
            result.AddSkipped(Localizer.Get("FolderMergeNeedsMultipleTargets"));
            return new FolderMergeResult(null, result);
        }

        var parent = ResolveTargetParent(normalizedPaths);
        if (string.IsNullOrWhiteSpace(parent))
        {
            result.AddError(Localizer.Get("PlanPreviewNoParent"));
            return new FolderMergeResult(null, result);
        }

        var targetName = ResolveMergeFolderName(normalizedPaths, settings);
        var targetCollision = NameCollisionResolver.Resolve(
            parent,
            targetName,
            CreateMergeCollisionOptions(settings, NameCollisionTargetKind.Folder));
        if (!targetCollision.IsReady)
        {
            result.AddSkipped(Localizer.Format("PlanPreviewTargetExistsFormat", targetCollision.TargetPath));
            return new FolderMergeResult(null, result);
        }

        var targetFolder = targetCollision.TargetPath;
        var createdTargetFolder = !Directory.Exists(targetFolder);
        Directory.CreateDirectory(targetFolder);

        foreach (var path in normalizedPaths)
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
                    MoveFile(path, targetFolder, settings, result);
                    continue;
                }

                if (Directory.Exists(path))
                {
                    MoveDirectory(path, targetFolder, settings, result);
                    continue;
                }

                result.AddSkipped(Localizer.Format("FolderMergeSourceMissingFormat", path));
            }
            catch (Exception ex)
            {
                result.AddError(path + " | " + ex.Message);
            }
        }

        if (createdTargetFolder &&
            result.AppliedCount == 0 &&
            Directory.Exists(targetFolder) &&
            !Directory.EnumerateFileSystemEntries(targetFolder).Any())
        {
            Directory.Delete(targetFolder, recursive: false);
            return new FolderMergeResult(null, result);
        }

        return new FolderMergeResult(targetFolder, result);
    }

    /// <summary>
    /// 파일 이동을 충돌 해소 후 수행한다.
    /// </summary>
    private static void MoveFile(
        string sourcePath,
        string targetFolder,
        FileToolsSettings settings,
        OperationResult result)
    {
        var collision = NameCollisionResolver.Resolve(
            targetFolder,
            Path.GetFileName(sourcePath),
            CreateMergeCollisionOptions(settings, NameCollisionTargetKind.File));
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
            CreateMergeCollisionOptions(settings, NameCollisionTargetKind.Folder));
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
    /// 충돌 정책을 병합 동작에 맞춰 기본 옵션으로 변환한다.
    /// </summary>
    private static NameCollisionOptions CreateMergeCollisionOptions(
        FileToolsSettings settings,
        NameCollisionTargetKind targetKind)
    {
        return new NameCollisionOptions
        {
            Policy = NameCollisionPolicy.AutoNumber,
            TargetKind = targetKind,
            ConflictNameTemplate = settings.FolderStructureConflictNameTemplate,
            IndexStyle = settings.FolderStructureConflictIndexStyle
        };
    }

    /// <summary>
    /// 공통 stem 기반으로 병합 폴더명을 결정한다.
    /// </summary>
    private static string ResolveMergeFolderName(IReadOnlyList<string> paths, FileToolsSettings settings)
    {
        var stems = paths
            .Select(GetPathStem)
            .Where(static stem => !string.IsNullOrWhiteSpace(stem))
            .ToArray();
        var commonStem = CreateCommonStem(stems);
        if (string.IsNullOrWhiteSpace(commonStem))
        {
            commonStem = Localizer.Get("DefaultMergeFolderName");
        }

        var context = new NameTemplateContext
        {
            CommonStem = commonStem,
            FirstFileStem = stems.FirstOrDefault(),
            SelectedCount = paths.Count
        };
        var template = paths.All(File.Exists)
            ? NameTemplateDefaults.MultiFileMergeFolderNameTemplate
            : NameTemplateDefaults.MultiFolderMergeFolderNameTemplate;
        var evaluation = NameTemplateResolver.CreateDefault(settings).Evaluate(template, context);
        return WindowsFileNameSafety.MakeSafeFileName(evaluation.IsReady ? evaluation.Value : commonStem);
    }

    /// <summary>
    /// 다중 경로의 시작 공통 접두사를 계산한다.
    /// </summary>
    private static string CreateCommonStem(IReadOnlyList<string> stems)
    {
        if (stems.Count == 0)
        {
            return "";
        }

        var prefix = stems[0];
        foreach (var stem in stems.Skip(1))
        {
            var length = 0;
            var max = Math.Min(prefix.Length, stem.Length);
            while (length < max && char.ToUpperInvariant(prefix[length]) == char.ToUpperInvariant(stem[length]))
            {
                length++;
            }

            prefix = prefix[..length];
            if (prefix.Length == 0)
            {
                break;
            }
        }

        return prefix.Trim().TrimEnd(' ', '.', '-', '_', '[', '(', '{');
    }

    /// <summary>
    /// 폴더/파일 구분에 따라 원본 stem 문자열을 추출한다.
    /// </summary>
    private static string GetPathStem(string path)
    {
        return Directory.Exists(path)
            ? Path.GetFileName(path)
            : Path.GetFileNameWithoutExtension(path);
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
    /// 입력 경로를 중복 제거 및 실제 존재 경로로 정규화한다.
    /// </summary>
    private static string[] NormalizePaths(IEnumerable<string> paths)
    {
        return paths
            .Select(static path => path.Trim().Trim('"'))
            .Where(static path => path.Length > 0 && (File.Exists(path) || Directory.Exists(path)))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray();
    }

    /// <summary>
    /// candidate 경로가 지정 부모 내부인지 판정한다.
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
}
