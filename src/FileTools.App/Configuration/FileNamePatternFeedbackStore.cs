using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

internal sealed record FileNamePatternFeedbackStoreOptions
{
    public bool Enabled { get; init; } = true;

    public int FeedbackLimit { get; init; } = FileNamePatternFeedbackStore.DefaultFeedbackLimit;
}

internal static class FileNamePatternFeedbackStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public const int MinimumFeedbackLimit = 100;

    public const int DefaultFeedbackLimit = 2000;

    public static string FeedbackPath => Path.Combine(FileToolsEnvironment.AppDataDir, "rename-pattern-feedback.jsonl");

    public static IReadOnlyList<FileNamePatternFeedback> Load(
        string? path = null,
        FileNamePatternFeedbackStoreOptions? options = null)
    {
        var normalizedOptions = NormalizeOptions(options);
        if (!normalizedOptions.Enabled)
        {
            return [];
        }

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

        return TrimToLimit(FileNamePatternFeedbackNormalizer.Normalize(feedback), normalizedOptions.FeedbackLimit);
    }

    public static void Save(
        IEnumerable<FileNamePatternFeedback> feedback,
        string? path = null,
        FileNamePatternFeedbackStoreOptions? options = null)
    {
        var normalizedOptions = NormalizeOptions(options);
        if (!normalizedOptions.Enabled)
        {
            return;
        }

        var targetPath = ResolvePath(path);
        EnsureParentDirectory(targetPath);
        using var writer = new StreamWriter(targetPath, append: false);
        foreach (var item in TrimToLimit(
            FileNamePatternFeedbackNormalizer.Normalize(feedback),
            normalizedOptions.FeedbackLimit))
        {
            writer.WriteLine(JsonSerializer.Serialize(item, JsonOptions));
        }
    }

    public static void Append(
        FileNamePatternFeedback feedback,
        string? path = null,
        FileNamePatternFeedbackStoreOptions? options = null)
    {
        var normalizedOptions = NormalizeOptions(options);
        if (!normalizedOptions.Enabled)
        {
            return;
        }

        var targetPath = ResolvePath(path);
        var normalized = FileNamePatternFeedbackNormalizer.Normalize([feedback]);
        if (normalized.Count == 0)
        {
            return;
        }

        var existing = Load(targetPath, normalizedOptions);
        Save(existing.Concat(normalized), targetPath, normalizedOptions);
    }

    public static FileNamePatternFeedbackStoreOptions CreateOptions(FileToolsSettings settings)
    {
        return NormalizeOptions(new FileNamePatternFeedbackStoreOptions
        {
            Enabled = settings.RenamePatternLearningEnabled,
            FeedbackLimit = settings.RenamePatternFeedbackLimit
        });
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static FileNamePatternFeedbackStoreOptions NormalizeOptions(FileNamePatternFeedbackStoreOptions? options)
    {
        options ??= new FileNamePatternFeedbackStoreOptions();
        return options with
        {
            FeedbackLimit = Math.Max(MinimumFeedbackLimit, options.FeedbackLimit)
        };
    }

    private static IReadOnlyList<FileNamePatternFeedback> TrimToLimit(
        IReadOnlyList<FileNamePatternFeedback> feedback,
        int feedbackLimit)
    {
        var limit = Math.Max(MinimumFeedbackLimit, feedbackLimit);
        return feedback.Count <= limit
            ? feedback
            : feedback.Skip(feedback.Count - limit).ToArray();
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
