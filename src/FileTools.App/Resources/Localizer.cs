using System.Globalization;
using System.Resources;

namespace FileTools;

internal static class Localizer
{
    private static readonly ResourceManager ResourceManager = new(
        "FileTools.Resources.Strings",
        typeof(Localizer).Assembly);

    public static string Get(string key)
    {
        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
            ?? ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? key;
    }

    public static string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }
}
