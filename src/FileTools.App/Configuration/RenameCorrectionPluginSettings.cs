namespace FileTools;

/// <summary>이름 교정 플러그인 설정과 정규화 정책.</summary>
internal sealed class RenameCorrectionPluginOptions
{
    public bool Enabled { get; set; }

    public string Language { get; set; } = RenameCorrectionPluginDefaults.DefaultLanguage;

    public List<RenameCorrectionPluginConfiguration> Plugins { get; set; } = [];

    /// <summary>
    /// 플러그인 설정 목록을 깊은 복사한다.
    /// </summary>
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

/// <summary>단일 플러그인 설정.</summary>
internal sealed class RenameCorrectionPluginConfiguration
{
    public string PluginId { get; set; } = "";

    public bool Enabled { get; set; }

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 설정 사전을 독립 복사해서 반환한다.
    /// </summary>
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

/// <summary>플러그인 설정 정규화 유틸.</summary>
internal static class RenameCorrectionPluginDefaults
{
    public const string DefaultLanguage = "ko-KR";

    public static readonly string[] SupportedLanguages = ["ko-KR", "en-US"];

    /// <summary>
    /// null/빈값/중복 제거로 사용자 설정을 정돈한다.
    /// </summary>
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
