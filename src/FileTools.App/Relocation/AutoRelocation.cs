using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FileTools;

internal static class AutoRelocationTemplateDefaults
{
    public const int SchemaVersion = 2;
    public const string DefaultTemplateId = "Default";
    public const string TemplateFileExtension = ".json";

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public static IReadOnlyList<AutoRelocationValueSource> FileDerivedValueSources { get; } =
    [
        AutoRelocationValueSource.FileName,
        AutoRelocationValueSource.FileExtension,
        AutoRelocationValueSource.KnownFileKind,
        AutoRelocationValueSource.Title,
        AutoRelocationValueSource.EpisodeRange,
        AutoRelocationValueSource.SizeBytes,
        AutoRelocationValueSource.ModifiedAt,
        AutoRelocationValueSource.CreatedAt
    ];

    public static AutoRelocationTemplateDocument CreateDefaultTemplate()
    {
        return new AutoRelocationTemplateDocument
        {
            Id = DefaultTemplateId,
            DisplayName = Localizer.Get("DefaultRelocationTemplateName"),
            Description = Localizer.Get("DefaultRelocationTemplateDescription"),
            PathRules =
            [
                new AutoRelocationPathRule
                {
                    Source = AutoRelocationValueSource.Title,
                    Transform = AutoRelocationValueTransform.InitialBucket,
                    Language = AutoRelocationLanguageProfile.KoreanEnglish,
                    Format = "[{value}]",
                    FallbackFolderName = "[0A]"
                }
            ]
        };
    }

    public static AutoRelocationValueSource NormalizeValueSource(AutoRelocationValueSource source)
    {
        return FileDerivedValueSources.Contains(source)
            ? source
            : source switch
            {
                AutoRelocationValueSource.FileType => AutoRelocationValueSource.KnownFileKind,
                AutoRelocationValueSource.OriginalTitle => AutoRelocationValueSource.FileName,
                AutoRelocationValueSource.Author => AutoRelocationValueSource.FileName,
                AutoRelocationValueSource.Tags => AutoRelocationValueSource.FileName,
                AutoRelocationValueSource.SeriesStatus => AutoRelocationValueSource.FileName,
                AutoRelocationValueSource.ImageCount => AutoRelocationValueSource.FileName,
                _ => AutoRelocationValueSource.FileName
            };
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

internal sealed class AutoRelocationTemplateDocument
{
    public int SchemaVersion { get; init; } = AutoRelocationTemplateDefaults.SchemaVersion;

    [JsonIgnore]
    public string Id { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string? Description { get; init; }

    public List<AutoRelocationPrefilterRule> Prefilters { get; init; } = [];

    public List<AutoRelocationPathRule> PathRules { get; init; } = [];
}

internal sealed class AutoRelocationPrefilterRule
{
    public bool Enabled { get; init; } = true;
    public AutoRelocationValueSource Source { get; init; } = AutoRelocationValueSource.FileName;
    public AutoRelocationFilterOperator Operator { get; init; } = AutoRelocationFilterOperator.Contains;
    public string Value { get; init; } = "";
    public AutoRelocationPrefilterAction Action { get; init; } = AutoRelocationPrefilterAction.ReviewOnly;
    public string? TargetFolderName { get; init; }
}

internal sealed class AutoRelocationPathRule
{
    public bool Enabled { get; init; } = true;
    public AutoRelocationValueSource Source { get; init; } = AutoRelocationValueSource.Title;
    public AutoRelocationValueTransform Transform { get; init; } = AutoRelocationValueTransform.InitialBucket;
    public AutoRelocationLanguageProfile Language { get; init; } = AutoRelocationLanguageProfile.Auto;
    public string Format { get; init; } = "{value}";
    public string FallbackFolderName { get; init; } = "[ETC]";
    public AutoRelocationTransformOptions Options { get; init; } = new();
}

internal sealed class AutoRelocationTransformOptions
{
    public int? CharacterCount { get; init; }
    public int? NumberStep { get; init; }
    public string? NumberUnit { get; init; }
    public string? NumberLabelFormat { get; init; }
    public List<AutoRelocationNumberRange> NumberRanges { get; init; } = [];
    public AutoRelocationDatePart? DatePart { get; init; }
}

internal sealed class AutoRelocationNumberRange
{
    public double? Min { get; init; }
    public double? Max { get; init; }
    public string Label { get; init; } = "";
}

internal enum AutoRelocationValueSource
{
    FileName,
    FileExtension,
    KnownFileKind,
    FileType,
    Title,
    EpisodeRange,
    OriginalTitle,
    Author,
    Tags,
    SeriesStatus,
    SizeBytes,
    ImageCount,
    ModifiedAt,
    CreatedAt
}

internal enum AutoRelocationValueTransform
{
    Full,
    InitialBucket,
    FirstCharacters,
    NumberRange,
    NumberFloor,
    NumberCeiling,
    DatePart
}

internal enum AutoRelocationLanguageProfile
{
    Auto,
    KoreanEnglish,
    Korean,
    English,
    Japanese,
    Chinese
}

internal enum AutoRelocationFilterOperator
{
    Contains,
    Equals,
    StartsWith,
    EndsWith,
    Regex,
    IsEmpty,
    IsNotEmpty
}

internal enum AutoRelocationPrefilterAction
{
    ReviewOnly,
    Exclude,
    RouteToFolder
}

internal enum AutoRelocationDatePart
{
    Year,
    YearMonth,
    Month,
    Day
}

internal sealed record AutoRelocationTemplateFile(
    AutoRelocationTemplateDocument Document,
    string FilePath);

internal sealed class FileKindExtensionRule
{
    public string Kind { get; set; } = "";

    public List<string> Extensions { get; set; } = [];
}

internal static class AutoRelocationFileTypeClassifier
{
    public const string FolderKind = "Folder";
    public const string ArchiveKind = "Archive";
    public const string ImageKind = "Image";
    public const string VideoKind = "Video";
    public const string MusicKind = "Music";
    public const string TextKind = "Text";
    public const string DocumentKind = "Document";
    public const string ProgramKind = "Program";
    public const string OtherKind = "Other";

    private static readonly char[] ExtensionSeparators = [' ', '\t', '\r', '\n', ',', ';'];

    private static readonly Dictionary<string, string[]> DefaultExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [ArchiveKind] =
        [
            ".7z", ".ace", ".arj", ".bz", ".bz2", ".cab", ".cb7", ".cbr", ".cbt", ".cbz",
            ".cpio", ".dmg", ".gz", ".gzip", ".iso", ".lha", ".lzh", ".rar", ".tar",
            ".tbz", ".tbz2", ".tgz", ".txz", ".xz", ".z", ".zip", ".zipx"
        ],
        [ImageKind] =
        [
            ".ai", ".arw", ".avif", ".bmp", ".cr2", ".cur", ".dds", ".dib", ".dng",
            ".gif", ".heic", ".heif", ".ico", ".jfif", ".jpeg", ".jpg", ".jxl", ".nef",
            ".orf", ".png", ".psd", ".raw", ".rw2", ".svg", ".tga", ".tif", ".tiff", ".webp"
        ],
        [VideoKind] =
        [
            ".3g2", ".3gp", ".asf", ".avi", ".divx", ".flv", ".m2t", ".m2ts", ".m4v",
            ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".mts", ".ogv", ".rm", ".rmvb",
            ".ts", ".vob", ".webm", ".wmv", ".ass", ".idx", ".sami", ".smi", ".srt",
            ".ssa", ".sub", ".sup", ".usf", ".vtt"
        ],
        [MusicKind] =
        [
            ".aac", ".ac3", ".aif", ".aifc", ".aiff", ".alac", ".amr", ".ape", ".au",
            ".dts", ".flac", ".m4a", ".mid", ".midi", ".mka", ".mp3", ".mpc", ".oga",
            ".ogg", ".opus", ".ra", ".wav", ".weba", ".wma"
        ],
        [TextKind] =
        [
            ".cfg", ".conf", ".css", ".csv", ".htm", ".html", ".ini", ".json", ".log",
            ".markdown", ".md", ".nfo", ".properties", ".sql", ".text", ".toml", ".tsv",
            ".txt", ".xml", ".yaml", ".yml"
        ],
        [DocumentKind] =
        [
            ".azw", ".azw3", ".doc", ".docm", ".docx", ".dot", ".dotx", ".epub", ".hwp",
            ".hwpx", ".key", ".mobi", ".numbers", ".odp", ".ods", ".odt", ".pages", ".pdf",
            ".pot", ".potx", ".pps", ".ppsx", ".ppt", ".pptm", ".pptx", ".rtf", ".tex",
            ".xls", ".xlsb", ".xlsm", ".xlsx", ".xlt", ".xltx"
        ],
        [ProgramKind] =
        [
            ".apk", ".appx", ".appxbundle", ".bat", ".bin", ".class", ".cmd", ".com",
            ".cpl", ".deb", ".dll", ".drv", ".efi", ".exe", ".gadget", ".ipa", ".jar",
            ".js", ".jse", ".lnk", ".lua", ".msi", ".msp", ".msix", ".ocx", ".php",
            ".pkg", ".pl", ".ps1", ".psd1", ".psm1", ".py", ".pyw", ".rb", ".reg",
            ".rpm", ".run", ".scr", ".sh", ".sys", ".vb", ".vbe", ".vbs", ".wsf", ".wsh"
        ]
    };

    public static IReadOnlyList<string> ConfigurableFileKinds { get; } =
    [
        ArchiveKind,
        ImageKind,
        VideoKind,
        MusicKind,
        TextKind,
        DocumentKind,
        ProgramKind
    ];

    public static IReadOnlyList<FileKindExtensionRule> CreateDefaultExtensionRules()
    {
        return ConfigurableFileKinds
            .Select(static kind => new FileKindExtensionRule
            {
                Kind = kind,
                Extensions = DefaultExtensionMap[kind].ToList()
            })
            .ToArray();
    }

    public static IReadOnlyList<FileKindExtensionRule> NormalizeExtensionRules(
        IEnumerable<FileKindExtensionRule>? rules)
    {
        if (rules is null)
        {
            return CreateDefaultExtensionRules();
        }

        var normalizedRules = new List<FileKindExtensionRule>();
        var supplied = new Dictionary<string, FileKindExtensionRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            var kind = NormalizeFileKind(rule.Kind);
            if (!IsValidConfigurableFileKind(kind))
            {
                continue;
            }

            if (!supplied.TryGetValue(kind, out var normalizedRule))
            {
                normalizedRule = new FileKindExtensionRule { Kind = kind };
                supplied[kind] = normalizedRule;
                normalizedRules.Add(normalizedRule);
            }

            normalizedRule.Extensions.AddRange(NormalizeExtensions(rule.Extensions));
        }

        return normalizedRules
            .Select(static rule => new FileKindExtensionRule
            {
                Kind = rule.Kind,
                Extensions = NormalizeExtensions(rule.Extensions).ToList()
            })
            .ToArray();
    }

    public static string NormalizeFileKind(string? value)
    {
        var kind = (value ?? string.Empty).Trim();
        if (kind.Length == 0)
        {
            return string.Empty;
        }

        return Regex.Replace(kind, @"\s+", " ");
    }

    public static bool IsValidConfigurableFileKind(string? value)
    {
        var kind = NormalizeFileKind(value);
        return kind.Length > 0 &&
            !IsReservedFileKind(kind) &&
            kind.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            !string.Equals(kind, ".", StringComparison.Ordinal) &&
            !string.Equals(kind, "..", StringComparison.Ordinal);
    }

    public static bool IsReservedFileKind(string? value)
    {
        var kind = NormalizeFileKind(value);
        return string.Equals(kind, FolderKind, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, OtherKind, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> ParseExtensionList(string text)
    {
        return NormalizeExtensions(text.Split(
            ExtensionSeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static IReadOnlyList<string> NormalizeExtensions(IEnumerable<string>? extensions)
    {
        if (extensions is null)
        {
            return [];
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in extensions)
        {
            var value = NormalizeExtension(extension);
            if (value.Length > 0 && seen.Add(value))
            {
                normalized.Add(value);
            }
        }

        return normalized;
    }

    public static string NormalizeExtension(string? value)
    {
        var extension = (value ?? string.Empty).Trim();
        if (extension.Length == 0)
        {
            return string.Empty;
        }

        if (extension.StartsWith("*.", StringComparison.Ordinal))
        {
            extension = extension[1..];
        }

        if (!extension.StartsWith(".", StringComparison.Ordinal))
        {
            extension = "." + extension;
        }

        if (!IsSupportedExtensionFormat(extension))
        {
            return string.Empty;
        }

        return extension.ToLowerInvariant();
    }

    public static string GetFileType(string path, FileToolsSettings? settings = null)
    {
        return GetKnownFileKind(path, settings);
    }

    public static string GetKnownFileKind(string path, FileToolsSettings? settings = null)
    {
        if (Directory.Exists(path))
        {
            return FolderKind;
        }

        var extension = NormalizeExtension(Path.GetExtension(path));
        if (extension.Length == 0)
        {
            return OtherKind;
        }

        foreach (var rule in GetConfiguredExtensionRules(settings))
        {
            if (rule.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return rule.Kind;
            }
        }

        return OtherKind;
    }

    private static IReadOnlyList<FileKindExtensionRule> GetConfiguredExtensionRules(FileToolsSettings? settings)
    {
        try
        {
            return NormalizeExtensionRules((settings ?? SettingsStore.Load()).FileKindExtensionRules);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            FileToolsEnvironment.Log("FILE_KIND", ex.Message);
            return CreateDefaultExtensionRules();
        }
    }

    private static bool IsSupportedExtensionFormat(string extension)
    {
        return extension.Length > 1 &&
            !extension.Contains("\\", StringComparison.Ordinal) &&
            !extension.Contains("/", StringComparison.Ordinal) &&
            !extension.Contains(":", StringComparison.Ordinal);
    }
}

internal static class AutoRelocationTemplateStore
{
    public static string TemplateRootPath => Path.Combine(FileToolsEnvironment.AppDataDir, "Relocate");

    public static IReadOnlyList<AutoRelocationTemplateFile> LoadTemplates()
    {
        EnsureTemplatesInitialized();
        var templates = new List<AutoRelocationTemplateFile>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in Directory.EnumerateFiles(
                     TemplateRootPath,
                     "*" + AutoRelocationTemplateDefaults.TemplateFileExtension,
                     SearchOption.TopDirectoryOnly)
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var id = NormalizeTemplateId(Path.GetFileNameWithoutExtension(filePath));
                if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id))
                {
                    continue;
                }

                var document = JsonSerializer.Deserialize<AutoRelocationTemplateDocument>(
                    File.ReadAllText(filePath),
                    AutoRelocationTemplateDefaults.JsonOptions);
                if (document is null)
                {
                    continue;
                }

                templates.Add(new AutoRelocationTemplateFile(NormalizeDocument(document, id), filePath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                FileToolsEnvironment.Log("TEMPLATE", ex.Message);
            }
        }

        return templates
            .OrderBy(static template => template.Document.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static template => template.Document.Id, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static AutoRelocationTemplateFile FindTemplateOrDefault(string? templateId)
    {
        var templates = LoadTemplates();
        return templates.FirstOrDefault(template => string.Equals(
                   template.Document.Id,
                   NormalizeTemplateId(templateId),
                   StringComparison.OrdinalIgnoreCase))
               ?? templates.First(template => string.Equals(
                   template.Document.Id,
                   AutoRelocationTemplateDefaults.DefaultTemplateId,
                   StringComparison.OrdinalIgnoreCase));
    }

    public static AutoRelocationTemplateFile SaveTemplate(
        AutoRelocationTemplateDocument document,
        string? previousTemplateId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var id = NormalizeTemplateId(document.Id);
        if (!IsValidTemplateId(id))
        {
            throw new InvalidOperationException(Localizer.Get("TemplateIdInvalid"));
        }

        Directory.CreateDirectory(TemplateRootPath);
        var normalized = NormalizeDocument(document, id);
        var targetPath = GetTemplateFilePath(id);
        var previousId = string.IsNullOrWhiteSpace(previousTemplateId)
            ? null
            : NormalizeTemplateId(previousTemplateId);
        var previousPath = string.IsNullOrWhiteSpace(previousId) ? null : GetTemplateFilePath(previousId);
        if (string.Equals(previousId, AutoRelocationTemplateDefaults.DefaultTemplateId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(previousId, id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Localizer.Get("DefaultTemplateCannotRename"));
        }

        if (File.Exists(targetPath) &&
            !string.Equals(targetPath, previousPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Localizer.Format("TemplateIdAlreadyExistsFormat", id));
        }

        File.WriteAllText(
            targetPath,
            JsonSerializer.Serialize(normalized, AutoRelocationTemplateDefaults.JsonOptions),
            Encoding.UTF8);
        if (!string.IsNullOrWhiteSpace(previousPath) &&
            !string.Equals(previousPath, targetPath, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(previousPath))
        {
            File.Delete(previousPath);
        }

        return new AutoRelocationTemplateFile(normalized, targetPath);
    }

    public static bool DeleteTemplate(string templateId)
    {
        var id = NormalizeTemplateId(templateId);
        if (!IsValidTemplateId(id) ||
            string.Equals(id, AutoRelocationTemplateDefaults.DefaultTemplateId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = GetTemplateFilePath(id);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public static void EnsureTemplatesInitialized()
    {
        Directory.CreateDirectory(TemplateRootPath);
        var defaultTemplatePath = GetTemplateFilePath(AutoRelocationTemplateDefaults.DefaultTemplateId);
        if (!File.Exists(defaultTemplatePath))
        {
            SaveTemplate(AutoRelocationTemplateDefaults.CreateDefaultTemplate());
        }
    }

    public static string NormalizeTemplateId(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.EndsWith(AutoRelocationTemplateDefaults.TemplateFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^AutoRelocationTemplateDefaults.TemplateFileExtension.Length].Trim();
        }

        return normalized;
    }

    public static bool IsValidTemplateId(string? value)
    {
        var normalized = NormalizeTemplateId(value);
        return normalized.Length > 0 &&
            normalized.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            !string.Equals(normalized, ".", StringComparison.Ordinal) &&
            !string.Equals(normalized, "..", StringComparison.Ordinal);
    }

    public static string CreateUniqueTemplateId(string displayName, IEnumerable<string> existingIds)
    {
        var baseId = NormalizeTemplateId(displayName);
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "New template";
        }

        var existingSet = existingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingSet.Contains(baseId))
        {
            return baseId;
        }

        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{baseId} {index}";
            if (!existingSet.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{baseId} {DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private static string GetTemplateFilePath(string templateId)
    {
        return Path.Combine(
            TemplateRootPath,
            NormalizeTemplateId(templateId) + AutoRelocationTemplateDefaults.TemplateFileExtension);
    }

    private static AutoRelocationTemplateDocument NormalizeDocument(
        AutoRelocationTemplateDocument document,
        string id)
    {
        if (IsLegacyDefaultDocument(document, id))
        {
            document = AutoRelocationTemplateDefaults.CreateDefaultTemplate();
        }

        return new AutoRelocationTemplateDocument
        {
            SchemaVersion = AutoRelocationTemplateDefaults.SchemaVersion,
            Id = NormalizeTemplateId(id),
            DisplayName = string.IsNullOrWhiteSpace(document.DisplayName)
                ? NormalizeTemplateId(id)
                : document.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(document.Description) ? null : document.Description.Trim(),
            Prefilters = document.Prefilters.Select(static rule => new AutoRelocationPrefilterRule
            {
                Enabled = rule.Enabled,
                Source = AutoRelocationTemplateDefaults.NormalizeValueSource(rule.Source),
                Operator = rule.Operator,
                Value = rule.Value.Trim(),
                Action = rule.Action,
                TargetFolderName = string.IsNullOrWhiteSpace(rule.TargetFolderName)
                    ? null
                    : rule.TargetFolderName.Trim()
            }).ToList(),
            PathRules = document.PathRules.Select(static rule => new AutoRelocationPathRule
            {
                Enabled = rule.Enabled,
                Source = AutoRelocationTemplateDefaults.NormalizeValueSource(rule.Source),
                Transform = rule.Transform,
                Language = rule.Language,
                Format = string.IsNullOrWhiteSpace(rule.Format) ? "{value}" : rule.Format.Trim(),
                FallbackFolderName = string.IsNullOrWhiteSpace(rule.FallbackFolderName)
                    ? "[ETC]"
                    : rule.FallbackFolderName.Trim(),
                Options = rule.Options
            }).ToList()
        };
    }

    private static bool IsLegacyDefaultDocument(AutoRelocationTemplateDocument document, string id)
    {
        return document.SchemaVersion < AutoRelocationTemplateDefaults.SchemaVersion &&
            string.Equals(NormalizeTemplateId(id), AutoRelocationTemplateDefaults.DefaultTemplateId, StringComparison.OrdinalIgnoreCase) &&
            document.Prefilters.Any(static rule =>
                rule.Source == AutoRelocationValueSource.Tags &&
                rule.Value.Contains("직번", StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class AutoRelocationPlanBuilder
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly string[] KoreanInitials =
    [
        "ㄱ", "ㄲ", "ㄴ", "ㄷ", "ㄸ", "ㄹ", "ㅁ", "ㅂ", "ㅃ", "ㅅ",
        "ㅆ", "ㅇ", "ㅈ", "ㅉ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ"
    ];

    public AutoRelocationPlanBuildResult Build(
        string rootFolderPath,
        AutoRelocationTemplateDocument template,
        IEnumerable<AutoRelocationItemContext> items)
    {
        var rootFolder = NormalizeDirectoryPath(rootFolderPath);
        var planItems = new List<AutoRelocationPlanItem>();
        var excludedCount = 0;

        foreach (var item in items)
        {
            var sourcePath = Path.GetFullPath(item.SourcePath);
            var prefilter = template.Prefilters
                .Where(static rule => rule.Enabled)
                .FirstOrDefault(rule => MatchesPrefilter(rule, item));

            if (prefilter is not null)
            {
                if (prefilter.Action == AutoRelocationPrefilterAction.Exclude)
                {
                    excludedCount++;
                    continue;
                }

                if (prefilter.Action == AutoRelocationPrefilterAction.ReviewOnly)
                {
                    planItems.Add(new AutoRelocationPlanItem(
                        sourcePath,
                        Path.Combine(rootFolder, Path.GetFileName(sourcePath)),
                        RequiresReview: true,
                        CreateTargetFolder: false));
                    continue;
                }

                if (prefilter.Action == AutoRelocationPrefilterAction.RouteToFolder)
                {
                    var targetFolder = string.IsNullOrWhiteSpace(prefilter.TargetFolderName)
                        ? rootFolder
                        : Path.Combine(rootFolder, MakeSafeFolderSegment(prefilter.TargetFolderName));
                    planItems.Add(new AutoRelocationPlanItem(
                        sourcePath,
                        Path.Combine(targetFolder, Path.GetFileName(sourcePath)),
                        RequiresReview: false,
                        CreateTargetFolder: !PathComparer.Equals(targetFolder, rootFolder)));
                    continue;
                }
            }

            var relocationFolder = BuildRelocationFolder(rootFolder, template.PathRules, item);
            planItems.Add(new AutoRelocationPlanItem(
                sourcePath,
                Path.Combine(relocationFolder, Path.GetFileName(sourcePath)),
                RequiresReview: false,
                CreateTargetFolder: !PathComparer.Equals(relocationFolder, rootFolder)));
        }

        return new AutoRelocationPlanBuildResult(planItems, excludedCount);
    }

    private static string BuildRelocationFolder(
        string rootFolder,
        IEnumerable<AutoRelocationPathRule> rules,
        AutoRelocationItemContext item)
    {
        var folder = rootFolder;
        foreach (var rule in rules.Where(static rule => rule.Enabled))
        {
            var segment = BuildPathSegment(rule, item);
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            folder = Path.Combine(folder, segment);
        }

        return folder;
    }

    private static string BuildPathSegment(AutoRelocationPathRule rule, AutoRelocationItemContext item)
    {
        var value = ResolveValue(rule.Source, item);
        var transformed = TransformValue(value, rule.Transform, rule.Language, rule.Options).Trim();
        if (string.IsNullOrWhiteSpace(transformed))
        {
            return string.IsNullOrWhiteSpace(rule.FallbackFolderName)
                ? string.Empty
                : MakeSafeFolderSegment(rule.FallbackFolderName);
        }

        var formatted = ApplyFolderFormat(rule.Format, transformed);
        return string.IsNullOrWhiteSpace(formatted)
            ? string.Empty
            : MakeSafeFolderSegment(formatted);
    }

    private static string ApplyFolderFormat(string? format, string value)
    {
        var normalizedFormat = string.IsNullOrWhiteSpace(format) ? "{value}" : format.Trim();
        if (normalizedFormat.Contains("{value}", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedFormat.Replace("{value}", value, StringComparison.OrdinalIgnoreCase);
        }

        if (normalizedFormat.Contains("값", StringComparison.Ordinal))
        {
            return normalizedFormat.Replace("값", value, StringComparison.Ordinal);
        }

        return normalizedFormat;
    }

    private static bool MatchesPrefilter(AutoRelocationPrefilterRule rule, AutoRelocationItemContext item)
    {
        var value = ResolveValue(rule.Source, item).Text.Trim();
        var operand = rule.Value.Trim();
        return rule.Operator switch
        {
            AutoRelocationFilterOperator.Contains => value.Contains(operand, StringComparison.OrdinalIgnoreCase),
            AutoRelocationFilterOperator.Equals => string.Equals(value, operand, StringComparison.OrdinalIgnoreCase),
            AutoRelocationFilterOperator.StartsWith => value.StartsWith(operand, StringComparison.OrdinalIgnoreCase),
            AutoRelocationFilterOperator.EndsWith => value.EndsWith(operand, StringComparison.OrdinalIgnoreCase),
            AutoRelocationFilterOperator.Regex => MatchesRegex(value, operand),
            AutoRelocationFilterOperator.IsEmpty => string.IsNullOrWhiteSpace(value),
            AutoRelocationFilterOperator.IsNotEmpty => !string.IsNullOrWhiteSpace(value),
            _ => false
        };
    }

    private static bool MatchesRegex(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static AutoRelocationResolvedValue ResolveValue(AutoRelocationValueSource source, AutoRelocationItemContext item)
    {
        var fileStem = GetProperty(item, "fileNameStem") ?? GetFileNameStem(item.SourcePath);
        return source switch
        {
            AutoRelocationValueSource.FileName => new AutoRelocationResolvedValue(fileStem),
            AutoRelocationValueSource.FileExtension => new AutoRelocationResolvedValue(
                GetProperty(item, "fileExtension") ?? GetFileExtension(item.SourcePath)),
            AutoRelocationValueSource.KnownFileKind => new AutoRelocationResolvedValue(
                GetProperty(item, "knownFileKind") ??
                GetProperty(item, "fileKind") ??
                GetProperty(item, "fileType") ??
                AutoRelocationFileTypeClassifier.GetKnownFileKind(item.SourcePath)),
            AutoRelocationValueSource.FileType => new AutoRelocationResolvedValue(
                GetProperty(item, "fileType") ?? AutoRelocationFileTypeClassifier.GetKnownFileKind(item.SourcePath)),
            AutoRelocationValueSource.Title => new AutoRelocationResolvedValue(GetProperty(item, "title") ?? fileStem),
            AutoRelocationValueSource.EpisodeRange => CreateEpisodeRangeValue(GetProperty(item, "episodeRange") ?? ""),
            AutoRelocationValueSource.OriginalTitle => new AutoRelocationResolvedValue(
                GetProperty(item, "originalTitle") ?? GetProperty(item, "title") ?? fileStem),
            AutoRelocationValueSource.Author => new AutoRelocationResolvedValue(GetProperty(item, "author") ?? ""),
            AutoRelocationValueSource.Tags => new AutoRelocationResolvedValue(GetProperty(item, "tags") ?? ""),
            AutoRelocationValueSource.SeriesStatus => new AutoRelocationResolvedValue(GetProperty(item, "seriesStatus") ?? ""),
            AutoRelocationValueSource.SizeBytes => new AutoRelocationResolvedValue(
                item.SizeBytes.ToString(CultureInfo.InvariantCulture),
                item.SizeBytes,
                null),
            AutoRelocationValueSource.ImageCount => new AutoRelocationResolvedValue(
                item.ImageCount.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                item.ImageCount,
                null),
            AutoRelocationValueSource.ModifiedAt => new AutoRelocationResolvedValue(
                FormatDateValue(item.ModifiedAt),
                null,
                item.ModifiedAt),
            AutoRelocationValueSource.CreatedAt => new AutoRelocationResolvedValue(
                FormatDateValue(item.CreatedAt),
                null,
                item.CreatedAt),
            _ => new AutoRelocationResolvedValue("")
        };
    }

    private static string GetFileNameStem(string path)
    {
        return Directory.Exists(path)
            ? Path.GetFileName(path)
            : Path.GetFileNameWithoutExtension(path);
    }

    private static string GetFileExtension(string path)
    {
        return File.Exists(path) ? Path.GetExtension(path).TrimStart('.') : "";
    }

    private static AutoRelocationResolvedValue CreateEpisodeRangeValue(string value)
    {
        var text = value.Trim();
        return new AutoRelocationResolvedValue(text, TryParseFirstNumber(text), null);
    }

    private static string TransformValue(
        AutoRelocationResolvedValue value,
        AutoRelocationValueTransform transform,
        AutoRelocationLanguageProfile language,
        AutoRelocationTransformOptions options)
    {
        return transform switch
        {
            AutoRelocationValueTransform.Full => value.Text.Trim(),
            AutoRelocationValueTransform.InitialBucket => GetInitialBucket(value.Text, language),
            AutoRelocationValueTransform.FirstCharacters => GetFirstTextElements(
                value.Text,
                Math.Max(1, options.CharacterCount ?? 1)),
            AutoRelocationValueTransform.NumberRange => GetNumberRangeLabel(value.Number ?? TryParseNumber(value.Text), options),
            AutoRelocationValueTransform.NumberFloor => GetNumberStepLabel(
                value.Number ?? TryParseNumber(value.Text),
                options,
                roundUp: false),
            AutoRelocationValueTransform.NumberCeiling => GetNumberStepLabel(
                value.Number ?? TryParseNumber(value.Text),
                options,
                roundUp: true),
            AutoRelocationValueTransform.DatePart => GetDatePartLabel(
                value.Date ?? TryParseDate(value.Text),
                options.DatePart ?? AutoRelocationDatePart.Year),
            _ => value.Text.Trim()
        };
    }

    private static string GetInitialBucket(string value, AutoRelocationLanguageProfile language)
    {
        var first = value.Trim().FirstOrDefault();
        if (first == default)
        {
            return string.Empty;
        }

        var profile = language == AutoRelocationLanguageProfile.Auto
            ? AutoRelocationLanguageProfile.KoreanEnglish
            : language;

        var koreanInitial = GetKoreanInitial(first);
        if (koreanInitial is not null &&
            profile is AutoRelocationLanguageProfile.Korean or AutoRelocationLanguageProfile.KoreanEnglish)
        {
            return koreanInitial;
        }

        if (IsAsciiEnglishLetter(first) &&
            profile is AutoRelocationLanguageProfile.English or AutoRelocationLanguageProfile.KoreanEnglish)
        {
            return char.ToUpperInvariant(first).ToString();
        }

        if (char.IsDigit(first) && profile == AutoRelocationLanguageProfile.KoreanEnglish)
        {
            return "0A";
        }

        if (profile is AutoRelocationLanguageProfile.Japanese or AutoRelocationLanguageProfile.Chinese)
        {
            return first.ToString();
        }

        return string.Empty;
    }

    private static string? GetKoreanInitial(char value)
    {
        const int hangulBase = 0xAC00;
        const int hangulLast = 0xD7A3;
        var code = value;
        if (code < hangulBase || code > hangulLast)
        {
            return null;
        }

        var initialIndex = (code - hangulBase) / (21 * 28);
        return initialIndex >= 0 && initialIndex < KoreanInitials.Length
            ? KoreanInitials[initialIndex]
            : null;
    }

    private static bool IsAsciiEnglishLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static string GetFirstTextElements(string value, int count)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var info = new StringInfo(trimmed);
        return info.SubstringByTextElements(0, Math.Min(count, info.LengthInTextElements));
    }

    private static string GetNumberRangeLabel(double? number, AutoRelocationTransformOptions options)
    {
        if (!number.HasValue)
        {
            return string.Empty;
        }

        var range = options.NumberRanges.FirstOrDefault(candidate =>
            (!candidate.Min.HasValue || number.Value >= candidate.Min.Value) &&
            (!candidate.Max.HasValue || number.Value <= candidate.Max.Value));
        return range is null ? string.Empty : range.Label.Trim();
    }

    private static string GetNumberStepLabel(double? number, AutoRelocationTransformOptions options, bool roundUp)
    {
        if (!number.HasValue)
        {
            return string.Empty;
        }

        var step = Math.Max(1, options.NumberStep ?? 1);
        var stepped = roundUp
            ? Math.Ceiling(number.Value / step) * step
            : Math.Floor(number.Value / step) * step;
        var value = FormatNumber(stepped);
        if (!string.IsNullOrWhiteSpace(options.NumberUnit))
        {
            value += options.NumberUnit.Trim();
        }

        var format = string.IsNullOrWhiteSpace(options.NumberLabelFormat)
            ? "{value}"
            : options.NumberLabelFormat.Trim();
        return format.Replace("{value}", value, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDatePartLabel(DateTime? date, AutoRelocationDatePart datePart)
    {
        if (!date.HasValue)
        {
            return string.Empty;
        }

        return datePart switch
        {
            AutoRelocationDatePart.Year => date.Value.ToString("yyyy", CultureInfo.InvariantCulture),
            AutoRelocationDatePart.YearMonth => date.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            AutoRelocationDatePart.Month => date.Value.ToString("MM", CultureInfo.InvariantCulture),
            AutoRelocationDatePart.Day => date.Value.ToString("dd", CultureInfo.InvariantCulture),
            _ => date.Value.ToString("yyyy", CultureInfo.InvariantCulture)
        };
    }

    private static double? TryParseNumber(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantNumber))
        {
            return invariantNumber;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var currentCultureNumber)
            ? currentCultureNumber
            : null;
    }

    private static double? TryParseFirstNumber(string value)
    {
        var match = Regex.Match(value, @"\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
        return match.Success ? TryParseNumber(match.Value) : null;
    }

    private static DateTime? TryParseDate(string value)
    {
        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var date)
            ? date
            : null;
    }

    private static string FormatNumber(double value)
    {
        return Math.Abs(value % 1) < double.Epsilon
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatDateValue(DateTime? date)
    {
        return date?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string? GetProperty(AutoRelocationItemContext item, string name)
    {
        if (item.Properties.TryGetValue(name, out var value))
        {
            return value;
        }

        return item.Properties
            .FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    private static string MakeSafeFolderSegment(string value)
    {
        return WindowsFileNameSafety.MakeSafeFileName(value.Trim());
    }

    private static string NormalizeDirectoryPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

internal sealed record AutoRelocationPlanBuildResult(
    IReadOnlyList<AutoRelocationPlanItem> Items,
    int ExcludedCount);

internal sealed record AutoRelocationPlanItem(
    string SourcePath,
    string TargetPath,
    bool RequiresReview,
    bool CreateTargetFolder);

internal sealed record AutoRelocationItemContext(
    string SourcePath,
    IReadOnlyDictionary<string, string?> Properties,
    long SizeBytes = 0,
    int? ImageCount = null,
    DateTime? ModifiedAt = null,
    DateTime? CreatedAt = null);

internal readonly record struct AutoRelocationResolvedValue(
    string Text,
    double? Number = null,
    DateTime? Date = null);
