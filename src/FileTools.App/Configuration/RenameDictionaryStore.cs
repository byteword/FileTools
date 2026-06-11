using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

/// <summary>이름 변경 사전(치환/공통어) 영속화 모델.</summary>
internal sealed class RenameDictionaryDocument
{
    public List<RenameDictionaryEntry> Replacements { get; set; } = [];

    public List<string> CommonPhrases { get; set; } = [];
}

internal sealed class RenameDictionaryEntry
{
    public string Source { get; set; } = "";

    public string Replacement { get; set; } = "";

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Source)
            ? ""
            : $"{Source} -> {Replacement}";
    }
}

internal static class RenameDictionaryStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string DictionaryPath => Path.Combine(FileToolsEnvironment.AppDataDir, "rename-dictionary.json");

    /// <summary>
    /// 사전 파일을 읽고 없으면 빈 문서를 저장한 뒤 반환한다.
    /// </summary>
    public static RenameDictionaryDocument Load()
    {
        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        if (!File.Exists(DictionaryPath))
        {
            var document = new RenameDictionaryDocument();
            Save(document);
            return document;
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<RenameDictionaryDocument>(
                File.ReadAllText(DictionaryPath),
                JsonOptions) ?? new RenameDictionaryDocument());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            FileToolsEnvironment.Log("RENAME-DICTIONARY", ex.Message);
            return new RenameDictionaryDocument();
        }
    }

    /// <summary>
    /// 문서를 정규화 후 JSON으로 저장한다.
    /// </summary>
    public static void Save(RenameDictionaryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        File.WriteAllText(DictionaryPath, JsonSerializer.Serialize(Normalize(document), JsonOptions));
    }

    /// <summary>
    /// source/phrase 입력을 정규화한다.
    /// 공백 제거와 중복 제거를 통해 저장 가능한 규칙 집합을 만든다.
    /// </summary>
    private static RenameDictionaryDocument Normalize(RenameDictionaryDocument document)
    {
        var normalized = new RenameDictionaryDocument();
        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in document.Replacements)
        {
            var source = entry.Source.Trim();
            if (source.Length == 0 || !seenSources.Add(source))
            {
                continue;
            }

            normalized.Replacements.Add(new RenameDictionaryEntry
            {
                Source = source,
                Replacement = entry.Replacement.Trim()
            });
        }

        var seenPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var phrase in document.CommonPhrases)
        {
            var normalizedPhrase = phrase.Trim();
            if (normalizedPhrase.Length > 0 && seenPhrases.Add(normalizedPhrase))
            {
                normalized.CommonPhrases.Add(normalizedPhrase);
            }
        }

        return normalized;
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
