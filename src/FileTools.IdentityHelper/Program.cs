using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;

namespace FileTools.IdentityHelper;

internal static class Program
{
    private const string PackageName = "byteword.FileTools";
    private const string RegistryKeyPath = @"SOFTWARE\FileTools";
    private const string InstalledValueName = "Windows11IdentityInstalled";
    private const string ThumbprintValueName = "Windows11IdentityCertificateThumbprint";

    private static int Main(string[] args)
    {
        try
        {
            var options = CommandLineOptions.Parse(args);
            return options.Command switch
            {
                "install" => Install(options),
                "uninstall" => Uninstall(),
                _ => throw new InvalidOperationException("Expected command: install or uninstall.")
            };
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Install(CommandLineOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MsixPath))
        {
            throw new InvalidOperationException("--msix is required.");
        }

        if (string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            throw new InvalidOperationException("--cert is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ExternalLocation))
        {
            throw new InvalidOperationException("--external-location is required.");
        }

        var msixPath = ResolvePath(options.MsixPath);
        var certificatePath = ResolvePath(options.CertificatePath);
        var externalLocation = Path.GetFullPath(options.ExternalLocation);

        if (!File.Exists(msixPath))
        {
            throw new FileNotFoundException("Identity MSIX was not found.", msixPath);
        }

        if (!File.Exists(certificatePath))
        {
            throw new FileNotFoundException("Signing certificate was not found.", certificatePath);
        }

        if (!Directory.Exists(externalLocation))
        {
            throw new DirectoryNotFoundException(externalLocation);
        }

        var certificate = ImportCertificate(certificatePath);
        RegisterPackage(msixPath, externalLocation);
        WriteMarker(certificate.Thumbprint);
        return 0;
    }

    private static int Uninstall()
    {
        var thumbprint = ReadMarkerThumbprint();
        RemovePackage();

        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            RemoveCertificate(thumbprint);
        }

        RemoveMarker();
        return 0;
    }

    private static X509Certificate2 ImportCertificate(string certificatePath)
    {
        var certificate = new X509Certificate2(certificatePath);
        using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        var existing = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            certificate.Thumbprint,
            validOnly: false);

        if (existing.Count == 0)
        {
            store.Add(certificate);
            Log("Imported MSIX signing certificate: " + certificate.Thumbprint);
        }
        else
        {
            Log("MSIX signing certificate already trusted: " + certificate.Thumbprint);
        }

        return certificate;
    }

    private static void RemoveCertificate(string thumbprint)
    {
        using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        var existing = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            thumbprint,
            validOnly: false);

        foreach (var certificate in existing)
        {
            store.Remove(certificate);
            Log("Removed MSIX signing certificate: " + certificate.Thumbprint);
        }
    }

    private static void RegisterPackage(string msixPath, string externalLocation)
    {
        var script = string.Join(
            Environment.NewLine,
            "$ErrorActionPreference = 'Stop'",
            "Add-AppxPackage -Path " + QuotePowerShell(msixPath) +
            " -ExternalLocation " + QuotePowerShell(externalLocation) +
            " -ForceUpdateFromAnyVersion");

        RunPowerShell(script);
        Log("Registered sparse identity package at " + externalLocation);
    }

    private static void RemovePackage()
    {
        var script = string.Join(
            Environment.NewLine,
            "$ErrorActionPreference = 'Stop'",
            "$package = Get-AppxPackage -Name " + QuotePowerShell(PackageName),
            "if ($package) { $package | Remove-AppxPackage -ErrorAction Stop }");

        RunPowerShell(script);
        Log("Removed sparse identity package if present.");
    }

    private static void RunPowerShell(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe"),
            Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start PowerShell.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(output))
        {
            Log(output.Trim());
        }

        if (process.ExitCode != 0)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Log(error.Trim());
            }

            throw new InvalidOperationException("PowerShell failed with exit code " + process.ExitCode + ".");
        }
    }

    private static void WriteMarker(string thumbprint)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
        key?.SetValue(InstalledValueName, 1, RegistryValueKind.DWord);
        key?.SetValue(ThumbprintValueName, thumbprint, RegistryValueKind.String);
    }

    private static string? ReadMarkerThumbprint()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        return key?.GetValue(ThumbprintValueName) as string;
    }

    private static void RemoveMarker()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
        key?.DeleteValue(InstalledValueName, throwOnMissingValue: false);
        key?.DeleteValue(ThumbprintValueName, throwOnMissingValue: false);
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    private static string QuotePowerShell(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "FileTools.IdentityHelper.log"),
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message + Environment.NewLine,
                Encoding.UTF8);
        }
        catch
        {
        }
    }

    private sealed record CommandLineOptions(
        string Command,
        string? MsixPath,
        string? CertificatePath,
        string? ExternalLocation)
    {
        public static CommandLineOptions Parse(string[] args)
        {
            if (args.Length == 0)
            {
                throw new InvalidOperationException("Missing command.");
            }

            var command = args[0].Trim().ToLowerInvariant();
            string? msixPath = null;
            string? certificatePath = null;
            string? externalLocation = null;

            for (var i = 1; i < args.Length; i++)
            {
                var name = args[i];
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("Missing value for " + name + ".");
                }

                var value = args[++i];
                switch (name)
                {
                    case "--msix":
                        msixPath = value;
                        break;
                    case "--cert":
                        certificatePath = value;
                        break;
                    case "--external-location":
                        externalLocation = value;
                        break;
                    default:
                        throw new InvalidOperationException("Unknown option: " + name + ".");
                }
            }

            return new CommandLineOptions(command, msixPath, certificatePath, externalLocation);
        }
    }
}
