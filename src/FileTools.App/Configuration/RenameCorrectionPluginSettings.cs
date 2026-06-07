namespace FileTools;

internal sealed class RenameCorrectionPluginOptions
{
    public bool Enabled { get; set; }

    public string Language { get; set; } = RenameCorrectionPluginDefaults.DefaultLanguage;

    public List<RenameCorrectionPluginConfiguration> Plugins { get; set; } = [];

    public RenameCorrectionPluginOptions Clone()
    {
        return new RenameCorrectionPluginOptions
        {
            Enabled = Enabled,
            Language = Language,
            Plugins = Plugins
                .Select(static plugin => plugin.Clone())
                .ToList()
        };
    }
}

internal sealed class RenameCorrectionPluginConfiguration
{
    public string PluginId { get; set; } = "";

    public bool Enabled { get; set; }

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public RenameCorrectionPluginConfiguration Clone()
    {
        return new RenameCorrectionPluginConfiguration
        {
            PluginId = PluginId,
            Enabled = Enabled,
            Settings = new Dictionary<string, string>(Settings, StringComparer.OrdinalIgnoreCase)
        };
    }
}

internal static class RenameCorrectionPluginDefaults
{
    public const string DefaultLanguage = "ko-KR";

    public static readonly string[] SupportedLanguages = ["ko-KR", "en-US"];

    public static RenameCorrectionPluginOptions Normalize(RenameCorrectionPluginOptions? options)
    {
        var normalized = options?.Clone() ?? new RenameCorrectionPluginOptions();
        normalized.Language = NormalizeLanguage(normalized.Language);

        var plugins = new List<RenameCorrectionPluginConfiguration>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in normalized.Plugins ?? [])
        {
            var pluginId = (plugin.PluginId ?? "").Trim();
            if (pluginId.Length == 0 || !seen.Add(pluginId))
            {
                continue;
            }

            plugins.Add(new RenameCorrectionPluginConfiguration
            {
                PluginId = pluginId,
                Enabled = plugin.Enabled,
                Settings = new Dictionary<string, string>(
                    plugin.Settings ?? new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase)
            });
        }

        normalized.Plugins = plugins;
        return normalized;
    }

    public static string NormalizeLanguage(string? language)
    {
        var normalized = (language ?? "").Trim();
        if (normalized.Length == 0)
        {
            return DefaultLanguage;
        }

        return SupportedLanguages.FirstOrDefault(
            candidate => string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)) ??
            normalized;
    }

    public static RenameCorrectionPluginConfiguration GetOrCreatePlugin(
        RenameCorrectionPluginOptions options,
        string pluginId)
    {
        var normalizedId = pluginId.Trim();
        var existing = options.Plugins.FirstOrDefault(
            plugin => string.Equals(plugin.PluginId, normalizedId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var created = new RenameCorrectionPluginConfiguration { PluginId = normalizedId };
        options.Plugins.Add(created);
        return created;
    }
}
