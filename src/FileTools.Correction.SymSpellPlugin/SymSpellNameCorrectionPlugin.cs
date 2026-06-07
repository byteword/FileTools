using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using FileTools.Correction;

namespace FileTools.Correction.SymSpellPlugin;

public sealed class SymSpellNameCorrectionPlugin : INameCorrectionPlugin
{
    private const string DictionaryPathKey = "dictionaryPath";
    private const string SourceModeKey = "sourceMode";
    private const string MaxEditDistanceKey = "maxEditDistance";
    private const string MinimumScoreKey = "minimumScore";
    private const string MaxCandidatesKey = "maxCandidates";
    private const string TermIndexKey = "termIndex";
    private const string CountIndexKey = "countIndex";

    private static readonly Regex TokenRegex = new(@"[\p{L}\p{M}]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly ConcurrentDictionary<string, Lazy<SymSpell?>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public NameCorrectionPluginDescriptor Descriptor { get; } = new()
    {
        Id = "filetools.symspell",
        DisplayName = "SymSpell candidate provider",
        Version = "1.0.0",
        License = "MIT",
        Description = "Adds review-only candidates from a user-provided SymSpell dictionary or corpus.",
        SupportedLanguages = ["en-US", "ko-KR"]
    };

    public IReadOnlyList<NameCorrectionSettingDefinition> GetSettingDefinitions()
    {
        return
        [
            new NameCorrectionSettingDefinition
            {
                Key = DictionaryPathKey,
                DisplayName = "Dictionary or corpus path",
                Kind = NameCorrectionSettingKind.FilePath,
                Description = "Use a user-provided file. Frequency dictionaries should use 'term count' rows."
            },
            new NameCorrectionSettingDefinition
            {
                Key = SourceModeKey,
                DisplayName = "Source mode",
                Kind = NameCorrectionSettingKind.Select,
                DefaultValue = "frequency",
                Options =
                [
                    new NameCorrectionSettingOption { Value = "frequency", DisplayName = "Frequency dictionary" },
                    new NameCorrectionSettingOption { Value = "corpus", DisplayName = "Plain corpus" }
                ]
            },
            new NameCorrectionSettingDefinition
            {
                Key = MaxEditDistanceKey,
                DisplayName = "Max edit distance",
                Kind = NameCorrectionSettingKind.Number,
                DefaultValue = "1",
                Description = "1 is conservative. Larger values add more false-positive risk."
            },
            new NameCorrectionSettingDefinition
            {
                Key = MinimumScoreKey,
                DisplayName = "Minimum score",
                Kind = NameCorrectionSettingKind.Number,
                DefaultValue = "0.60",
                Description = "Candidates below this score are ignored."
            },
            new NameCorrectionSettingDefinition
            {
                Key = MaxCandidatesKey,
                DisplayName = "Max candidates",
                Kind = NameCorrectionSettingKind.Number,
                DefaultValue = "3"
            },
            new NameCorrectionSettingDefinition
            {
                Key = TermIndexKey,
                DisplayName = "Term column",
                Kind = NameCorrectionSettingKind.Number,
                DefaultValue = "0"
            },
            new NameCorrectionSettingDefinition
            {
                Key = CountIndexKey,
                DisplayName = "Count column",
                Kind = NameCorrectionSettingKind.Number,
                DefaultValue = "1"
            }
        ];
    }

    public IReadOnlyDictionary<string, string> NormalizeSettings(IReadOnlyDictionary<string, string> settings)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [DictionaryPathKey] = Get(settings, DictionaryPathKey, ""),
            [SourceModeKey] = Get(settings, SourceModeKey, "frequency").Equals("corpus", StringComparison.OrdinalIgnoreCase)
                ? "corpus"
                : "frequency",
            [MaxEditDistanceKey] = ClampInt(Get(settings, MaxEditDistanceKey, "1"), 0, 3, 1).ToString(),
            [MinimumScoreKey] = ClampDouble(Get(settings, MinimumScoreKey, "0.60"), 0, 1, 0.60)
                .ToString("0.00", CultureInfo.InvariantCulture),
            [MaxCandidatesKey] = ClampInt(Get(settings, MaxCandidatesKey, "3"), 1, 10, 3).ToString(),
            [TermIndexKey] = ClampInt(Get(settings, TermIndexKey, "0"), 0, 8, 0).ToString(),
            [CountIndexKey] = ClampInt(Get(settings, CountIndexKey, "1"), 0, 8, 1).ToString()
        };
    }

    public IReadOnlyList<PluginCorrectionCandidate> GenerateCandidates(
        NameCorrectionRequest request,
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeSettings(settings);
        var symSpell = GetOrLoadDictionary(normalized);
        if (symSpell is null || symSpell.WordCount == 0)
        {
            return [];
        }

        var source = string.IsNullOrWhiteSpace(request.SuggestedStem)
            ? request.OriginalStem
            : request.SuggestedStem;
        if (string.IsNullOrWhiteSpace(source))
        {
            return [];
        }

        var maxEditDistance = ClampInt(Get(normalized, MaxEditDistanceKey, "1"), 0, 3, 1);
        var minimumScore = ClampDouble(Get(normalized, MinimumScoreKey, "0.60"), 0, 1, 0.60);
        var maxCandidates = ClampInt(Get(normalized, MaxCandidatesKey, "3"), 1, 10, 3);
        var changed = 0;
        var totalDistance = 0;

        var candidate = TokenRegex.Replace(source, match =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var token = match.Value;
            var suggestions = symSpell.Lookup(token, SymSpell.Verbosity.Closest, maxEditDistance);
            var suggestion = suggestions.FirstOrDefault(item =>
                !string.Equals(item.term, token, StringComparison.OrdinalIgnoreCase));
            if (suggestion is null)
            {
                return token;
            }

            changed++;
            totalDistance += suggestion.distance;
            return PreserveSimpleCasing(token, suggestion.term);
        });

        if (changed == 0 || string.Equals(candidate, source, StringComparison.Ordinal))
        {
            return [];
        }

        var averageDistance = (double)totalDistance / changed;
        var score = Math.Clamp(1.0 - (averageDistance / (maxEditDistance + 1.0)), 0, 1);
        if (score < minimumScore)
        {
            return [];
        }

        var result = new List<PluginCorrectionCandidate>
        {
            new()
            {
                Value = candidate,
                Score = score,
                Reason = $"corrected {changed} token(s), max edit {maxEditDistance}",
                Source = "SymSpell",
                IsFullFileName = false,
                RequiresReview = true
            }
        };
        return result.Take(maxCandidates).ToArray();
    }

    private static SymSpell? GetOrLoadDictionary(IReadOnlyDictionary<string, string> settings)
    {
        var path = Get(settings, DictionaryPathKey, "");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        var mode = Get(settings, SourceModeKey, "frequency");
        var maxEditDistance = ClampInt(Get(settings, MaxEditDistanceKey, "1"), 0, 3, 1);
        var termIndex = ClampInt(Get(settings, TermIndexKey, "0"), 0, 8, 0);
        var countIndex = ClampInt(Get(settings, CountIndexKey, "1"), 0, 8, 1);
        var key = string.Join('|', fullPath, mode, maxEditDistance, termIndex, countIndex);

        return Cache.GetOrAdd(key, _ => new Lazy<SymSpell?>(() =>
        {
            if (!File.Exists(fullPath))
            {
                return null;
            }

            var symSpell = new SymSpell(maxDictionaryEditDistance: maxEditDistance);
            var loaded = mode.Equals("corpus", StringComparison.OrdinalIgnoreCase)
                ? symSpell.CreateDictionary(fullPath)
                : symSpell.LoadDictionary(fullPath, termIndex, countIndex);
            return loaded ? symSpell : null;
        })).Value;
    }

    private static string PreserveSimpleCasing(string original, string suggestion)
    {
        if (original.All(char.IsUpper))
        {
            return suggestion.ToUpperInvariant();
        }

        if (char.IsUpper(original[0]))
        {
            return char.ToUpperInvariant(suggestion[0]) + suggestion[1..];
        }

        return suggestion;
    }

    private static string Get(IReadOnlyDictionary<string, string> settings, string key, string fallback)
    {
        return settings.TryGetValue(key, out var value) ? value.Trim() : fallback;
    }

    private static int ClampInt(string value, int min, int max, int fallback)
    {
        return int.TryParse(value, out var parsed) ? Math.Clamp(parsed, min, max) : fallback;
    }

    private static double ClampDouble(string value, double min, double max, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, min, max)
            : fallback;
    }
}
