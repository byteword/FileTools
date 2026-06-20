namespace FileTools;

internal sealed record FolderWrapPlanPreview(
    bool IsReady,
    string SourcePath,
    string TargetFolderName,
    string? TargetFolderPath,
    string? TargetFilePath,
    string? FailureReason);

internal static class FolderWrapOperations
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static FolderWrapPlanPreview CreatePreview(
        string filePath,
        FileToolsSettings settings,
        string? targetFolderName = null)
    {
        var sourcePath = Path.GetFullPath(filePath);
        if (!File.Exists(sourcePath))
        {
            return new FolderWrapPlanPreview(false, sourcePath, "", null, null, Localizer.Get("PlanPreviewNotFile"));
        }

        var parent = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return new FolderWrapPlanPreview(false, sourcePath, "", null, null, Localizer.Get("PlanPreviewNoParent"));
        }

        var folderName = string.IsNullOrWhiteSpace(targetFolderName)
            ? FolderStructureNameTemplates.ResolveWrapFolderName(sourcePath, settings)
            : WindowsFileNameSafety.MakeSafeFileName(targetFolderName.Trim());
        var targetFolder = Path.Combine(parent, folderName);
        if (!Directory.Exists(targetFolder))
        {
            var folderCollision = NameCollisionResolver.Resolve(
                parent,
                folderName,
                FolderStructureCollisionOptions.Create(settings, NameCollisionTargetKind.Folder));
            targetFolder = folderCollision.TargetPath;
            if (!folderCollision.IsReady)
            {
                return new FolderWrapPlanPreview(
                    false,
                    sourcePath,
                    folderName,
                    targetFolder,
                    null,
                    Localizer.Format("PlanPreviewTargetExistsFormat", targetFolder));
            }
        }

        var fileCollision = NameCollisionResolver.Resolve(
            targetFolder,
            Path.GetFileName(sourcePath),
            FolderStructureCollisionOptions.Create(settings, NameCollisionTargetKind.File));
        if (!fileCollision.IsReady)
        {
            return new FolderWrapPlanPreview(
                false,
                sourcePath,
                folderName,
                targetFolder,
                fileCollision.TargetPath,
                Localizer.Format("PlanPreviewTargetExistsFormat", fileCollision.TargetPath));
        }

        return new FolderWrapPlanPreview(true, sourcePath, folderName, targetFolder, fileCollision.TargetPath, null);
    }

    public static OperationResult WrapFiles(
        IEnumerable<string> paths,
        FileToolsSettings settings,
        IReadOnlyDictionary<string, string>? targetFolderNames = null)
    {
        var result = new OperationResult();
        foreach (var path in NormalizeFilePaths(paths))
        {
            result.AddCandidate();
            try
            {
                var targetFolderName = TryGetTargetFolderName(targetFolderNames, path);
                var preview = CreatePreview(path, settings, targetFolderName);
                if (!preview.IsReady ||
                    string.IsNullOrWhiteSpace(preview.TargetFolderPath) ||
                    string.IsNullOrWhiteSpace(preview.TargetFilePath))
                {
                    result.AddSkipped(preview.FailureReason ?? Localizer.Get("PlanPreviewUnavailable"));
                    continue;
                }

                Directory.CreateDirectory(preview.TargetFolderPath);
                File.Move(path, preview.TargetFilePath);
                result.AddApplied(Path.GetFileName(path) + " -> " + Path.GetFileName(preview.TargetFolderPath) + "\\");
                FileToolsEnvironment.Log("WRAP", path + " -> " + preview.TargetFilePath);
            }
            catch (Exception ex)
            {
                result.AddError(path + " | " + ex.Message);
            }
        }

        return result;
    }

    private static string? TryGetTargetFolderName(IReadOnlyDictionary<string, string>? targetFolderNames, string path)
    {
        if (targetFolderNames is null)
        {
            return null;
        }

        return targetFolderNames.TryGetValue(path, out var value) ||
               targetFolderNames.TryGetValue(Path.GetFullPath(path), out value)
            ? value
            : null;
    }

    private static string[] NormalizeFilePaths(IEnumerable<string> paths)
    {
        return paths
            .Select(static path => path.Trim().Trim('"'))
            .Where(static path => path.Length > 0 && File.Exists(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray();
    }
}
