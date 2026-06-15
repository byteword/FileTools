using System.Drawing;
using System.IO;
using System.Reflection;

namespace FileTools;

internal static class ApplicationIconProvider
{
    private const string IconResourceName = "FileTools.Resources.FileToolsIcon.ico";
    private static Icon? CachedIcon;

    public static Icon? GetApplicationIcon()
    {
        if (CachedIcon is null)
        {
            CachedIcon = LoadApplicationIcon();
        }

        return CachedIcon is null ? null : (Icon)CachedIcon.Clone();
    }

    public static Image? GetApplicationIconImage()
    {
        using var icon = GetApplicationIcon();
        return icon?.ToBitmap();
    }

    private static Icon? LoadApplicationIcon()
    {
        var assembly = typeof(ApplicationIconProvider).Assembly;
        using var resourceStream = assembly.GetManifestResourceStream(IconResourceName);
        if (resourceStream is not null)
        {
            using var icon = new Icon(resourceStream);
            return (Icon)icon.Clone();
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return null;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            return icon is null ? null : (Icon)icon.Clone();
        }
        catch
        {
            return null;
        }
    }
}
