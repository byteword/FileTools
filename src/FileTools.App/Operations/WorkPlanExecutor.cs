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
        var aggregate = new OperationResult();
        foreach (var target in targets)
        {
            RunTarget(target, aggregate);
        }

        return aggregate;
    }

    private void RunTarget(WorkTargetPlan target, OperationResult aggregate)
    {
        var currentPath = target.Path;
        if (target.Steps.Count == 0)
        {
            aggregate.AddSkipped(Path.GetFileName(target.Path) + " has no planned actions");
            return;
        }

        foreach (var step in target.Steps)
        {
            if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
            {
                aggregate.AddSkipped(currentPath + " no longer exists");
                break;
            }

            var predictedPath = PredictNextPath(step, currentPath);
            var result = RunStep(step, currentPath);
            aggregate.Merge(result);
            if (!result.HasErrors && !string.IsNullOrWhiteSpace(predictedPath) &&
                (File.Exists(predictedPath) || Directory.Exists(predictedPath)))
            {
                currentPath = predictedPath;
            }
        }
    }

    private OperationResult RunStep(WorkPlanStep step, string path)
    {
        var settings = _baseSettings.Clone();
        var runner = new FileToolRunner(settings);

        switch (step.Kind)
        {
            case WorkPlanStepKind.FileNameCorrection:
                return runner.Run(ToolMode.FileNameCorrection, [path]);
            case WorkPlanStepKind.FolderWrap:
                settings.FolderStructureOperation = FolderStructureOperation.WrapFiles;
                return runner.Run(ToolMode.FolderStructure, [path]);
            case WorkPlanStepKind.FolderUnwrap:
                settings.FolderStructureOperation = step.FolderOperation;
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

    private static string? PredictNextPath(WorkPlanStep step, string path)
    {
        return step.Kind switch
        {
            WorkPlanStepKind.FileNameCorrection => PredictRenamePath(path),
            WorkPlanStepKind.FolderWrap => PredictWrapPath(path),
            WorkPlanStepKind.FolderUnwrap => PredictUnwrapPath(path, step.FolderOperation),
            _ => null
        };
    }

    private static string? PredictRenamePath(string path)
    {
        try
        {
            var preview = new RenamePlanner().CreatePlan([path]).FirstOrDefault();
            return preview is { Status: RenamePreviewStatus.Ready or RenamePreviewStatus.Conflict } &&
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
        return Path.Combine(parent, folderName, Path.GetFileName(path));
    }

    private static string? PredictUnwrapPath(string path, FolderStructureOperation operation)
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

        return Path.Combine(dir.Parent.FullName, files[0].Name);
    }
}
