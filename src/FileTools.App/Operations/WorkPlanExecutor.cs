namespace FileTools;

internal sealed class WorkPlanExecutor
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly FileToolsSettings _baseSettings;

    public WorkPlanExecutor(FileToolsSettings baseSettings)
    {
        _baseSettings = baseSettings;
    }

    public OperationResult Run(IEnumerable<WorkTargetPlan> targets)
    {
        return Run(targets, CancellationToken.None, progress: null);
    }

    public OperationResult Run(
        IEnumerable<WorkTargetPlan> targets,
        CancellationToken cancellationToken,
        IProgress<string>? progress)
    {
        var aggregate = new OperationResult();
        foreach (var target in targets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            progress?.Report(Localizer.Format("LogTargetStartingFormat", Path.GetFileName(target.Path)));
            if (!RunTarget(target, aggregate, cancellationToken, progress))
            {
                break;
            }
        }

        return aggregate;
    }

    private bool RunTarget(
        WorkTargetPlan target,
        OperationResult aggregate,
        CancellationToken cancellationToken,
        IProgress<string>? progress)
    {
        var currentPath = target.Path;
        if (target.Steps.Count == 0)
        {
            aggregate.AddSkipped(Path.GetFileName(target.Path) + " has no planned actions");
            return true;
        }

        foreach (var step in target.Steps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
            {
                aggregate.AddSkipped(currentPath + " no longer exists");
                break;
            }

            progress?.Report(Localizer.Format("LogStepStartingFormat", Path.GetFileName(currentPath), step.DisplayName));
            var predictedPath = PredictNextPath(step, currentPath);
            var result = RunStep(step, currentPath);
            aggregate.Merge(result);
            ReportStepResult(result, progress);
            if (!result.HasErrors && result.AppliedCount > 0 && !string.IsNullOrWhiteSpace(predictedPath) &&
                (File.Exists(predictedPath) || Directory.Exists(predictedPath)))
            {
                currentPath = predictedPath;
            }
        }

        return true;
    }

    private static void ReportStepResult(OperationResult result, IProgress<string>? progress)
    {
        if (progress is null)
        {
            return;
        }

        foreach (var error in result.Errors)
        {
            progress.Report(Localizer.Format("LogErrorFormat", error));
        }

        foreach (var message in result.Messages)
        {
            progress.Report(message);
        }
    }

    private OperationResult RunStep(WorkPlanStep step, string path)
    {
        var settings = _baseSettings.Clone();
        var runner = new FileToolRunner(settings);

        switch (step.Kind)
        {
            case WorkPlanStepKind.FileNameCorrection:
                if (!string.IsNullOrWhiteSpace(step.ManualRenameFileName))
                {
                    return RenameOperations.Apply([RenameOperations.CreateManualPreview(
                        path,
                        step.ManualRenameFileName,
                        settings)]);
                }

                return runner.Run(ToolMode.FileNameCorrection, [path]);
            case WorkPlanStepKind.FolderWrap:
                settings.FolderStructureOperation = FolderStructureOperation.WrapFiles;
                return runner.Run(ToolMode.FolderStructure, [path]);
            case WorkPlanStepKind.FolderUnwrap:
                settings.FolderStructureOperation = step.FolderOperation;
                settings.FolderUnwrapNameMismatchMode = step.FolderUnwrapNameMismatchMode;
                return runner.Run(ToolMode.FolderStructure, [path]);
            case WorkPlanStepKind.AutoRelocation:
                settings.AutoRelocationTemplateId = string.IsNullOrWhiteSpace(step.AutoRelocationTemplateId)
                    ? settings.AutoRelocationTemplateId
                    : step.AutoRelocationTemplateId;
                settings.AutoRelocationTargetRootPath = string.IsNullOrWhiteSpace(step.ManualTargetRootPath)
                    ? null
                    : step.ManualTargetRootPath;
                return runner.Run(ToolMode.AutoRelocation, [path]);
            default:
                return new OperationResult();
        }
    }

    private string? PredictNextPath(WorkPlanStep step, string path)
    {
        return step.Kind switch
        {
            WorkPlanStepKind.FileNameCorrection => PredictRenamePath(step, path),
            WorkPlanStepKind.FolderWrap => PredictWrapPath(path),
            WorkPlanStepKind.FolderUnwrap => PredictUnwrapPath(
                path,
                step.FolderOperation,
                step.FolderUnwrapNameMismatchMode),
            WorkPlanStepKind.AutoRelocation => PredictAutoRelocationPath(step, path),
            _ => null
        };
    }

    private string? PredictRenamePath(WorkPlanStep step, string path)
    {
        try
        {
            var preview = !string.IsNullOrWhiteSpace(step.ManualRenameFileName)
                ? RenameOperations.CreateManualPreview(path, step.ManualRenameFileName, _baseSettings)
                : new RenamePlanner(CreateFileNameCorrector()).CreatePlan([path]).FirstOrDefault();
            return preview is not null &&
                   !PathComparer.Equals(preview.OriginalPath, preview.SuggestedPath)
                ? preview.SuggestedPath
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? PredictWrapPath(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return null;
        }

        var folderName = WindowsFileNameSafety.MakeSafeFileName(Path.GetFileNameWithoutExtension(path));
        return Path.Combine(parent, folderName);
    }

    private string? PredictAutoRelocationPath(WorkPlanStep step, string path)
    {
        try
        {
            var templateId = string.IsNullOrWhiteSpace(step.AutoRelocationTemplateId)
                ? _baseSettings.AutoRelocationTemplateId
                : step.AutoRelocationTemplateId;
            var template = AutoRelocationTemplateStore
                .FindTemplateOrDefault(templateId)
                .Document;
            var targetRootOverride = string.IsNullOrWhiteSpace(step.ManualTargetRootPath)
                ? null
                : Path.GetFullPath(step.ManualTargetRootPath);
            var context = CreateRelocationContext(path, targetRootOverride);
            if (context is null)
            {
                return null;
            }

            var plan = new AutoRelocationPlanBuilder()
                .Build(context.RootFolder, template, [context.Context]);
            var item = plan.Items.FirstOrDefault();
            if (item is null || item.RequiresReview)
            {
                return null;
            }

            var targetPath = CreateUniqueTargetPath(item.TargetPath);
            if (PathComparer.Equals(Path.GetFullPath(path), targetPath) ||
                IsSubPathOf(targetPath, path))
            {
                return null;
            }

            return targetPath;
        }
        catch
        {
            return null;
        }
    }

    private static string? PredictUnwrapPath(
        string path,
        FolderStructureOperation operation,
        FolderUnwrapNameMismatchMode mismatchMode)
    {
        if (!Directory.Exists(path))
        {
            return null;
        }

        var dir = new DirectoryInfo(path);
        if (dir.Parent is null)
        {
            return null;
        }

        if (operation == FolderStructureOperation.MoveInnerFilesUp)
        {
            return dir.Parent.FullName;
        }

        var files = dir.GetFiles("*", SearchOption.TopDirectoryOnly);
        var subDirs = dir.GetDirectories("*", SearchOption.TopDirectoryOnly);
        if (files.Length != 1 || subDirs.Length != 0)
        {
            return null;
        }

        if (operation == FolderStructureOperation.UnwrapSameNameSingleFile &&
            !string.Equals(Path.GetFileNameWithoutExtension(files[0].Name), dir.Name, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.Combine(dir.Parent.FullName, ResolveUnwrappedFileName(dir.Name, files[0].Name, mismatchMode));
    }

    private static string ResolveUnwrappedFileName(
        string folderName,
        string fileName,
        FolderUnwrapNameMismatchMode mismatchMode)
    {
        var fileStem = Path.GetFileNameWithoutExtension(fileName);
        if (string.Equals(folderName, fileStem, StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        return mismatchMode switch
        {
            FolderUnwrapNameMismatchMode.UseFolderName =>
                WindowsFileNameSafety.MakeSafeFileName(folderName + extension),
            FolderUnwrapNameMismatchMode.PrefixFolderName =>
                WindowsFileNameSafety.MakeSafeFileName(folderName + "-" + fileStem + extension),
            _ => fileName
        };
    }

    private RelocationContextWithRoot? CreateRelocationContext(string path, string? targetRootOverride)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return null;
        }

        var corrector = CreateFileNameCorrector();
        var preview = corrector.CreatePreview(path);
        var fileNameStem = GetRelocationFileNameStem(path);
        var knownFileKind = AutoRelocationFileTypeClassifier.GetKnownFileKind(path, _baseSettings);
        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fileName"] = Path.GetFileName(path),
            ["fileNameStem"] = fileNameStem,
            ["fileExtension"] = GetRelocationFileExtension(path),
            ["knownFileKind"] = knownFileKind,
            ["fileKind"] = knownFileKind,
            ["fileType"] = knownFileKind,
            ["title"] = preview.Parts.Title,
            ["originalTitle"] = fileNameStem,
            ["episodeRange"] = preview.Parts.EpisodeRange
        };

        var info = File.Exists(path)
            ? new FileInfo(path) as FileSystemInfo
            : new DirectoryInfo(path);
        var sizeBytes = File.Exists(path) ? new FileInfo(path).Length : 0;

        return new RelocationContextWithRoot(
            targetRootOverride ?? parent,
            new AutoRelocationItemContext(
                path,
                properties,
                sizeBytes,
                ImageCount: null,
                info.LastWriteTime,
                info.CreationTime));
    }

    private static string GetRelocationFileNameStem(string path)
    {
        return Directory.Exists(path)
            ? Path.GetFileName(path)
            : Path.GetFileNameWithoutExtension(path);
    }

    private static string GetRelocationFileExtension(string path)
    {
        return File.Exists(path) ? Path.GetExtension(path).TrimStart('.') : "";
    }

    private static string CreateUniqueTargetPath(string targetPath)
    {
        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            return targetPath;
        }

        var directory = Path.GetDirectoryName(targetPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return targetPath;
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

    private sealed record RelocationContextWithRoot(
        string RootFolder,
        AutoRelocationItemContext Context);

    private KoreanFileNameCorrector CreateFileNameCorrector()
    {
        var dictionary = RenameDictionaryStore.Load();
        var parserProfile = RenameParserProfileStore.Load();
        var rules = RenameRuleStore.Load();
        return new KoreanFileNameCorrector(new CorrectionOptions
        {
            ParserProfile = parserProfile,
            RenameDictionary = _baseSettings.RenameUseDictionary ? dictionary.Replacements : [],
            CommonPhrases = _baseSettings.RenameUseDictionary ? dictionary.CommonPhrases.ToArray() : [],
            Rules = rules.Rules
        });
    }
}
