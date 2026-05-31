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

        var installedPath = ContextMenuRegistrar.Install(exe, SettingsStore.Load());
        MessageBox.Show(
            Localizer.Format("ContextMenuInstalledDialogFormat", installedPath),
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void UninstallContextMenu()
    {
        ContextMenuRegistrar.Uninstall();
        MessageBox.Show(
            Localizer.Get("ContextMenuRemoved"),
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void RunFromContextMenu(string[] args)
    {
        if (args.Length < 2 || !Enum.TryParse(args[0], ignoreCase: true, out ToolMode mode))
        {
            FileToolsEnvironment.Log("CONTEXT", "Invalid arguments: " + string.Join(" | ", args));
            return;
        }

        var path = args[1].Trim('"');
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            FileToolsEnvironment.Log("CONTEXT", "Path does not exist: " + path);
            return;
        }

        Directory.CreateDirectory(FileToolsEnvironment.QueueDir);
        var queueFile = Path.Combine(FileToolsEnvironment.QueueDir, mode + ".txt");
        AppendQueue(queueFile, path);

        using var mutex = new Mutex(initiallyOwned: false, name: "Local\\" + FileToolsEnvironment.AppName + "_" + mode);
        if (!mutex.WaitOne(0))
        {
            return;
        }

        try
        {
            Thread.Sleep(1300);
            var paths = ReadAndClearQueue(queueFile);
            var result = new FileToolRunner(SettingsStore.Load()).Run(mode, paths);
            FileToolsEnvironment.Log(
                "CONTEXT",
                $"{mode}: target={result.CandidateCount}, applied={result.AppliedCount}, skipped={result.SkippedCount}, errors={result.Errors.Count}");

            if (result.HasErrors)
            {
                MessageBox.Show(
                    result.ToUserMessage(ToolModeText.GetDisplayName(mode)),
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
        var settings = SettingsStore.Load();
        settings.FolderStructureOperation = legacyMode switch
        {
            "SameNameSingleFile" => FolderStructureOperation.UnwrapSameNameSingleFile,
            "SingleFileFolder" => FolderStructureOperation.UnwrapSingleFileFolder,
            "MoveAllInnerFiles" => FolderStructureOperation.MoveInnerFilesUp,
            _ => settings.FolderStructureOperation
        };

        SettingsStore.Save(settings);
        RunFromContextMenu([nameof(ToolMode.FolderStructure), path]);
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
