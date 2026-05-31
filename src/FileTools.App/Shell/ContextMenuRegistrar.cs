using Microsoft.Win32;

namespace FileTools;

internal static class ContextMenuRegistrar
{
    private static readonly (string KeyName, ToolMode Mode)[] Menus =
    [
        ("FileTools_NameCorrection", ToolMode.FileNameCorrection),
        ("FileTools_FolderStructure", ToolMode.FolderStructure),
        ("FileTools_AutoRelocation", ToolMode.AutoRelocation)
    ];

    private static readonly string[] BaseKeys =
    [
        @"Software\Classes\*\shell",
        @"Software\Classes\Directory\shell"
    ];

    private static readonly string[] LegacyKeys =
    [
        "FolderUnwrap_SameName",
        "FolderUnwrap_SingleFile",
        "FolderUnwrap_MoveAll"
    ];

    public static string Install(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new InvalidOperationException("실행 파일 경로를 찾을 수 없습니다.");
        }

        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        var installedPath = Path.Combine(FileToolsEnvironment.AppDataDir, "FileTools.exe");
        CopyRuntimeFiles(executablePath, installedPath);

        foreach (var baseKey in BaseKeys)
        {
            foreach (var menu in Menus)
            {
                CreateMenu(baseKey, menu.KeyName, ToolModeText.GetDisplayName(menu.Mode), installedPath, menu.Mode);
            }
        }

        RemoveLegacyFolderUnwrapKeys();
        return installedPath;
    }

    public static void Uninstall()
    {
        foreach (var baseKey in BaseKeys)
        {
            foreach (var menu in Menus)
            {
                DeleteMenu(baseKey, menu.KeyName);
            }
        }

        RemoveLegacyFolderUnwrapKeys();
    }

    private static void CreateMenu(
        string baseKey,
        string keyName,
        string menuText,
        string exePath,
        ToolMode mode)
    {
        using var key = Registry.CurrentUser.CreateSubKey(baseKey + "\\" + keyName);
        if (key is null)
        {
            throw new InvalidOperationException("레지스트리 키 생성 실패: " + baseKey + "\\" + keyName);
        }

        key.SetValue("", menuText, RegistryValueKind.String);
        key.SetValue("Icon", exePath, RegistryValueKind.String);
        key.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);

        using var cmd = key.CreateSubKey("command");
        if (cmd is null)
        {
            throw new InvalidOperationException("레지스트리 command 키 생성 실패: " + keyName);
        }

        cmd.SetValue("", $"\"{exePath}\" /context {mode} \"%1\"", RegistryValueKind.String);
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

    private static void RemoveLegacyFolderUnwrapKeys()
    {
        const string legacyBaseKey = @"Software\Classes\Directory\shell";
        foreach (var key in LegacyKeys)
        {
            DeleteMenu(legacyBaseKey, key);
        }
    }
}
