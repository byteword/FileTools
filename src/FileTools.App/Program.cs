using System.Text;
using System.Windows.Forms;

namespace FileTools;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            ApplicationConfiguration.Initialize();

            if (args.Length > 0)
            {
                var verb = args[0].Trim().ToLowerInvariant();
                if (verb is "/install" or "--install")
                {
                    InstallContextMenu();
                    return;
                }

                if (verb is "/uninstall" or "--uninstall")
                {
                    UninstallContextMenu();
                    return;
                }

                if (verb is "/context" or "--context")
                {
                    RunFromContextMenu(args.Skip(1).ToArray());
                    return;
                }

                if (verb is "/open" or "--open")
                {
                    OpenFromContextMenu(args.Skip(1).ToArray());
                    return;
                }

                if (verb is "/run" or "--run")
                {
                    RunLegacyContextMenu(args.Skip(1).ToArray());
                    return;
                }
            }

            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            FileToolsEnvironment.Log("FATAL", ex.ToString());
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void InstallContextMenu()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            throw new InvalidOperationException(Localizer.Get("CannotLocateExecutable"));
        }

        var settings = SettingsStore.Load();
        settings.RegisterContextMenu = true;

        var installedPath = ContextMenuRegistrar.Install(exe, settings);
        SettingsStore.Save(settings);

        MessageBox.Show(
            Localizer.Format("ContextMenuInstalledDialogFormat", installedPath),
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void UninstallContextMenu()
    {
        ContextMenuRegistrar.Uninstall();

        var settings = SettingsStore.Load();
        settings.RegisterContextMenu = false;
        SettingsStore.Save(settings);

        MessageBox.Show(
            Localizer.Get("ContextMenuRemoved"),
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void RunFromContextMenu(string[] args)
    {
        if (args.Length < 2 || !ContextMenuCommandLine.TryParseCommand(args[0], out var command))
        {
            FileToolsEnvironment.Log("CONTEXT", "Invalid arguments: " + string.Join(" | ", args));
            return;
        }

        var paths = GetExistingPaths(args.Skip(1));
        if (paths.Length == 0)
        {
            FileToolsEnvironment.Log("CONTEXT", "No valid paths: " + string.Join(" | ", args.Skip(1)));
            return;
        }

        Directory.CreateDirectory(FileToolsEnvironment.QueueDir);
        var queueFile = Path.Combine(FileToolsEnvironment.QueueDir, command + ".txt");
        foreach (var path in paths)
        {
            AppendQueue(queueFile, path);
        }

        using var mutex = new Mutex(initiallyOwned: false, name: "Local\\" + FileToolsEnvironment.AppName + "_" + command);
        if (!mutex.WaitOne(0))
        {
            return;
        }

        try
        {
            Thread.Sleep(1300);
            var queuedPaths = ReadAndClearQueue(queueFile);
            var result = ExecuteContextCommand(command, queuedPaths);
            if (result is null)
            {
                return;
            }

            FileToolsEnvironment.Log(
                "CONTEXT",
                $"{command}: target={result.CandidateCount}, applied={result.AppliedCount}, skipped={result.SkippedCount}, errors={result.Errors.Count}");

            if (result.HasErrors)
            {
                MessageBox.Show(
                    result.ToUserMessage(ToolModeText.GetDisplayName(command)),
                    FileToolsEnvironment.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch
            {
            }
        }
    }

    private static OperationResult? ExecuteContextCommand(ContextMenuCommand command, IReadOnlyList<string> paths)
    {
        if (command == ContextMenuCommand.OpenApp)
        {
            Application.Run(new MainForm(paths));
            return null;
        }

        if (command == ContextMenuCommand.FileCompare)
        {
            Application.Run(new MainForm(paths, MainFormStartupAction.OpenFileCompare));
            return null;
        }

        var settings = SettingsStore.Load();
        if (command == ContextMenuCommand.FileNameCorrection)
        {
            return RenameReviewDialog.ShowAndApply(paths, settings);
        }

        if (command == ContextMenuCommand.FolderMergeSelectedTargets)
        {
            var preview = FolderMergeOperations.CreateMergePlanPreview(paths, settings);
            if (!preview.IsReady)
            {
                var result = new OperationResult();
                if (!string.IsNullOrWhiteSpace(preview.FailureReason))
                {
                    result.AddSkipped(preview.FailureReason);
                }

                return result;
            }

            var allowFolderContentsMode = paths.Count(path => Directory.Exists(path)) >= 2;
            using var optionsDialog = new FolderMergeOptionsDialog(
                paths,
                settings,
                new FolderMergeOptions(preview.TargetFolderName, FolderMergeMode.MergeFolderUnits),
                allowFolderContentsMode);
            if (optionsDialog.ShowDialog() != DialogResult.OK)
            {
                var canceled = new OperationResult();
                canceled.AddSkipped(Localizer.Get("FolderMergeCanceled"));
                return canceled;
            }

            var options = optionsDialog.ResultOptions;
            preview = FolderMergeOperations.CreateMergePlanPreview(paths, settings, options);
            if (!preview.IsReady)
            {
                var unavailable = new OperationResult();
                if (!string.IsNullOrWhiteSpace(preview.FailureReason))
                {
                    unavailable.AddSkipped(preview.FailureReason);
                }

                return unavailable;
            }

            var confirmation = MessageBox.Show(
                BuildFolderMergeContextConfirmationMessage(preview, options),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.OK)
            {
                var result = new OperationResult();
                result.AddSkipped(Localizer.Get("FolderMergeCanceled"));
                return result;
            }

            return FolderMergeOperations.MergeIntoFolder(paths, settings, options).OperationResult;
        }

        if (command is ContextMenuCommand.ArchiveMergeGroupByArchiveName or ContextMenuCommand.ArchiveMergePreserveInternalPaths)
        {
            return ExecuteArchiveMergeContextCommand(command, paths, settings);
        }

        var mode = command switch
        {
            ContextMenuCommand.FileNameCorrection => ToolMode.FileNameCorrection,
            ContextMenuCommand.FolderStructure => ToolMode.FolderStructure,
            ContextMenuCommand.FolderWrapFiles => ToolMode.FolderStructure,
            ContextMenuCommand.FolderUnwrapSameNameSingleFile => ToolMode.FolderStructure,
            ContextMenuCommand.FolderUnwrapSingleFile => ToolMode.FolderStructure,
            ContextMenuCommand.FolderUnwrapUseFolderName => ToolMode.FolderStructure,
            ContextMenuCommand.FolderUnwrapKeepFileName => ToolMode.FolderStructure,
            ContextMenuCommand.FolderUnwrapPrefixFolderName => ToolMode.FolderStructure,
            ContextMenuCommand.FolderMoveInnerFilesUp => ToolMode.FolderStructure,
            ContextMenuCommand.AutoRelocation => ToolMode.AutoRelocation,
            ContextMenuCommand.AutoRelocationCurrentFolder => ToolMode.AutoRelocation,
            ContextMenuCommand.AutoRelocationChooseTarget => ToolMode.AutoRelocation,
            _ => ToolMode.FileNameCorrection
        };

        switch (command)
        {
            case ContextMenuCommand.FolderWrapFiles:
                settings.FolderStructureOperation = FolderStructureOperation.WrapFiles;
                break;
            case ContextMenuCommand.FolderUnwrapSameNameSingleFile:
                settings.FolderStructureOperation = FolderStructureOperation.UnwrapSameNameSingleFile;
                break;
            case ContextMenuCommand.FolderUnwrapSingleFile:
                settings.FolderStructureOperation = FolderStructureOperation.UnwrapSingleFileFolder;
                break;
            case ContextMenuCommand.FolderUnwrapUseFolderName:
                settings.FolderStructureOperation = FolderStructureOperation.UnwrapSingleFileFolder;
                settings.FolderUnwrapNameMismatchMode = FolderUnwrapNameMismatchMode.UseFolderName;
                break;
            case ContextMenuCommand.FolderUnwrapKeepFileName:
                settings.FolderStructureOperation = FolderStructureOperation.UnwrapSingleFileFolder;
                settings.FolderUnwrapNameMismatchMode = FolderUnwrapNameMismatchMode.KeepFileName;
                break;
            case ContextMenuCommand.FolderUnwrapPrefixFolderName:
                settings.FolderStructureOperation = FolderStructureOperation.UnwrapSingleFileFolder;
                settings.FolderUnwrapNameMismatchMode = FolderUnwrapNameMismatchMode.PrefixFolderName;
                break;
            case ContextMenuCommand.FolderMoveInnerFilesUp:
                settings.FolderStructureOperation = FolderStructureOperation.MoveInnerFilesUp;
                break;
            case ContextMenuCommand.AutoRelocationCurrentFolder:
                settings.AutoRelocationTargetRootPath = null;
                break;
            case ContextMenuCommand.AutoRelocationChooseTarget:
                var targetRoot = ChooseRelocationTargetRoot();
                if (string.IsNullOrWhiteSpace(targetRoot))
                {
                    return null;
                }

                settings.AutoRelocationTargetRootPath = targetRoot;
                break;
        }

        return new FileToolRunner(settings).Run(mode, paths);
    }

    private static OperationResult ExecuteArchiveMergeContextCommand(
        ContextMenuCommand command,
        IReadOnlyList<string> paths,
        FileToolsSettings settings)
    {
        var layout = command == ContextMenuCommand.ArchiveMergePreserveInternalPaths
            ? ArchiveMergeLayout.PreserveInternalPaths
            : ArchiveMergeLayout.GroupByArchiveName;
        var options = ArchiveMergeOperations.CreateDefaultOptions(paths, settings, layout);
        if (options is null)
        {
            var result = new OperationResult();
            result.AddSkipped(Localizer.Get("ArchiveMergeNeedsMultipleArchives"));
            return result;
        }

        if (settings.ArchiveMergeOutputNamePolicy == ArchiveMergeOutputNamePolicy.Manual)
        {
            var outputPath = ChooseArchiveMergeOutputPath(options.OutputPath);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                var result = new OperationResult();
                result.AddSkipped(Localizer.Get("ArchiveMergeCanceled"));
                return result;
            }

            options.OutputPath = outputPath;
        }

        return ArchiveMergeProgressDialog.Run(owner: null, options) ?? new OperationResult();
    }

    private static string? ChooseArchiveMergeOutputPath(string suggestedPath)
    {
        using var dialog = new SaveFileDialog
        {
            Title = Localizer.Get("ArchiveMergeOutputDialogTitle"),
            Filter = Localizer.Get("ArchiveMergeOutputDialogFilter"),
            FileName = Path.GetFileName(suggestedPath)
        };

        var initialDirectory = Path.GetDirectoryName(suggestedPath);
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    private static string? ChooseRelocationTargetRoot()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = Localizer.Get("ManualTargetRootDialogDescription"),
            UseDescriptionForTitle = true
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static string BuildFolderMergeContextConfirmationMessage(
        FolderMergePlanPreview preview,
        FolderMergeOptions options)
    {
        var message = new StringBuilder();
        message.AppendLine(Localizer.Format("FolderMergeConfirmFormat", preview.SourcePaths.Count, preview.TargetFolderPath));
        message.AppendLine(
            Localizer.Format(
                "FolderMergeModeLabelFormat",
                options.Mode == FolderMergeMode.MergeFolderContentsOnly
                    ? Localizer.Get("FolderMergeModeMergeContentsOnly")
                    : Localizer.Get("FolderMergeModeMergeFolders")));
        if (!string.IsNullOrWhiteSpace(preview.TargetParentPath))
        {
            message.AppendLine(Localizer.Format("FolderMergeTargetParentFormat", preview.TargetParentPath));
        }

        if (preview.HasMultipleParents)
        {
            message.AppendLine(Localizer.Get("FolderMergeMultiParentWarning"));
        }

        message.AppendLine(Localizer.Get("FolderMergeSelectedSourcesHeader"));
        for (var i = 0; i < preview.SourcePaths.Count; i++)
        {
            message.AppendLine($"{i + 1}. {preview.SourcePaths[i]}");
        }

        return message.ToString().TrimEnd();
    }

    private static void OpenFromContextMenu(string[] args)
    {
        if (args.Length == 0)
        {
            Application.Run(new MainForm());
            return;
        }

        var paths = GetExistingPaths(args);
        if (paths.Length == 0)
        {
            Application.Run(new MainForm());
            return;
        }

        Directory.CreateDirectory(FileToolsEnvironment.QueueDir);
        var queueFile = Path.Combine(FileToolsEnvironment.QueueDir, "Open.txt");
        foreach (var path in paths)
        {
            AppendQueue(queueFile, path);
        }

        using var mutex = new Mutex(initiallyOwned: false, name: "Local\\" + FileToolsEnvironment.AppName + "_Open");
        if (!mutex.WaitOne(0))
        {
            return;
        }

        try
        {
            Thread.Sleep(1300);
            var queuedPaths = ReadAndClearQueue(queueFile);
            Application.Run(new MainForm(queuedPaths));
        }
        finally
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch
            {
            }
        }
    }

    private static string[] GetExistingPaths(IEnumerable<string> args)
    {
        return args
            .Select(static arg => arg.Trim('"'))
            .Where(static path => File.Exists(path) || Directory.Exists(path))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
    }

    private static void RunLegacyContextMenu(string[] args)
    {
        if (args.Length < 2)
        {
            return;
        }

        var legacyMode = args[0];
        var path = args[1];
        var command = legacyMode switch
        {
            "SameNameSingleFile" => ContextMenuCommand.FolderUnwrapSameNameSingleFile,
            "SingleFileFolder" => ContextMenuCommand.FolderUnwrapSingleFile,
            "MoveAllInnerFiles" => ContextMenuCommand.FolderMoveInnerFilesUp,
            _ => ContextMenuCommand.FolderStructure
        };

        RunFromContextMenu([command.ToString(), path]);
    }

    private static void AppendQueue(string queueFile, string path)
    {
        for (var i = 0; i < 10; i++)
        {
            try
            {
                File.AppendAllText(queueFile, path + Environment.NewLine, Encoding.UTF8);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(60);
            }
        }

        FileToolsEnvironment.Log("QUEUE", "Failed to append: " + path);
    }

    private static List<string> ReadAndClearQueue(string queueFile)
    {
        for (var i = 0; i < 10; i++)
        {
            try
            {
                if (!File.Exists(queueFile))
                {
                    return [];
                }

                var lines = File.ReadAllLines(queueFile, Encoding.UTF8)
                    .Select(static x => x.Trim())
                    .Where(static x => x.Length > 0 && (File.Exists(x) || Directory.Exists(x)))
                    .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                    .ToList();

                File.Delete(queueFile);
                return lines;
            }
            catch (IOException)
            {
                Thread.Sleep(60);
            }
        }

        return [];
    }
}
