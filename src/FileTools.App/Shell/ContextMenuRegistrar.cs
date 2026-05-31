using Microsoft.Win32;

namespace FileTools;

internal static class ContextMenuRegistrar
{
    private const string GroupedMenuKeyName = "FileTools";
    private const string LegacyOpenMenuKeyName = "FileTools_Open";

    private static readonly ContextMenuBaseKey[] BaseKeys =
    [
        new(@"Software\Classes\*\shell", ContextMenuTargetKind.File),
        new(@"Software\Classes\Directory\shell", ContextMenuTargetKind.Directory)
    ];

    private static readonly ContextMenuCommandDefinition[] CommandDefinitions =
    [
        new(
            "FileTools_01_NameCorrection",
            ContextMenuCommand.FileNameCorrection,
            ContextMenuTargetKind.File | ContextMenuTargetKind.Directory,
            settings => settings.ContextMenuFileNameCorrection),
        new(
            "FileTools_02_FolderWrapFiles",
            ContextMenuCommand.FolderWrapFiles,
            ContextMenuTargetKind.File,
            settings => settings.ContextMenuFolderStructure && settings.ContextMenuFolderWrapFiles),
        new(
            "FileTools_03_FolderUnwrapSameName",
            ContextMenuCommand.FolderUnwrapSameNameSingleFile,
            ContextMenuTargetKind.Directory,
            settings => settings.ContextMenuFolderStructure && settings.ContextMenuFolderUnwrapSameNameSingleFile),
        new(
            "FileTools_04_FolderUnwrapSingleFile",
            ContextMenuCommand.FolderUnwrapSingleFile,
            ContextMenuTargetKind.Directory,
            settings => settings.ContextMenuFolderStructure && settings.ContextMenuFolderUnwrapSingleFile),
        new(
            "FileTools_05_FolderMoveInnerFilesUp",
            ContextMenuCommand.FolderMoveInnerFilesUp,
            ContextMenuTargetKind.Directory,
            settings => settings.ContextMenuFolderStructure && settings.ContextMenuFolderMoveInnerFilesUp),
        new(
            "FileTools_06_AutoRelocationCurrentFolder",
            ContextMenuCommand.AutoRelocationCurrentFolder,
            ContextMenuTargetKind.File | ContextMenuTargetKind.Directory,
            settings => settings.ContextMenuAutoRelocation && settings.ContextMenuAutoRelocationCurrentFolder),
        new(
            "FileTools_07_AutoRelocationChooseTarget",
            ContextMenuCommand.AutoRelocationChooseTarget,
            ContextMenuTargetKind.File | ContextMenuTargetKind.Directory,
            settings => settings.ContextMenuAutoRelocation && settings.ContextMenuAutoRelocationChooseTarget),
        new(
            "FileTools_99_Open",
            ContextMenuCommand.OpenApp,
            ContextMenuTargetKind.File | ContextMenuTargetKind.Directory,
            settings => settings.ContextMenuOpenApp)
    ];

    private static readonly string[] LegacyKeys =
    [
        "FileTools_NameCorrection",
        "FileTools_FolderStructure",
        "FileTools_AutoRelocation",
        LegacyOpenMenuKeyName,
        "FolderUnwrap_SameName",
        "FolderUnwrap_SingleFile",
        "FolderUnwrap_MoveAll"
    ];

    public static string Install(string executablePath, FileToolsSettings settings)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new InvalidOperationException(Localizer.Get("CannotLocateExecutable"));
        }

        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        var installedPath = Path.Combine(FileToolsEnvironment.AppDataDir, "FileTools.exe");
        CopyRuntimeFiles(executablePath, installedPath);

        RemoveAllContextMenuKeys();

        if (!settings.RegisterContextMenu)
        {
            return installedPath;
        }

        foreach (var baseKey in BaseKeys)
        {
            if (settings.ContextMenuLayout == ContextMenuLayout.Expanded)
            {
                CreateExpandedMenus(baseKey, installedPath, settings);
            }
            else
            {
                CreateGroupedMenu(baseKey, installedPath, settings);
            }
        }

        return installedPath;
    }

    public static void Uninstall()
    {
        RemoveAllContextMenuKeys();
    }

    private static void CreateExpandedMenus(ContextMenuBaseKey baseKey, string exePath, FileToolsSettings settings)
    {
        foreach (var definition in GetEnabledDefinitions(baseKey.TargetKind, settings))
        {
            CreateCommandMenu(baseKey.RegistryPath, definition, exePath);
        }
    }

    private static void CreateGroupedMenu(ContextMenuBaseKey baseKey, string exePath, FileToolsSettings settings)
    {
        var enabledMenus = GetEnabledDefinitions(baseKey.TargetKind, settings).ToArray();
        if (enabledMenus.Length == 0)
        {
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(baseKey.RegistryPath + "\\" + GroupedMenuKeyName);
        if (key is null)
        {
            throw new InvalidOperationException("레지스트리 키 생성 실패: " + baseKey.RegistryPath + "\\" + GroupedMenuKeyName);
        }

        key.SetValue("", FileToolsEnvironment.AppName, RegistryValueKind.String);
        key.SetValue("MUIVerb", FileToolsEnvironment.AppName, RegistryValueKind.String);
        key.SetValue("Icon", exePath, RegistryValueKind.String);
        key.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);
        key.SetValue("SubCommands", "", RegistryValueKind.String);

        var shellBase = baseKey.RegistryPath + "\\" + GroupedMenuKeyName + @"\shell";
        foreach (var definition in enabledMenus)
        {
            CreateCommandMenu(shellBase, definition, exePath);
        }
    }

    private static IEnumerable<ContextMenuCommandDefinition> GetEnabledDefinitions(
        ContextMenuTargetKind targetKind,
        FileToolsSettings settings)
    {
        return CommandDefinitions
            .Where(definition => definition.TargetKinds.HasFlag(targetKind))
            .Where(definition => definition.IsEnabled(settings));
    }

    private static void CreateCommandMenu(
        string baseKey,
        ContextMenuCommandDefinition definition,
        string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(baseKey + "\\" + definition.KeyName);
        if (key is null)
        {
            throw new InvalidOperationException("레지스트리 키 생성 실패: " + baseKey + "\\" + definition.KeyName);
        }

        var menuText = ToolModeText.GetDisplayName(definition.Command);
        key.SetValue("", menuText, RegistryValueKind.String);
        key.SetValue("MUIVerb", menuText, RegistryValueKind.String);
        key.SetValue("Icon", exePath, RegistryValueKind.String);
        key.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);

        using var cmd = key.CreateSubKey("command");
        if (cmd is null)
        {
            throw new InvalidOperationException("레지스트리 command 키 생성 실패: " + definition.KeyName);
        }

        cmd.SetValue("", CreateCommandLine(exePath, definition.Command), RegistryValueKind.String);
    }

    private static string CreateCommandLine(string exePath, ContextMenuCommand command)
    {
        return command == ContextMenuCommand.OpenApp
            ? $"\"{exePath}\" /open \"%1\""
            : $"\"{exePath}\" /context {command} \"%1\"";
    }

    private static void CopyRuntimeFiles(string executablePath, string installedPath)
    {
        if (!string.Equals(Path.GetFullPath(executablePath), Path.GetFullPath(installedPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(executablePath, installedPath, overwrite: true);
        }

        var sourceDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return;
        }

        var installDirectory = Path.GetDirectoryName(installedPath);
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            return;
        }

        foreach (var companionFile in new[] { "FileTools.dll", "FileTools.deps.json", "FileTools.runtimeconfig.json" })
        {
            var source = Path.Combine(sourceDirectory, companionFile);
            if (!File.Exists(source))
            {
                continue;
            }

            var target = Path.Combine(installDirectory, companionFile);
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(source, target, overwrite: true);
        }
    }

    private static void DeleteMenu(string baseKey, string keyName)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(baseKey + "\\" + keyName, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            FileToolsEnvironment.Log("UNINSTALL", ex.Message);
        }
    }

    private static void RemoveAllContextMenuKeys()
    {
        foreach (var baseKey in BaseKeys)
        {
            DeleteMenu(baseKey.RegistryPath, GroupedMenuKeyName);
            foreach (var definition in CommandDefinitions)
            {
                DeleteMenu(baseKey.RegistryPath, definition.KeyName);
            }

            foreach (var keyName in LegacyKeys)
            {
                DeleteMenu(baseKey.RegistryPath, keyName);
            }
        }
    }

    [Flags]
    private enum ContextMenuTargetKind
    {
        File = 1,
        Directory = 2
    }

    private sealed record ContextMenuBaseKey(string RegistryPath, ContextMenuTargetKind TargetKind);

    private sealed record ContextMenuCommandDefinition(
        string KeyName,
        ContextMenuCommand Command,
        ContextMenuTargetKind TargetKinds,
        Func<FileToolsSettings, bool> IsEnabled);
}
