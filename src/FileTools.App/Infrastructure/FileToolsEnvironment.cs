using System.Text;

namespace FileTools;

internal static class FileToolsEnvironment
{
    public const string AppName = "FileTools";

    public static string AppDataDir { get; } = ResolveAppDataDir();

    public static string QueueDir { get; } = Path.Combine(
        Path.GetTempPath(),
        AppName + "_Queue");

    public static string LogPath { get; } = Path.Combine(
        Path.GetTempPath(),
        AppName + ".log");

    public static void Log(string tag, string message)
    {
        try
        {
            File.AppendAllText(
                LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {tag}: {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static string ResolveAppDataDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            var primary = Path.Combine(appData, AppName);
            try
            {
                Directory.CreateDirectory(primary);
                return primary;
            }
            catch
            {
            }
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, AppName + "Data");
        Directory.CreateDirectory(fallback);
        return fallback;
    }
}
