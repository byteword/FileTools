using Microsoft.Win32;
using System.Security;

namespace FileTools;

internal sealed record RegisteredFileExtension(string Extension, string Description)
{
    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Description)
            ? Extension
            : $"{Extension} - {Description}";
    }
}

internal static class SystemFileExtensionRegistry
{
    public static IReadOnlyList<RegisteredFileExtension> LoadRegisteredExtensions()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            return LoadRegisteredExtensionsCore();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            FileToolsEnvironment.Log("EXTENSIONS", ex.Message);
            return [];
        }
    }

    private static IReadOnlyList<RegisteredFileExtension> LoadRegisteredExtensionsCore()
    {
        var extensions = new List<RegisteredFileExtension>();
        using var classesRoot = Registry.ClassesRoot;
        foreach (var keyName in classesRoot.GetSubKeyNames())
        {
            var extension = AutoRelocationFileTypeClassifier.NormalizeExtension(keyName);
            if (extension.Length == 0 || !keyName.StartsWith(".", StringComparison.Ordinal))
            {
                continue;
            }

            extensions.Add(new RegisteredFileExtension(
                extension,
                GetFriendlyDescription(classesRoot, extension)));
        }

        return extensions
            .GroupBy(static item => item.Extension, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static item => item.Extension, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetFriendlyDescription(RegistryKey classesRoot, string extension)
    {
        try
        {
            using var extensionKey = classesRoot.OpenSubKey(extension);
            var progId = (extensionKey?.GetValue(null) as string)?.Trim();
            if (string.IsNullOrWhiteSpace(progId))
            {
                return "";
            }

            using var progIdKey = classesRoot.OpenSubKey(progId);
            var friendlyName = (progIdKey?.GetValue(null) as string)?.Trim();
            return string.IsNullOrWhiteSpace(friendlyName) ? progId : friendlyName;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or SecurityException)
        {
            FileToolsEnvironment.Log("EXTENSIONS", $"{extension}: {ex.Message}");
            return "";
        }
    }
}
