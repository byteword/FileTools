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
        if (args.Length < 2 || !TryParseContextCommand(args[0], out var command))
        {
            FileToolsEnvironment.Log("CONTEXT", "Invalid arguments: " + string.Join(" | ", args));
            return;
        }

        var paths = args
            .Skip(1)
            .Select(static arg => arg.Trim('"'))
            .Where(static path => File.Exists(path) || Directory.Exists(path))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
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

    private static bool TryParseContextCommand(string value, out ContextMenuCommand command)
    {
        if (Enum.TryParse(value, ignoreCase: true, out command) &&
            Enum.IsDefined(command))
        {
            return true;
        }

        if (!Enum.TryParse(value, ignoreCase: true, out ToolMode mode) ||
            !Enum.IsDefined(mode))
        {
            return false;
        }

        command = mode switch
        {
            ToolMode.FileNameCorrection => ContextMenuCommand.FileNameCorrection,
            ToolMode.FolderStructure => ContextMenuCommand.FolderStructure,
            ToolMode.AutoRelocation => ContextMenuCommand.AutoRelocation,
            _ => default
        };
        return true;
    }

    private static OperationResult? ExecuteContextCommand(ContextMenuCommand command, IReadOnlyList<string> paths)
    {
        if (command == ContextMenuCommand.OpenApp)
        {
            Application.Run(new MainForm(paths));
            return null;
        }

        var settings = SettingsStore.Load();
        if (command == ContextMenuCommand.FileNameCorrection)
        {
            return RenameReviewDialog.ShowAndApply(paths, settings);
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

    private static string? ChooseRelocationTargetRoot()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = Localizer.Get("ManualTargetRootDialogDescription"),
            UseDescriptionForTitle = true
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static void OpenFromContextMenu(string[] args)
    {
        if (args.Length == 0)
        {
            Application.Run(new MainForm());
            return;
        }

        var path = args[0].Trim('"');
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            Application.Run(new MainForm());
            return;
        }

        Directory.CreateDirectory(FileToolsEnvironment.QueueDir);
        var queueFile = Path.Combine(FileToolsEnvironment.QueueDir, "Open.txt");
        AppendQueue(queueFile, path);

        using var mutex = new Mutex(initiallyOwned: false, name: "Local\\" + FileToolsEnvironment.AppName + "_Open");
        if (!mutex.WaitOne(0))
        {
            return;
        }

        try
        {
            Thread.Sleep(1300);
            var paths = ReadAndClearQueue(queueFile);
            Application.Run(new MainForm(paths));
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
