namespace FileTools;

internal sealed record FolderMergeResult(string? TargetFolderPath, OperationResult OperationResult);

internal static class FolderMergeOperations
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

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

    private static string GetPathStem(string path)
    {
        return Directory.Exists(path)
            ? Path.GetFileName(path)
            : Path.GetFileNameWithoutExtension(path);
    }

    private static string? ResolveTargetParent(IReadOnlyList<string> paths)
    {
        var firstParent = Path.GetDirectoryName(paths[0]);
        if (string.IsNullOrWhiteSpace(firstParent))
        {
            return null;
        }

        return firstParent;
    }

    private static string[] NormalizePaths(IEnumerable<string> paths)
    {
        return paths
            .Select(static path => path.Trim().Trim('"'))
            .Where(static path => path.Length > 0 && (File.Exists(path) || Directory.Exists(path)))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray();
    }

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
