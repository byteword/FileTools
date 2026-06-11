using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using FileTools.Correction;

namespace FileTools;

/// <summary>
/// 런타임 플러그인 탐색/로드 레코드와 정규화 캐시.
/// </summary>
internal sealed record LoadedNameCorrectionPlugin(
    INameCorrectionPlugin Instance,
    NameCorrectionPluginDescriptor Descriptor,
    string AssemblyPath,
    AssemblyLoadContext LoadContext)
{
    public IReadOnlyList<NameCorrectionSettingDefinition> SettingDefinitions { get; init; } = [];
}

internal static class NameCorrectionPluginCatalog
{
    private static readonly object Sync = new();
    private static IReadOnlyList<LoadedNameCorrectionPlugin>? _cachedPlugins;

    /// <summary>캐시를 사용해 플러그인 리스트를 한 번만 조회한다.</summary>
    public static IReadOnlyList<LoadedNameCorrectionPlugin> Discover()
    {
        lock (Sync)
        {
            _cachedPlugins ??= DiscoverCore();
            return _cachedPlugins;
        }
    }

    /// <summary>
    /// 테스트 전용 캐시 초기화.
    /// </summary>
    internal static void ResetForTests()
    {
        lock (Sync)
        {
            _cachedPlugins = null;
        }
    }

    /// <summary>
    /// 플러그인 루트에서 후보 어셈블리를 모두 수집하고
    /// ID 중복 제거 후 표시명 순으로 정렬해 반환한다.
    /// </summary>
    private static IReadOnlyList<LoadedNameCorrectionPlugin> DiscoverCore()
    {
        var plugins = new List<LoadedNameCorrectionPlugin>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyPath in EnumeratePluginAssemblies())
        {
            if (!seenPaths.Add(Path.GetFullPath(assemblyPath)))
            {
                continue;
            }

            foreach (var plugin in LoadPluginsFromAssembly(assemblyPath))
            {
                if (seen.Add(plugin.Descriptor.Id))
                {
                    plugins.Add(plugin);
                }
            }
        }

        return plugins
            .OrderBy(static plugin => plugin.Descriptor.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 루트 폴더를 스캔해 매니페스트와 파일명 규칙에 맞는 dll 후보를 수집한다.
    /// </summary>
    private static IEnumerable<string> EnumeratePluginAssemblies()
    {
        foreach (var root in GetPluginRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in EnumerateManifestAssemblies(root))
            {
                yield return path;
            }

            IEnumerable<string> paths;
            try
            {
                paths = Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                FileToolsEnvironment.Log("RENAME-PLUGIN", $"Plugin root scan failed: {root} | {ex.Message}");
                continue;
            }

            foreach (var path in paths)
            {
                if (Path.GetFileName(path).StartsWith("FileTools.Correction.", StringComparison.OrdinalIgnoreCase) &&
                    !Path.GetFileName(path).Equals("FileTools.Correction.Abstractions.dll", StringComparison.OrdinalIgnoreCase))
                {
                    yield return path;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateManifestAssemblies(string root)
    {
        IEnumerable<string> manifests;
        try
        {
            manifests = Directory.EnumerateFiles(root, "plugin.json", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileToolsEnvironment.Log("RENAME-PLUGIN", $"Plugin manifest scan failed: {root} | {ex.Message}");
            yield break;
        }

        foreach (var manifest in manifests)
        {
            var assemblyPath = TryReadEntryAssemblyPath(manifest);
            if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
            {
                yield return assemblyPath;
            }
        }
    }

    private static string? TryReadEntryAssemblyPath(string manifestPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("entryAssembly", out var entryAssembly) ||
                entryAssembly.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = entryAssembly.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath) ?? "", value));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            FileToolsEnvironment.Log("RENAME-PLUGIN", $"Plugin manifest read failed: {manifestPath} | {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<string> GetPluginRoots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Plugins");
        yield return Path.Combine(FileToolsEnvironment.AppDataDir, "Plugins");
    }

    /// <summary>단일 어셈블리에서 플러그인 타입을 로드해 실제 실행 인스턴스를 만든다.</summary>
    private static IEnumerable<LoadedNameCorrectionPlugin> LoadPluginsFromAssembly(string assemblyPath)
    {
        Assembly assembly;
        var fullPath = Path.GetFullPath(assemblyPath);
        var loadContext = new PluginAssemblyLoadContext(fullPath);
        try
        {
            assembly = loadContext.LoadFromAssemblyPath(fullPath);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
        {
            FileToolsEnvironment.Log("RENAME-PLUGIN", $"Plugin assembly load failed: {assemblyPath} | {ex.Message}");
            yield break;
        }

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(static type => type is not null).Cast<Type>().ToArray();
            FileToolsEnvironment.Log("RENAME-PLUGIN", $"Plugin type scan partial failure: {assemblyPath} | {ex.Message}");
        }

        foreach (var type in types)
        {
            if (!typeof(INameCorrectionPlugin).IsAssignableFrom(type) ||
                type.IsAbstract ||
                type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            LoadedNameCorrectionPlugin? loaded = null;
            try
            {
                var instance = (INameCorrectionPlugin)Activator.CreateInstance(type)!;
                var descriptor = NormalizeDescriptor(instance.Descriptor, type);
                loaded = new LoadedNameCorrectionPlugin(instance, descriptor, assemblyPath, loadContext)
                {
                    SettingDefinitions = NormalizeDefinitions(instance.GetSettingDefinitions())
                };
            }
            catch (Exception ex)
            {
                FileToolsEnvironment.Log("RENAME-PLUGIN", $"Plugin activation failed: {type.FullName} | {ex.Message}");
            }

            if (loaded is not null)
            {
                yield return loaded;
            }
        }
    }

    /// <summary>플러그인 메타데이터를 누락 항목 없이 정규화한다.</summary>
    private static NameCorrectionPluginDescriptor NormalizeDescriptor(
        NameCorrectionPluginDescriptor descriptor,
        Type fallbackType)
    {
        var id = string.IsNullOrWhiteSpace(descriptor.Id)
            ? fallbackType.FullName ?? fallbackType.Name
            : descriptor.Id.Trim();
        return descriptor with
        {
            Id = id,
            DisplayName = string.IsNullOrWhiteSpace(descriptor.DisplayName)
                ? id
                : descriptor.DisplayName.Trim(),
            Version = descriptor.Version.Trim(),
            License = descriptor.License.Trim(),
            Description = descriptor.Description.Trim(),
            SupportedLanguages = descriptor.SupportedLanguages
                .Where(static language => !string.IsNullOrWhiteSpace(language))
                .Select(static language => language.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    /// <summary>중복 키/공백이 있는 플러그인 설정 정의를 정제한다.</summary>
    private static IReadOnlyList<NameCorrectionSettingDefinition> NormalizeDefinitions(
        IReadOnlyList<NameCorrectionSettingDefinition> definitions)
    {
        var result = new List<NameCorrectionSettingDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            var key = definition.Key.Trim();
            if (key.Length == 0 || !seen.Add(key))
            {
                continue;
            }

            result.Add(definition with
            {
                Key = key,
                DisplayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? key
                    : definition.DisplayName.Trim(),
                DefaultValue = definition.DefaultValue.Trim(),
                Description = definition.Description.Trim()
            });
        }

        return result;
    }

    private sealed class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _contractAssemblyName = typeof(INameCorrectionPlugin).Assembly.GetName().Name!;

        public PluginAssemblyLoadContext(string pluginAssemblyPath)
        {
            _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.Equals(assemblyName.Name, _contractAssemblyName, StringComparison.Ordinal))
            {
                return typeof(INameCorrectionPlugin).Assembly;
            }

            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(libraryPath);
        }
    }
}
