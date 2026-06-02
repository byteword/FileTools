using System.Drawing;
using System.Runtime.InteropServices;

namespace FileTools;

internal static class FileSystemIconProvider
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;

    private static readonly Dictionary<string, Image> IconCache = new(StringComparer.OrdinalIgnoreCase);

    public static Image GetSmallIcon(string path, bool isDirectory)
    {
        var key = isDirectory ? "<folder>" : Path.GetFullPath(path);
        if (IconCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var icon = OperatingSystem.IsWindows()
            ? LoadShellIcon(path, isDirectory)
            : SystemIcons.Application.ToBitmap();
        IconCache[key] = icon;
        return icon;
    }

    private static Image LoadShellIcon(string path, bool isDirectory)
    {
        var attributes = isDirectory ? FileAttributeDirectory : FileAttributeNormal;
        var flags = ShgfiIcon | ShgfiSmallIcon;
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            flags |= ShgfiUseFileAttributes;
        }

        var result = SHGetFileInfo(path, attributes, out var info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return SystemIcons.Application.ToBitmap();
        }

        try
        {
            using var icon = (Icon)Icon.FromHandle(info.hIcon).Clone();
            return icon.ToBitmap();
        }
        finally
        {
            _ = DestroyIcon(info.hIcon);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
