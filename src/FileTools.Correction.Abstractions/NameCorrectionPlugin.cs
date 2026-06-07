namespace FileTools.Correction;

public interface INameCorrectionPlugin
{
    NameCorrectionPluginDescriptor Descriptor { get; }

    IReadOnlyList<NameCorrectionSettingDefinition> GetSettingDefinitions();

    IReadOnlyDictionary<string, string> NormalizeSettings(IReadOnlyDictionary<string, string> settings);

    IReadOnlyList<PluginCorrectionCandidate> GenerateCandidates(
        NameCorrectionRequest request,
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken);
}

public sealed record NameCorrectionPluginDescriptor
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string Version { get; init; } = "";

    public string License { get; init; } = "";

    public string Description { get; init; } = "";

    public IReadOnlyList<string> SupportedLanguages { get; init; } = [];
}

public sealed record NameCorrectionRequest
{
    public required string OriginalPath { get; init; }

    public required string OriginalFileName { get; init; }

    public required string OriginalStem { get; init; }

    public required string SuggestedFileName { get; init; }

    public required string SuggestedStem { get; init; }

    public required string Extension { get; init; }

    public string Title { get; init; } = "";

    public string? EpisodeRange { get; init; }

    public string? Author { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string Language { get; init; } = "";

    public bool IsDirectory { get; init; }

    public IReadOnlyList<string> CommonPhrases { get; init; } = [];
}

public sealed record PluginCorrectionCandidate
{
    public required string Value { get; init; }

    public double Score { get; init; } = 0.5;

    public string Reason { get; init; } = "";

    public string Source { get; init; } = "";

    public bool IsFullFileName { get; init; }

    public bool RequiresReview { get; init; } = true;
}

public enum NameCorrectionSettingKind
{
    Boolean,
    Text,
    Number,
    FilePath,
    Select
}

public sealed record NameCorrectionSettingDefinition
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public NameCorrectionSettingKind Kind { get; init; } = NameCorrectionSettingKind.Text;

    public string DefaultValue { get; init; } = "";

    public string Description { get; init; } = "";

    public bool IsRequired { get; init; }

    public IReadOnlyList<NameCorrectionSettingOption> Options { get; init; } = [];
}

public sealed record NameCorrectionSettingOption
{
    public required string Value { get; init; }

    public required string DisplayName { get; init; }
}
