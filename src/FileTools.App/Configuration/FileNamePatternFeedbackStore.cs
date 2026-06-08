using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

internal static class FileNamePatternFeedbackStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string FeedbackPath => Path.Combine(FileToolsEnvironment.AppDataDir, "rename-pattern-feedback.jsonl");

    public static IReadOnlyList<FileNamePatternFeedback> Load(string? path = null)
    {
        var targetPath = ResolvePath(path);
        if (!File.Exists(targetPath))
        {
            return [];
        }

        var feedback = new List<FileNamePatternFeedback>();
        try
        {
            foreach (var line in File.ReadLines(targetPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var item = JsonSerializer.Deserialize<FileNamePatternFeedback>(line, JsonOptions);
                    if (item is not null)
                    {
                        feedback.Add(item);
                    }
                }
                catch (JsonException ex)
                {
                    FileToolsEnvironment.Log("RENAME-PATTERN-FEEDBACK", ex.Message);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileToolsEnvironment.Log("RENAME-PATTERN-FEEDBACK", ex.Message);
            return [];
        }

        return FileNamePatternFeedbackNormalizer.Normalize(feedback);
    }

    public static void Save(IEnumerable<FileNamePatternFeedback> feedback, string? path = null)
    {
        var targetPath = ResolvePath(path);
        EnsureParentDirectory(targetPath);
        using var writer = new StreamWriter(targetPath, append: false);
        foreach (var item in FileNamePatternFeedbackNormalizer.Normalize(feedback))
        {
            writer.WriteLine(JsonSerializer.Serialize(item, JsonOptions));
        }
    }

    public static void Append(FileNamePatternFeedback feedback, string? path = null)
    {
        var targetPath = ResolvePath(path);
        var normalized = FileNamePatternFeedbackNormalizer.Normalize([feedback]);
        if (normalized.Count == 0)
        {
            return;
        }

        EnsureParentDirectory(targetPath);
        File.AppendAllText(
            targetPath,
            JsonSerializer.Serialize(normalized[0], JsonOptions) + Environment.NewLine);
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string ResolvePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? FeedbackPath : path;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
