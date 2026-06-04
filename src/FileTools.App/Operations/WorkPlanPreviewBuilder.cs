namespace FileTools;

internal sealed record WorkPlanStepPreview(
    int Number,
    WorkPlanStep Step,
    string PreviewText,
    string ToolTipText,
    bool HasWarning);

internal sealed class WorkPlanPreviewBuilder
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly FileToolsSettings _settings;

    public WorkPlanPreviewBuilder(FileToolsSettings settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<WorkPlanStepPreview> Build(WorkTargetPlan target)
    {
        var state = PreviewPathState.FromPath(target.Path);
        var previews = new List<WorkPlanStepPreview>();

        for (var index = 0; index < target.Steps.Count; index++)
        {
            var result = BuildStepPreview(index + 1, target.Steps[index], state);
            previews.Add(result.Preview);
            if (result.NextState is not null)
            {
                state = result.NextState;
            }
        }

        return previews;
    }

    private PreviewBuildResult BuildStepPreview(int number, WorkPlanStep step, PreviewPathState state)
    {
        if (state.Kind == PreviewPathKind.Unknown && !PathExists(state.Path))
        {
            return CreateWarning(number, step, state, Localizer.Format("PlanPreviewInputMissingFormat", state.Path));
        }

        return step.Kind switch
        {
            WorkPlanStepKind.FileNameCorrection => BuildRenamePreview(number, step, state),
            WorkPlanStepKind.FolderWrap => BuildWrapPreview(number, step, state),
            WorkPlanStepKind.FolderUnwrap => BuildUnwrapPreview(number, step, state),
            WorkPlanStepKind.AutoRelocation => BuildRelocationPreview(number, step, state),
            WorkPlanStepKind.ArchiveMerge => BuildArchiveMergePreview(number, step),
            _ => CreateWarning(number, step, state, Localizer.Get("PlanPreviewUnavailable"))
        };
    }

    private PreviewBuildResult BuildArchiveMergePreview(int number, WorkPlanStep step)
    {
        var options = step.ArchiveMergeOptions;
        if (options is null)
        {
            return new PreviewBuildResult(
                new WorkPlanStepPreview(number, step, Localizer.Get("ArchiveMergePlanMissingOptions"), step.DisplayName, HasWarning: true),
                NextState: null);
        }

        var missing = options.SourcePaths.FirstOrDefault(static path => !File.Exists(path));
        if (!string.IsNullOrWhiteSpace(missing))
        {
            return new PreviewBuildResult(
                new WorkPlanStepPreview(
                    number,
                    step,
                    Localizer.Format("PlanPreviewInputMissingFormat", missing),
                    CreateArchiveMergeToolTip(step, options, Localizer.Format("PlanPreviewInputMissingFormat", missing)),
                    HasWarning: true),
                NextState: null);
        }

        var preview = Localizer.Format(
            "ArchiveMergePreviewFormat",
            options.SourcePaths.Count,
            Path.GetFileName(options.OutputPath));
        return new PreviewBuildResult(
            new WorkPlanStepPreview(number, step, preview, CreateArchiveMergeToolTip(step, options, warning: ""), HasWarning: false),
            NextState: null);
    }

    private PreviewBuildResult BuildRenamePreview(int number, WorkPlanStep step, PreviewPathState state)
    {
        try
        {
            var preview = string.IsNullOrWhiteSpace(step.ManualRenameFileName)
                ? new RenamePlanner(CreateFileNameCorrector()).CreatePlan([state.Path]).FirstOrDefault()
                : CreateManualRenamePreview(state.Path, step.ManualRenameFileName);
            if (preview is null)
            {
                return CreateWarning(number, step, state, Localizer.Get("PlanPreviewUnavailable"));
            }

            var warning = preview.Status is RenamePreviewStatus.NeedsReview or RenamePreviewStatus.Conflict
                ? string.Join("; ", preview.Reasons)
                : "";
            var nextState = state with { Path = preview.SuggestedPath };
            return CreateResult(number, step, state, preview.SuggestedPath, nextState, warning);
        }
        catch (Exception ex)
        {
            return CreateWarning(number, step, state, ex.Message);
        }
    }

    private PreviewBuildResult BuildWrapPreview(int number, WorkPlanStep step, PreviewPathState state)
    {
        if (state.Kind != PreviewPathKind.File)
        {
            return CreateWarning(number, step, state, Localizer.Get("PlanPreviewNotFile"));
        }

        var parent = Path.GetDirectoryName(state.Path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return CreateWarning(number, step, state, Localizer.Get("PlanPreviewNoParent"));
        }

        var folderName = FolderStructureNameTemplates.ResolveWrapFolderName(state.Path, _settings);
        var targetFolder = Path.Combine(parent, folderName);
        if (!Directory.Exists(targetFolder))
        {
            var folderCollision = NameCollisionResolver.Resolve(
                parent,
                folderName,
                FolderStructureCollisionOptions.Create(_settings, NameCollisionTargetKind.Folder));
            targetFolder = folderCollision.TargetPath;
            if (!folderCollision.IsReady)
            {
                return CreateWarning(number, step, state, Localizer.Format("PlanPreviewTargetExistsFormat", targetFolder));
            }
        }

        var fileCollision = NameCollisionResolver.Resolve(
            targetFolder,
            Path.GetFileName(state.Path),
            FolderStructureCollisionOptions.Create(_settings, NameCollisionTargetKind.File));
        if (!fileCollision.IsReady)
        {
            return CreateWarning(number, step, state, Localizer.Format("PlanPreviewTargetExistsFormat", fileCollision.TargetPath));
        }

        var nextState = new PreviewPathState(
            targetFolder,
            PreviewPathKind.Folder,
            Path.GetFileName(state.Path));
        return CreateResult(number, step, state, targetFolder, nextState);
    }

    private PreviewBuildResult BuildUnwrapPreview(int number, WorkPlanStep step, PreviewPathState state)
    {
        if (state.Kind != PreviewPathKind.Folder)
        {
            return CreateWarning(number, step, state, Localizer.Get("PlanPreviewNotFolder"));
        }

        var parent = Path.GetDirectoryName(state.Path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return CreateWarning(number, step, state, Localizer.Get("PlanPreviewNoParent"));
        }

        if (step.FolderOperation == FolderStructureOperation.MoveInnerFilesUp)
        {
            if (!CanPreviewMoveInnerFilesUp(state))
            {
                return CreateWarning(number, step, state, Localizer.Get("PlanPreviewMoveUpUnavailable"));
            }

            var moveUpState = new PreviewPathState(parent, PreviewPathKind.Folder, SingleChildFileName: null);
            return CreateResult(number, step, state, parent, moveUpState);
        }

        string? reason = null;
        var childFileName = state.SingleChildFileName;
        if (string.IsNullOrWhiteSpace(childFileName))
        {
            childFileName = TryGetSingleChildFileName(state.Path, out reason);
        }

        if (string.IsNullOrWhiteSpace(childFileName))
        {
            return CreateWarning(number, step, state, reason ?? Localizer.Get("PlanPreviewSingleFileUnavailable"));
        }

        var folderName = Path.GetFileName(state.Path);
        if (step.FolderOperation == FolderStructureOperation.UnwrapSameNameSingleFile &&
            !string.Equals(Path.GetFileNameWithoutExtension(childFileName), folderName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateWarning(number, step, state, Localizer.Format("PlanPreviewSameNameMismatchFormat", childFileName));
        }

        var targetFileName = FolderStructureNameTemplates.ResolveUnwrappedFileNameFromFolderPath(
            state.Path,
            childFileName,
            step.FolderUnwrapNameMismatchMode,
            _settings);
        var fileCollision = NameCollisionResolver.Resolve(
            parent,
            targetFileName,
            FolderStructureCollisionOptions.Create(_settings, NameCollisionTargetKind.File));
        var targetPath = fileCollision.TargetPath;
        if (!fileCollision.IsReady)
        {
            return CreateWarning(number, step, state, Localizer.Format("PlanPreviewTargetExistsFormat", targetPath));
        }

        var nextState = new PreviewPathState(targetPath, PreviewPathKind.File, SingleChildFileName: null);
        return CreateResult(number, step, state, targetPath, nextState);
    }

    private PreviewBuildResult BuildRelocationPreview(int number, WorkPlanStep step, PreviewPathState state)
    {
        try
        {
            var root = string.IsNullOrWhiteSpace(step.ManualTargetRootPath)
                ? Path.GetDirectoryName(state.Path)
                : Path.GetFullPath(step.ManualTargetRootPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return CreateWarning(number, step, state, Localizer.Get("PlanPreviewNoParent"));
            }

            var templateId = string.IsNullOrWhiteSpace(step.AutoRelocationTemplateId)
                ? _settings.AutoRelocationTemplateId
                : step.AutoRelocationTemplateId;
            var template = AutoRelocationTemplateStore
                .FindTemplateOrDefault(templateId)
                .Document;
            var context = CreateRelocationContext(state);
            var plan = new AutoRelocationPlanBuilder().Build(root, template, [context]);
            var item = plan.Items.FirstOrDefault();
            if (item is null)
            {
                return CreateWarning(number, step, state, Localizer.Get("PlanPreviewRelocationExcluded"));
            }

            if (item.RequiresReview)
            {
                return CreateWarning(number, step, state, Localizer.Get("PlanPreviewRequiresReview"));
            }

            var targetPath = CreateUniqueTargetPath(item.TargetPath);
            if (PathComparer.Equals(Path.GetFullPath(state.Path), targetPath) ||
                IsSubPathOf(targetPath, state.Path))
            {
                return CreateWarning(number, step, state, Localizer.Get("PlanPreviewNoChange"));
            }

            var nextState = state with { Path = targetPath };
            return CreateResult(number, step, state, targetPath, nextState);
        }
        catch (Exception ex)
        {
            return CreateWarning(number, step, state, ex.Message);
        }
    }

    private PreviewBuildResult CreateResult(
        int number,
        WorkPlanStep step,
        PreviewPathState state,
        string outputPath,
        PreviewPathState nextState,
        string warning = "")
    {
        var previewText = PathComparer.Equals(Path.GetFullPath(state.Path), Path.GetFullPath(outputPath))
            ? Localizer.Get("PlanPreviewNoChange")
            : FormatPathTransition(state.Path, outputPath);
        var toolTip = CreateToolTip(step, state.Path, outputPath, warning);
        return new PreviewBuildResult(
            new WorkPlanStepPreview(number, step, previewText, toolTip, !string.IsNullOrWhiteSpace(warning)),
            nextState);
    }

    private PreviewBuildResult CreateWarning(int number, WorkPlanStep step, PreviewPathState state, string warning)
    {
        var toolTip = CreateToolTip(step, state.Path, outputPath: null, warning);
        return new PreviewBuildResult(
            new WorkPlanStepPreview(number, step, warning, toolTip, HasWarning: true),
            NextState: null);
    }

    private static string CreateToolTip(WorkPlanStep step, string inputPath, string? outputPath, string warning)
    {
        var lines = new List<string>
        {
            step.DisplayName,
            Localizer.Format("PlanTooltipInputFormat", inputPath),
            Localizer.Format("PlanTooltipOptionsFormat", CreateStepOptionText(step))
        };
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            lines.Add(Localizer.Format("PlanTooltipOutputFormat", outputPath));
        }

        if (!string.IsNullOrWhiteSpace(warning))
        {
            lines.Add(Localizer.Format("PlanTooltipWarningFormat", warning));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string CreateStepOptionText(WorkPlanStep step)
    {
        if (step.Kind == WorkPlanStepKind.FileNameCorrection && !string.IsNullOrWhiteSpace(step.ManualRenameFileName))
        {
            return Localizer.Format("PlanOptionManualRenameFormat", step.ManualRenameFileName);
        }

        if (step.Kind == WorkPlanStepKind.ArchiveMerge && step.ArchiveMergeOptions is { } options)
        {
            return ArchiveMergeOperations.DescribeOptions(options);
        }

        return step.DisplayName;
    }

    private static string CreateArchiveMergeToolTip(WorkPlanStep step, ArchiveMergeOptions options, string warning)
    {
        var lines = new List<string>
        {
            step.DisplayName,
            Localizer.Format("PlanTooltipOptionsFormat", ArchiveMergeOperations.DescribeOptions(options)),
            Localizer.Format("PlanTooltipOutputFormat", options.OutputPath)
        };
        if (!string.IsNullOrWhiteSpace(warning))
        {
            lines.Add(Localizer.Format("PlanTooltipWarningFormat", warning));
        }

        lines.AddRange(options.SourcePaths.Take(12));
        return string.Join(Environment.NewLine, lines);
    }

    private RenamePreview CreateManualRenamePreview(string path, string fileName)
    {
        var preview = CreateFileNameCorrector().CreatePreview(path);
        var directory = Path.GetDirectoryName(preview.OriginalPath) ?? "";
        var safeFileName = WindowsFileNameSafety.MakeSafeFileName(fileName);
        return preview with
        {
            SuggestedFileName = safeFileName,
            SuggestedPath = Path.Combine(directory, safeFileName),
            Status = PathComparer.Equals(preview.OriginalFileName, safeFileName)
                ? RenamePreviewStatus.Unchanged
                : RenamePreviewStatus.Ready
        };
    }

    private AutoRelocationItemContext CreateRelocationContext(PreviewPathState state)
    {
        var fileName = Path.GetFileName(state.Path);
        var extensionWithDot = state.Kind == PreviewPathKind.File ? Path.GetExtension(fileName) : "";
        var fileNameStem = state.Kind == PreviewPathKind.Folder
            ? fileName
            : Path.GetFileNameWithoutExtension(fileName);
        var corrector = CreateFileNameCorrector();
        var parts = corrector.ParseParts(fileNameStem, extensionWithDot);
        var knownFileKind = state.Kind == PreviewPathKind.Folder
            ? "Folder"
            : AutoRelocationFileTypeClassifier.GetKnownFileKind(state.Path, _settings);
        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fileName"] = fileName,
            ["fileNameStem"] = fileNameStem,
            ["fileExtension"] = extensionWithDot.TrimStart('.'),
            ["knownFileKind"] = knownFileKind,
            ["fileKind"] = knownFileKind,
            ["fileType"] = knownFileKind,
            ["title"] = parts.Title,
            ["originalTitle"] = fileNameStem,
            ["episodeRange"] = parts.EpisodeRange
        };

        var sizeBytes = File.Exists(state.Path) ? new FileInfo(state.Path).Length : 0;
        var modifiedAt = TryGetFileSystemInfo(state.Path)?.LastWriteTime;
        var createdAt = TryGetFileSystemInfo(state.Path)?.CreationTime;
        return new AutoRelocationItemContext(
            state.Path,
            properties,
            sizeBytes,
            ImageCount: null,
            modifiedAt,
            createdAt);
    }

    private KoreanFileNameCorrector CreateFileNameCorrector()
    {
        var dictionary = RenameDictionaryStore.Load();
        var parserProfile = RenameParserProfileStore.Load();
        var candidateProfile = RenameCandidateProfileStore.Load(dictionary.CommonPhrases);
        var rules = RenameRuleStore.Load();
        return new KoreanFileNameCorrector(new CorrectionOptions
        {
            ParserProfile = parserProfile,
            RenameDictionary = _settings.RenameUseDictionary ? dictionary.Replacements : [],
            CommonPhrases = _settings.RenameUseDictionary ? dictionary.CommonPhrases.ToArray() : [],
            CandidateProfile = _settings.RenameUseDictionary ? candidateProfile : RenameCandidateProfileStore.CreateDefaultDocument(),
            Rules = rules.Rules
        });
    }

    private static bool CanPreviewMoveInnerFilesUp(PreviewPathState state)
    {
        if (!string.IsNullOrWhiteSpace(state.SingleChildFileName))
        {
            return true;
        }

        return Directory.Exists(state.Path) &&
               Directory.EnumerateFiles(state.Path, "*", SearchOption.TopDirectoryOnly).Any();
    }

    private static string? TryGetSingleChildFileName(string folderPath, out string? reason)
    {
        reason = null;
        if (!Directory.Exists(folderPath))
        {
            reason = Localizer.Get("PlanPreviewSingleFileUnavailable");
            return null;
        }

        var dir = new DirectoryInfo(folderPath);
        var files = dir.GetFiles("*", SearchOption.TopDirectoryOnly);
        var subDirs = dir.GetDirectories("*", SearchOption.TopDirectoryOnly);
        if (files.Length != 1 || subDirs.Length != 0)
        {
            reason = Localizer.Get("PlanPreviewSingleFileUnavailable");
            return null;
        }

        return files[0].Name;
    }

    private static string FormatPathTransition(string inputPath, string outputPath)
    {
        var inputName = Path.GetFileName(inputPath);
        if (string.IsNullOrWhiteSpace(inputName))
        {
            inputName = inputPath;
        }

        var outputName = Path.GetFileName(outputPath);
        if (string.IsNullOrWhiteSpace(outputName) || string.Equals(inputName, outputName, StringComparison.OrdinalIgnoreCase))
        {
            outputName = outputPath;
        }

        return Localizer.Format("PlanPreviewTransitionFormat", inputName, outputName);
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

    private static bool PathExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private static FileSystemInfo? TryGetFileSystemInfo(string path)
    {
        if (File.Exists(path))
        {
            return new FileInfo(path);
        }

        return Directory.Exists(path) ? new DirectoryInfo(path) : null;
    }

    private sealed record PreviewBuildResult(
        WorkPlanStepPreview Preview,
        PreviewPathState? NextState);

    private sealed record PreviewPathState(
        string Path,
        PreviewPathKind Kind,
        string? SingleChildFileName)
    {
        public static PreviewPathState FromPath(string path)
        {
            if (Directory.Exists(path))
            {
                return new PreviewPathState(path, PreviewPathKind.Folder, TryGetSingleChildFileName(path, out _));
            }

            return File.Exists(path)
                ? new PreviewPathState(path, PreviewPathKind.File, SingleChildFileName: null)
                : new PreviewPathState(path, PreviewPathKind.Unknown, SingleChildFileName: null);
        }
    }

    private enum PreviewPathKind
    {
        Unknown,
        File,
        Folder
    }
}
