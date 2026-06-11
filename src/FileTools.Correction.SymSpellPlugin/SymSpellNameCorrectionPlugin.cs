using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using FileTools.Correction;

namespace FileTools.Correction.SymSpellPlugin;

/// <summary>
/// SymSpell 기반 후보 생성 플러그인.
/// 사용자 사전/코퍼스와 설정을 바탕으로 파일명 토큰 단위 보정 후보를 생성한다.
/// </summary>
public sealed class SymSpellNameCorrectionPlugin : INameCorrectionPlugin
{
    /// <summary>사전 경로 설정 키.</summary>
    private const string DictionaryPathKey = "dictionaryPath";
    /// <summary>소스 모드 설정 키.</summary>
    private const string SourceModeKey = "sourceMode";
    /// <summary>허용 편집 거리 설정 키.</summary>
    private const string MaxEditDistanceKey = "maxEditDistance";
    /// <summary>최소 점수 임계값 키.</summary>
    private const string MinimumScoreKey = "minimumScore";
    /// <summary>최대 후보 개수 키.</summary>
    private const string MaxCandidatesKey = "maxCandidates";
    /// <summary>사전 항목의 term 열 인덱스.</summary>
    private const string TermIndexKey = "termIndex";
    /// <summary>사전 항목의 빈도/카운트 열 인덱스.</summary>
    private const string CountIndexKey = "countIndex";

    /// <summary>후보 계산에서 사용할 토큰 추출 정규식.</summary>
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{M}]{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    /// <summary>옵션 조합별 SymSpell 인스턴스 캐시.</summary>
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

    /// <summary>
    /// 설정 값을 정규화해 안전한 범위로 보정한다.
    /// </summary>
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

    /// <summary>
    /// path/옵션 조합별로 SymSpell 인스턴스를 캐시에서 재사용한다.
    /// </summary>
    /// <remarks>
    /// 1) 경로/모드/거리/컬럼 설정으로 키를 만들고
    /// 2) 캐시 미스면 로딩/파싱 비용이 큰 SymSpell 로더를 한 번만 실행해 저장한다.
    /// </remarks>
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

    /// <summary>
    /// 대문자/첫 글자 대문자 유지 정책으로 대소문자 가독성을 유지한다.
    /// </summary>
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

    /// <summary>키가 없으면 fallback을 반환하는 설정 값 조회 유틸.</summary>
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
