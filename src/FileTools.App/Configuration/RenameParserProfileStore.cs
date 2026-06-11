using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

/// <summary>파일명 파서용 사전(접두어/키워드) 모델과 저장소.</summary>
internal sealed class RenameParserProfileDocument
{
    public List<string> KnownTags { get; set; } =
    [
        "완결",
        "번역",
        "단편",
        "컬러",
        "무삭제",
        "개정판",
        "외전",
        "직번"
    ];

    public List<string> TitleNoiseWords { get; set; } =
    [
        "완결",
        "번역",
        "단편",
        "컬러",
        "무삭제",
        "개정판",
        "외전"
    ];

    public List<string> AuthorPrefixes { get; set; } =
    [
        "작가",
        "저자",
        "by"
    ];

    public List<string> EpisodePrefixes { get; set; } =
    [
        "제",
        "第",
        "ep",
        "episode"
    ];

    public List<string> EpisodeUnits { get; set; } =
    [
        "화",
        "話",
        "회",
        "편",
        "권",
        "巻",
        "부",
        "ep",
        "episode"
    ];
}

internal static class RenameParserProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string ProfilePath => Path.Combine(FileToolsEnvironment.AppDataDir, "rename-parser-profile.json");

    /// <summary>
    /// 파서 프로필을 읽고 없으면 기본값을 저장한다.
    /// </summary>
    public static RenameParserProfileDocument Load()
    {
        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        if (!File.Exists(ProfilePath))
        {
            var document = CreateDefaultDocument();
            Save(document);
            return document;
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<RenameParserProfileDocument>(
                File.ReadAllText(ProfilePath),
                JsonOptions) ?? CreateDefaultDocument());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            FileToolsEnvironment.Log("RENAME-PARSER-PROFILE", ex.Message);
            return CreateDefaultDocument();
        }
    }

    /// <summary>
    /// 정규화 후 파서 프로필을 저장한다.
    /// </summary>
    public static void Save(RenameParserProfileDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        File.WriteAllText(ProfilePath, JsonSerializer.Serialize(Normalize(document), JsonOptions));
    }

    /// <summary>기본 문서를 반환한다.</summary>
    public static RenameParserProfileDocument CreateDefaultDocument()
    {
        return new RenameParserProfileDocument();
    }

    public static RenameParserProfileDocument Normalize(RenameParserProfileDocument document)
    {
        return new RenameParserProfileDocument
        {
            KnownTags = NormalizeList(document.KnownTags),
            TitleNoiseWords = NormalizeList(document.TitleNoiseWords),
            AuthorPrefixes = NormalizeList(document.AuthorPrefixes),
            EpisodePrefixes = NormalizeList(document.EpisodePrefixes),
            EpisodeUnits = NormalizeList(document.EpisodeUnits)
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
