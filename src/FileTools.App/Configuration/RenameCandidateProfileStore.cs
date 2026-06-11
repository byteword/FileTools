using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

/// <summary>왜곡한글 후보 계산에 쓰는 학습 프로필 모델과 저장소.</summary>
internal sealed class RenameCandidateProfileDocument
{
    public ObfuscatedHangulCandidateProfile ObfuscatedHangul { get; set; } = new();
}

internal sealed class ObfuscatedHangulCandidateProfile
{
    public List<string> ScoringWords { get; set; } = [];

    public List<string> ProtectedEnglishWords { get; set; } = [];
}

internal static class RenameCandidateProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static readonly string[] DefaultProtectedEnglishWords =
    [
        "idol",
        "lol",
        "no",
        "vol",
        "season",
        "special",
        "episode",
        "ep"
    ];

    public static string ProfilePath => Path.Combine(FileToolsEnvironment.AppDataDir, "rename-candidate-profile.json");

    /// <summary>
    /// 프로필 파일을 읽고 없으면 기본 프로필을 생성한다.
    /// </summary>
    public static RenameCandidateProfileDocument Load(IEnumerable<string>? legacyScoringWords = null)
    {
        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        if (!File.Exists(ProfilePath))
        {
            var document = CreateDefaultDocument(legacyScoringWords);
            Save(document);
            return document;
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<RenameCandidateProfileDocument>(
                File.ReadAllText(ProfilePath),
                JsonOptions) ?? CreateDefaultDocument());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            FileToolsEnvironment.Log("RENAME-CANDIDATE-PROFILE", ex.Message);
            return CreateDefaultDocument();
        }
    }

    /// <summary>
    /// 현재 프로필을 정규화해 저장한다.
    /// </summary>
    public static void Save(RenameCandidateProfileDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        File.WriteAllText(ProfilePath, JsonSerializer.Serialize(Normalize(document), JsonOptions));
    }

    /// <summary>기본 프로필을 생성한다.</summary>
    public static RenameCandidateProfileDocument CreateDefaultDocument(IEnumerable<string>? scoringWords = null)
    {
        return new RenameCandidateProfileDocument
        {
            ObfuscatedHangul = new ObfuscatedHangulCandidateProfile
            {
                ScoringWords = NormalizeList(scoringWords),
                ProtectedEnglishWords = NormalizeList(DefaultProtectedEnglishWords)
            }
        };
    }

    public static RenameCandidateProfileDocument Normalize(RenameCandidateProfileDocument document)
    {
        return new RenameCandidateProfileDocument
        {
            ObfuscatedHangul = new ObfuscatedHangulCandidateProfile
            {
                ScoringWords = NormalizeList(document.ObfuscatedHangul?.ScoringWords),
                ProtectedEnglishWords = NormalizeList(document.ObfuscatedHangul is null
                    ? DefaultProtectedEnglishWords
                    : document.ObfuscatedHangul.ProtectedEnglishWords)
            }
        };
    }

    private static List<string> NormalizeList(IEnumerable<string>? values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var value in values ?? [])
        {
            var normalized = value.Trim();
            if (normalized.Length > 0 && seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
