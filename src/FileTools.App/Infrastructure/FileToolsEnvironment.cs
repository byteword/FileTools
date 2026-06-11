using System.Text;

namespace FileTools;

/// <summary>앱 공용 경로/로그 유틸.</summary>
internal static class FileToolsEnvironment
{
    /// <summary>공통 앱 식별자.</summary>
    public const string AppName = "FileTools";

    public static string AppDataDir { get; } = ResolveAppDataDir();

    public static string QueueDir { get; } = Path.Combine(
        Path.GetTempPath(),
        AppName + "_Queue");

    public static string LogPath { get; } = Path.Combine(
        Path.GetTempPath(),
        AppName + ".log");

    /// <summary>
    /// 단순 텍스트 로그를 기록한다.
    /// </summary>
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

    /// <summary>
    /// %AppData%\FileTools 경로를 계산한다. 실패 시 실행 폴더 기반으로 폴백.
    /// </summary>
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
