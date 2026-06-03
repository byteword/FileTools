using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using Windows.Management.Deployment;

namespace FileTools;

internal static class Windows11NativeContextMenuRegistrar
{
    private const string PackageName = "byteword.FileTools";
    private const string RegistryKeyPath = @"SOFTWARE\FileTools";
    private const string InstalledValueName = "Windows11IdentityInstalled";
    private const string ThumbprintValueName = "Windows11IdentityCertificateThumbprint";

    public static string[] GetMissingSupportFiles(string installDirectory)
    {
        return new[]
            {
                GetIdentityMsixPath(installDirectory),
                GetIdentityCertificatePath(installDirectory)
            }
            .Where(path => !File.Exists(path))
            .ToArray();
    }

    public static void Install(string installDirectory)
    {
        var msixPath = GetIdentityMsixPath(installDirectory);
        var certificatePath = GetIdentityCertificatePath(installDirectory);
        var externalLocation = Path.GetFullPath(installDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

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
        RemovePackage();
        RegisterPackage(msixPath, externalLocation);
        WriteMarker(certificate.Thumbprint);
    }

    public static void Uninstall()
    {
        var thumbprint = ReadMarkerThumbprint();
        RemovePackage();

        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            RemoveCertificate(thumbprint);
        }

        RemoveMarker();
    }

    private static string GetIdentityMsixPath(string installDirectory)
    {
        return Path.Combine(installDirectory, "FileTools.Identity.msix");
    }

    private static string GetIdentityCertificatePath(string installDirectory)
    {
        return Path.Combine(installDirectory, "FileTools.Identity.cer");
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
        }
    }

    private static void RegisterPackage(string msixPath, string externalLocation)
    {
        var packageManager = new PackageManager();
        var options = new AddPackageOptions
        {
            ExternalLocationUri = new Uri(externalLocation)
        };
        packageManager
            .AddPackageByUriAsync(new Uri(msixPath), options)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    private static void RemovePackage()
    {
        var packageManager = new PackageManager();
        var packages = packageManager
            .FindPackagesForUser(string.Empty, PackageName)
            .ToArray();

        foreach (var package in packages)
        {
            packageManager
                .RemovePackageAsync(package.Id.FullName)
                .AsTask()
                .GetAwaiter()
                .GetResult();
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
}
