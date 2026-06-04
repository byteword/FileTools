using System.Globalization;
using System.Text;

namespace FileTools;

internal enum NameTemplateEvaluationStatus
{
    Ready,
    MissingToken,
    InvalidTemplate,
    InvalidResult
}

internal sealed record NameTemplateEvaluationResult(
    NameTemplateEvaluationStatus Status,
    string Value,
    string? Reason = null)
{
    public bool IsReady => Status == NameTemplateEvaluationStatus.Ready;
}

internal sealed record NameTemplateToken(string Name, string? Format);

internal interface INameTemplateTokenProvider
{
    bool TryResolve(NameTemplateToken token, NameTemplateContext context, out string value);
}

internal sealed record NameTemplateContext
{
    public string? SourcePath { get; init; }
    public string? FileName { get; init; }
    public string? FileStem { get; init; }
    public string? Extension { get; init; }
    public string? ExtensionNoDot { get; init; }
    public string? FolderName { get; init; }
    public string? ParentFolderName { get; init; }
    public string? CommonStem { get; init; }
    public string? FirstFileStem { get; init; }
    public int? SelectedCount { get; init; }
    public string? TargetExtension { get; init; }
    public string? Stem { get; init; }
    public int? Index { get; init; }
    public string? IndexLabel { get; init; }

    public static NameTemplateContext FromFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(fileName);
        var parent = Path.GetDirectoryName(filePath);
        return new NameTemplateContext
        {
            SourcePath = filePath,
            FileName = fileName,
            FileStem = Path.GetFileNameWithoutExtension(fileName),
            Extension = extension,
            ExtensionNoDot = extension.TrimStart('.'),
            FolderName = string.IsNullOrWhiteSpace(parent) ? null : Path.GetFileName(parent),
            ParentFolderName = GetParentFolderName(parent)
        };
    }

    public static NameTemplateContext FromFolderChild(string folderPath, string childFileName)
    {
        var extension = Path.GetExtension(childFileName);
        return new NameTemplateContext
        {
            SourcePath = Path.Combine(folderPath, childFileName),
            FileName = childFileName,
            FileStem = Path.GetFileNameWithoutExtension(childFileName),
            Extension = extension,
            ExtensionNoDot = extension.TrimStart('.'),
            FolderName = Path.GetFileName(folderPath),
            ParentFolderName = GetParentFolderName(folderPath)
        };
    }

    public static NameTemplateContext FromFolderChildName(string folderName, string childFileName)
    {
        var extension = Path.GetExtension(childFileName);
        return new NameTemplateContext
        {
            FileName = childFileName,
            FileStem = Path.GetFileNameWithoutExtension(childFileName),
            Extension = extension,
            ExtensionNoDot = extension.TrimStart('.'),
            FolderName = folderName
        };
    }

    public static NameTemplateContext FromNameParts(string fileName, string stem, string extension)
    {
        return new NameTemplateContext
        {
            FileName = fileName,
            FileStem = stem,
            Stem = stem,
            Extension = extension,
            ExtensionNoDot = extension.TrimStart('.')
        };
    }

    private static string? GetParentFolderName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(parent) ? null : Path.GetFileName(parent);
    }
}

internal sealed class NameTemplateResolver
{
    public static NameTemplateResolver Default { get; } = CreateDefault(settings: null);

    private readonly IReadOnlyList<INameTemplateTokenProvider> _tokenProviders;

    public NameTemplateResolver(IEnumerable<INameTemplateTokenProvider> tokenProviders)
    {
        _tokenProviders = tokenProviders.ToArray();
    }

    public static NameTemplateResolver CreateDefault(FileToolsSettings? settings)
    {
        var providers = new List<INameTemplateTokenProvider>
        {
            new FileSystemNameTemplateTokenProvider(),
            new SelectionNameTemplateTokenProvider()
        };
        if (settings is not null)
        {
            providers.Add(new RenameCorrectionNameTemplateTokenProvider(settings));
        }

        return new NameTemplateResolver(providers);
    }

    public NameTemplateEvaluationResult Evaluate(string? template, NameTemplateContext context)
    {
        var normalizedTemplate = string.IsNullOrWhiteSpace(template) ? "{FileStem}" : template;
        var builder = new StringBuilder();

        for (var index = 0; index < normalizedTemplate.Length; index++)
        {
            var ch = normalizedTemplate[index];
            if (ch == '{')
            {
                if (index + 1 < normalizedTemplate.Length && normalizedTemplate[index + 1] == '{')
                {
                    builder.Append('{');
                    index++;
                    continue;
                }

                var endIndex = normalizedTemplate.IndexOf('}', index + 1);
                if (endIndex < 0)
                {
                    return new NameTemplateEvaluationResult(
                        NameTemplateEvaluationStatus.InvalidTemplate,
                        "",
                        "Template contains an unclosed token.");
                }

                var tokenText = normalizedTemplate[(index + 1)..endIndex].Trim();
                if (string.IsNullOrWhiteSpace(tokenText))
                {
                    return new NameTemplateEvaluationResult(
                        NameTemplateEvaluationStatus.InvalidTemplate,
                        "",
                        "Template contains an empty token.");
                }

                var token = ParseToken(tokenText);
                if (!TryResolve(token, context, out var resolvedValue))
                {
                    return new NameTemplateEvaluationResult(
                        NameTemplateEvaluationStatus.MissingToken,
                        "",
                        "Template token is unavailable: " + token.Name);
                }

                builder.Append(resolvedValue);
                index = endIndex;
                continue;
            }

            if (ch == '}')
            {
                if (index + 1 < normalizedTemplate.Length && normalizedTemplate[index + 1] == '}')
                {
                    builder.Append('}');
                    index++;
                    continue;
                }

                return new NameTemplateEvaluationResult(
                    NameTemplateEvaluationStatus.InvalidTemplate,
                    "",
                    "Template contains an unopened closing brace.");
            }

            builder.Append(ch);
        }

        var value = builder.ToString();
        return string.IsNullOrWhiteSpace(value)
            ? new NameTemplateEvaluationResult(
                NameTemplateEvaluationStatus.InvalidResult,
                "",
                "Template produced an empty name.")
            : new NameTemplateEvaluationResult(NameTemplateEvaluationStatus.Ready, value);
    }

    private bool TryResolve(NameTemplateToken token, NameTemplateContext context, out string value)
    {
        foreach (var provider in _tokenProviders)
        {
            if (provider.TryResolve(token, context, out value))
            {
                return true;
            }
        }

        value = "";
        return false;
    }

    private static NameTemplateToken ParseToken(string tokenText)
    {
        var separatorIndex = tokenText.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return new NameTemplateToken(tokenText, Format: null);
        }

        return new NameTemplateToken(
            tokenText[..separatorIndex].Trim(),
            tokenText[(separatorIndex + 1)..].Trim());
    }
}

internal sealed class FileSystemNameTemplateTokenProvider : INameTemplateTokenProvider
{
    public bool TryResolve(NameTemplateToken token, NameTemplateContext context, out string value)
    {
        var tokenName = token.Name.Trim();
        switch (tokenName.ToUpperInvariant())
        {
            case "FILENAME":
                return TryResolveText(context.FileName, out value);
            case "FILESTEM":
                return TryResolveText(context.FileStem, out value);
            case "STEM":
                return TryResolveText(context.Stem ?? context.FileStem, out value);
            case "EXTENSION":
                return TryResolveText(context.Extension, out value);
            case "EXTENSIONNODOT":
                return TryResolveText(context.ExtensionNoDot, out value);
            case "FOLDERNAME":
                return TryResolveText(context.FolderName, out value);
            case "PARENTFOLDERNAME":
                return TryResolveText(context.ParentFolderName, out value);
            case "TARGETEXTENSION":
                return TryResolveText(context.TargetExtension, out value);
            case "INDEX":
                return TryResolveNumber(context.Index, token.Format, out value);
            case "INDEXLABEL":
                return TryResolveText(context.IndexLabel, out value);
            default:
                value = "";
                return false;
        }
    }

    private static bool TryResolveText(string? text, out string value)
    {
        if (text is null)
        {
            value = "";
            return false;
        }

        value = text;
        return true;
    }

    private static bool TryResolveNumber(int? number, string? format, out string value)
    {
        if (number is null)
        {
            value = "";
            return false;
        }

        value = string.IsNullOrWhiteSpace(format)
            ? number.Value.ToString(CultureInfo.InvariantCulture)
            : number.Value.ToString(format, CultureInfo.InvariantCulture);
        return true;
    }
}

internal sealed class SelectionNameTemplateTokenProvider : INameTemplateTokenProvider
{
    public bool TryResolve(NameTemplateToken token, NameTemplateContext context, out string value)
    {
        switch (token.Name.Trim().ToUpperInvariant())
        {
            case "COMMONSTEM":
                return TryResolveText(context.CommonStem, out value);
            case "FIRSTFILESTEM":
                return TryResolveText(context.FirstFileStem, out value);
            case "SELECTEDCOUNT":
                return TryResolveNumber(context.SelectedCount, token.Format, out value);
            default:
                value = "";
                return false;
        }
    }

    private static bool TryResolveText(string? text, out string value)
    {
        if (text is null)
        {
            value = "";
            return false;
        }

        value = text;
        return true;
    }

    private static bool TryResolveNumber(int? number, string? format, out string value)
    {
        if (number is null)
        {
            value = "";
            return false;
        }

        value = string.IsNullOrWhiteSpace(format)
            ? number.Value.ToString(CultureInfo.InvariantCulture)
            : number.Value.ToString(format, CultureInfo.InvariantCulture);
        return true;
    }
}

internal sealed class RenameCorrectionNameTemplateTokenProvider : INameTemplateTokenProvider
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly FileToolsSettings _settings;
    private readonly Dictionary<string, RenamePreview?> _previewCache;
    private KoreanFileNameCorrector? _corrector;

    public RenameCorrectionNameTemplateTokenProvider(FileToolsSettings settings)
    {
        _settings = settings.Clone();
        _previewCache = new Dictionary<string, RenamePreview?>(PathComparer);
    }

    public bool TryResolve(NameTemplateToken token, NameTemplateContext context, out string value)
    {
        if (!TryGetPreview(context, out var preview))
        {
            value = "";
            return false;
        }

        if (preview.Status is RenamePreviewStatus.NeedsReview or RenamePreviewStatus.Conflict or RenamePreviewStatus.Skipped)
        {
            value = "";
            return false;
        }

        switch (token.Name.Trim().ToUpperInvariant())
        {
            case "CORRECTEDFILENAME":
                return TryResolveText(preview.SuggestedFileName, out value);
            case "CORRECTEDFILESTEM":
                return TryResolveText(Path.GetFileNameWithoutExtension(preview.SuggestedFileName), out value);
            case "TITLE":
                return TryResolveText(preview.Parts.Title, out value);
            case "EPISODERANGE":
                return TryResolveText(preview.Parts.EpisodeRange, out value);
            case "AUTHOR":
                return TryResolveText(preview.Parts.Author, out value);
            case "TAGS":
                return TryResolveText(string.Join(", ", preview.Parts.Tags), out value);
            default:
                value = "";
                return false;
        }
    }

    private bool TryGetPreview(NameTemplateContext context, out RenamePreview preview)
    {
        var sourcePath = context.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            sourcePath = context.FileName;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            preview = null!;
            return false;
        }

        if (!_previewCache.TryGetValue(sourcePath, out var cached))
        {
            try
            {
                _corrector ??= CreateFileNameCorrector(_settings);
                cached = _corrector.CreatePreview(sourcePath);
            }
            catch
            {
                cached = null;
            }

            _previewCache[sourcePath] = cached;
        }

        preview = cached!;
        return preview is not null;
    }

    private static KoreanFileNameCorrector CreateFileNameCorrector(FileToolsSettings settings)
    {
        var dictionary = RenameDictionaryStore.Load();
        var rules = RenameRuleStore.Load();
        return new KoreanFileNameCorrector(new CorrectionOptions
        {
            RenameDictionary = settings.RenameUseDictionary ? dictionary.Replacements : [],
            CommonPhrases = settings.RenameUseDictionary ? dictionary.CommonPhrases.ToArray() : [],
            Rules = rules.Rules
        });
    }

    private static bool TryResolveText(string? text, out string value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = "";
            return false;
        }

        value = text;
        return true;
    }
}

internal enum NameCollisionPolicy
{
    Skip,
    AutoNumber,
    Ask,
    MergeIntoExisting
}

internal enum NameCollisionTargetKind
{
    File,
    Folder
}

internal enum ConflictIndexStyle
{
    Number,
    ZeroPadded3,
    Roman,
    KoreanNumber,
    KoreanHeavenlyStem,
    Alphabet
}

internal enum NameCollisionStatus
{
    Ready,
    Skipped,
    RequiresInteraction,
    CannotResolve
}

internal sealed record NameCollisionOptions
{
    public NameCollisionPolicy Policy { get; init; } = NameCollisionPolicy.Skip;
    public NameCollisionTargetKind TargetKind { get; init; } = NameCollisionTargetKind.File;
    public string ConflictNameTemplate { get; init; } = NameTemplateDefaults.DefaultConflictNameTemplate;
    public ConflictIndexStyle IndexStyle { get; init; } = ConflictIndexStyle.Number;
    public int FirstConflictIndex { get; init; } = 2;
    public int MaxIndexExclusive { get; init; } = 10_000;
}

internal sealed record NameCollisionResult(
    NameCollisionStatus Status,
    string TargetName,
    string TargetPath,
    bool HadCollision,
    string? Reason = null)
{
    public bool IsReady => Status == NameCollisionStatus.Ready;
}

internal static class NameCollisionResolver
{
    public static NameCollisionResult Resolve(
        string directory,
        string desiredName,
        NameCollisionOptions? options = null)
    {
        options ??= new NameCollisionOptions();
        var safeDesiredName = WindowsFileNameSafety.MakeSafeFileName(desiredName);
        var targetPath = Path.Combine(directory, safeDesiredName);
        var existingState = GetExistingState(targetPath, options.TargetKind);

        if (existingState == NameCollisionExistingState.None)
        {
            return new NameCollisionResult(
                NameCollisionStatus.Ready,
                safeDesiredName,
                targetPath,
                HadCollision: false);
        }

        if (options.Policy == NameCollisionPolicy.MergeIntoExisting &&
            existingState == NameCollisionExistingState.MatchingKind)
        {
            return new NameCollisionResult(
                NameCollisionStatus.Ready,
                safeDesiredName,
                targetPath,
                HadCollision: true);
        }

        if (options.Policy == NameCollisionPolicy.Ask)
        {
            return new NameCollisionResult(
                NameCollisionStatus.RequiresInteraction,
                safeDesiredName,
                targetPath,
                HadCollision: true,
                "Name collision requires user interaction.");
        }

        if (options.Policy != NameCollisionPolicy.AutoNumber)
        {
            return new NameCollisionResult(
                NameCollisionStatus.Skipped,
                safeDesiredName,
                targetPath,
                HadCollision: true,
                "Target name already exists.");
        }

        var (stem, extension) = SplitName(safeDesiredName, options.TargetKind);
        for (var index = Math.Max(1, options.FirstConflictIndex); index < options.MaxIndexExclusive; index++)
        {
            var indexLabel = ConflictIndexFormatter.Format(index, options.IndexStyle);
            var context = NameTemplateContext.FromNameParts(safeDesiredName, stem, extension) with
            {
                Index = index,
                IndexLabel = indexLabel
            };
            var evaluation = NameTemplateResolver.Default.Evaluate(options.ConflictNameTemplate, context);
            var candidateName = evaluation.IsReady
                ? WindowsFileNameSafety.MakeSafeFileName(evaluation.Value)
                : WindowsFileNameSafety.MakeSafeFileName($"{stem} ({index}){extension}");
            var candidatePath = Path.Combine(directory, candidateName);
            if (GetExistingState(candidatePath, options.TargetKind) == NameCollisionExistingState.None)
            {
                return new NameCollisionResult(
                    NameCollisionStatus.Ready,
                    candidateName,
                    candidatePath,
                    HadCollision: true);
            }
        }

        return new NameCollisionResult(
            NameCollisionStatus.CannotResolve,
            safeDesiredName,
            targetPath,
            HadCollision: true,
            "Could not resolve name collision.");
    }

    private static (string Stem, string Extension) SplitName(string name, NameCollisionTargetKind targetKind)
    {
        if (targetKind == NameCollisionTargetKind.Folder)
        {
            return (name, "");
        }

        return (Path.GetFileNameWithoutExtension(name), Path.GetExtension(name));
    }

    private static NameCollisionExistingState GetExistingState(string path, NameCollisionTargetKind targetKind)
    {
        var fileExists = File.Exists(path);
        var directoryExists = Directory.Exists(path);
        if (!fileExists && !directoryExists)
        {
            return NameCollisionExistingState.None;
        }

        return targetKind switch
        {
            NameCollisionTargetKind.File when fileExists => NameCollisionExistingState.MatchingKind,
            NameCollisionTargetKind.Folder when directoryExists => NameCollisionExistingState.MatchingKind,
            _ => NameCollisionExistingState.OtherKind
        };
    }

    private enum NameCollisionExistingState
    {
        None,
        MatchingKind,
        OtherKind
    }
}

internal static class ConflictIndexFormatter
{
    private static readonly string[] KoreanNumbers =
    [
        "", "하나", "둘", "셋", "넷", "다섯", "여섯", "일곱", "여덟", "아홉", "열"
    ];

    private static readonly string[] KoreanHeavenlyStems =
    [
        "갑", "을", "병", "정", "무", "기", "경", "신", "임", "계"
    ];

    public static string Format(int index, ConflictIndexStyle style)
    {
        return style switch
        {
            ConflictIndexStyle.ZeroPadded3 => index.ToString("000", CultureInfo.InvariantCulture),
            ConflictIndexStyle.Roman => ToRoman(index),
            ConflictIndexStyle.KoreanNumber => ToKoreanNumber(index),
            ConflictIndexStyle.KoreanHeavenlyStem => ToKoreanHeavenlyStem(index),
            ConflictIndexStyle.Alphabet => ToAlphabet(index),
            _ => index.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static string ToKoreanNumber(int index)
    {
        return index >= 1 && index < KoreanNumbers.Length
            ? KoreanNumbers[index]
            : index.ToString(CultureInfo.InvariantCulture);
    }

    private static string ToKoreanHeavenlyStem(int index)
    {
        if (index <= 0)
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }

        return KoreanHeavenlyStems[(index - 1) % KoreanHeavenlyStems.Length];
    }

    private static string ToAlphabet(int index)
    {
        if (index <= 0)
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }

        var value = index;
        var builder = new StringBuilder();
        while (value > 0)
        {
            value--;
            builder.Insert(0, (char)('A' + value % 26));
            value /= 26;
        }

        return builder.ToString();
    }

    private static string ToRoman(int index)
    {
        if (index <= 0 || index > 3999)
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }

        ReadOnlySpan<(int Value, string Label)> map =
        [
            (1000, "M"),
            (900, "CM"),
            (500, "D"),
            (400, "CD"),
            (100, "C"),
            (90, "XC"),
            (50, "L"),
            (40, "XL"),
            (10, "X"),
            (9, "IX"),
            (5, "V"),
            (4, "IV"),
            (1, "I")
        ];

        var value = index;
        var builder = new StringBuilder();
        foreach (var item in map)
        {
            while (value >= item.Value)
            {
                builder.Append(item.Label);
                value -= item.Value;
            }
        }

        return builder.ToString();
    }
}

internal static class NameTemplateDefaults
{
    public const string FolderWrapFolderNameTemplate = "{FileStem}";
    public const string FolderUnwrapKeepFileNameTemplate = "{FileName}";
    public const string FolderUnwrapUseFolderNameTemplate = "{FolderName}{Extension}";
    public const string FolderUnwrapPrefixFolderNameTemplate = "{FolderName}-{FileStem}{Extension}";
    public const string DefaultConflictNameTemplate = "{Stem} ({Index}){Extension}";
    public const string MultiFileMergeFolderNameTemplate = "{CommonStem}";
    public const string MultiFolderMergeFolderNameTemplate = "{CommonStem}";
    public const string ArchiveMergeFileNameTemplate = "{CommonStem}{TargetExtension}";
}

internal static class FolderStructureNameTemplates
{
    public static string ResolveWrapFolderName(string filePath, FileToolsSettings? settings = null)
    {
        var context = NameTemplateContext.FromFile(filePath);
        return ResolveSafeName(
            settings?.FolderWrapFolderNameTemplate ?? NameTemplateDefaults.FolderWrapFolderNameTemplate,
            context,
            Path.GetFileNameWithoutExtension(filePath),
            settings);
    }

    public static string ResolveUnwrappedFileNameFromFolderPath(
        string folderPath,
        string fileName,
        FolderUnwrapNameMismatchMode mismatchMode,
        FileToolsSettings? settings = null)
    {
        var folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrWhiteSpace(folderName))
        {
            folderName = folderPath;
        }

        return ResolveUnwrappedFileName(
            folderName,
            fileName,
            mismatchMode,
            NameTemplateContext.FromFolderChild(folderPath, fileName),
            settings);
    }

    public static string ResolveUnwrappedFileName(
        string folderName,
        string fileName,
        FolderUnwrapNameMismatchMode mismatchMode,
        FileToolsSettings? settings = null)
    {
        return ResolveUnwrappedFileName(
            folderName,
            fileName,
            mismatchMode,
            NameTemplateContext.FromFolderChildName(folderName, fileName),
            settings);
    }

    private static string ResolveUnwrappedFileName(
        string folderName,
        string fileName,
        FolderUnwrapNameMismatchMode mismatchMode,
        NameTemplateContext context,
        FileToolsSettings? settings)
    {
        var fileStem = Path.GetFileNameWithoutExtension(fileName);
        if (string.Equals(folderName, fileStem, StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        var template = mismatchMode switch
        {
            FolderUnwrapNameMismatchMode.UseFolderName => NameTemplateDefaults.FolderUnwrapUseFolderNameTemplate,
            FolderUnwrapNameMismatchMode.PrefixFolderName => NameTemplateDefaults.FolderUnwrapPrefixFolderNameTemplate,
            FolderUnwrapNameMismatchMode.CustomTemplate =>
                settings?.FolderUnwrapMismatchFileNameTemplate ??
                NameTemplateDefaults.FolderUnwrapPrefixFolderNameTemplate,
            _ => NameTemplateDefaults.FolderUnwrapKeepFileNameTemplate
        };

        return ResolveSafeName(template, context, fileName, settings);
    }

    private static string ResolveSafeName(
        string template,
        NameTemplateContext context,
        string fallback,
        FileToolsSettings? settings)
    {
        var evaluation = NameTemplateResolver.CreateDefault(settings).Evaluate(template, context);
        var name = evaluation.IsReady ? evaluation.Value : fallback;
        return WindowsFileNameSafety.MakeSafeFileName(name);
    }
}

internal static class FolderStructureCollisionOptions
{
    public static NameCollisionOptions Create(FileToolsSettings settings, NameCollisionTargetKind targetKind)
    {
        return new NameCollisionOptions
        {
            Policy = NormalizePolicy(settings.FolderStructureConflictPolicy),
            TargetKind = targetKind,
            ConflictNameTemplate = settings.FolderStructureConflictNameTemplate,
            IndexStyle = settings.FolderStructureConflictIndexStyle
        };
    }

    private static NameCollisionPolicy NormalizePolicy(NameCollisionPolicy policy)
    {
        return policy == NameCollisionPolicy.MergeIntoExisting
            ? NameCollisionPolicy.Skip
            : policy;
    }
}

internal static class NameTemplateText
{
    public static string GetDisplayName(NameCollisionPolicy policy)
    {
        return policy switch
        {
            NameCollisionPolicy.Skip => Localizer.Get("NameCollisionPolicySkip"),
            NameCollisionPolicy.AutoNumber => Localizer.Get("NameCollisionPolicyAutoNumber"),
            NameCollisionPolicy.Ask => Localizer.Get("NameCollisionPolicyAsk"),
            NameCollisionPolicy.MergeIntoExisting => Localizer.Get("NameCollisionPolicyMergeIntoExisting"),
            _ => policy.ToString()
        };
    }

    public static string GetDisplayName(ConflictIndexStyle style)
    {
        return style switch
        {
            ConflictIndexStyle.Number => Localizer.Get("ConflictIndexStyleNumber"),
            ConflictIndexStyle.ZeroPadded3 => Localizer.Get("ConflictIndexStyleZeroPadded3"),
            ConflictIndexStyle.Roman => Localizer.Get("ConflictIndexStyleRoman"),
            ConflictIndexStyle.KoreanNumber => Localizer.Get("ConflictIndexStyleKoreanNumber"),
            ConflictIndexStyle.KoreanHeavenlyStem => Localizer.Get("ConflictIndexStyleKoreanHeavenlyStem"),
            ConflictIndexStyle.Alphabet => Localizer.Get("ConflictIndexStyleAlphabet"),
            _ => style.ToString()
        };
    }
}
