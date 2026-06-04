using System.Runtime.CompilerServices;

namespace FileTools.Tests;

internal sealed class TempDirectory : IDisposable
{
    private TempDirectory(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public static TempDirectory Create([CallerMemberName] string? testName = null)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "FileTools.Tests",
            (testName ?? "test") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TempDirectory(root);
    }

    public string GetPath(string relativePath)
    {
        return Path.Combine(Root, relativePath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
        }
    }
}
