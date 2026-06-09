using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

internal sealed class FileToolsSettings
{
    public FolderStructureOperation FolderStructureOperation { get; set; } = FolderStructureOperation.Auto;

    public FolderUnwrapNameMismatchMode FolderUnwrapNameMismatchMode { get; set; } = FolderUnwrapNameMismatchMode.KeepFileName;

    public string FolderWrapFolderNameTemplate { get; set; } = NameTemplateDefaults.FolderWrapFolderNameTemplate;

    public string FolderUnwrapMismatchFileNameTemplate { get; set; } =
        NameTemplateDefaults.FolderUnwrapPrefixFolderNameTemplate;

    public NameCollisionPolicy FolderStructureConflictPolicy { get; set; } = NameCollisionPolicy.Skip;

    public string FolderStructureConflictNameTemplate { get; set; } = NameTemplateDefaults.DefaultConflictNameTemplate;

    public ConflictIndexStyle FolderStructureConflictIndexStyle { get; set; } = ConflictIndexStyle.Number;

    public string AutoRelocationTemplateId { get; set; } = AutoRelocationTemplateDefaults.DefaultTemplateId;

    public string? AutoRelocationTargetRootPath { get; set; }

    public List<FileKindExtensionRule> FileKindExtensionRules { get; set; } =
        AutoRelocationFileTypeClassifier.CreateDefaultExtensionRules().ToList();

    public bool RegisterContextMenu { get; set; } = true;

    public ContextMenuLayout ContextMenuLayout { get; set; } = ContextMenuLayout.Grouped;

    public bool ContextMenuOpenApp { get; set; } = true;

    public bool ContextMenuFileNameCorrection { get; set; } = true;

    public bool ContextMenuFolderStructure { get; set; } = true;

    public bool ContextMenuFolderWrapFiles { get; set; } = true;

    public bool ContextMenuFolderUnwrapSameNameSingleFile { get; set; } = true;

    public bool ContextMenuFolderUnwrapSingleFile { get; set; } = true;

    public bool ContextMenuFolderMoveInnerFilesUp { get; set; } = true;

    public bool ContextMenuFolderMergeSelectedTargets { get; set; } = true;

    public bool ContextMenuAutoRelocation { get; set; } = true;

    public bool ContextMenuAutoRelocationCurrentFolder { get; set; } = true;

    public bool ContextMenuAutoRelocationChooseTarget { get; set; } = true;

    public RenameReviewMode RenameReviewMode { get; set; } = RenameReviewMode.Always;

    public bool RenameUseDictionary { get; set; } = true;

    public RenameCorrectionPluginOptions RenameCorrectionPlugins { get; set; } = new();

    public bool RenamePatternLearningEnabled { get; set; } = true;

    public int RenamePatternFeedbackLimit { get; set; } = FileNamePatternFeedbackStore.DefaultFeedbackLimit;

    public ArchiveMergeLayout ArchiveMergeLayout { get; set; } = ArchiveMergeLayout.GroupByArchiveName;

    public ArchiveMergeCollisionPolicy ArchiveMergeCollisionPolicy { get; set; } = ArchiveMergeCollisionPolicy.AutoNumber;

    public ArchiveMergeDuplicatePolicy ArchiveMergeDuplicatePolicy { get; set; } = ArchiveMergeDuplicatePolicy.KeepBoth;

    public ArchiveMergeFailurePolicy ArchiveMergeFailurePolicy { get; set; } = ArchiveMergeFailurePolicy.AbortAll;

    public ArchiveMergeOutputNamePolicy ArchiveMergeOutputNamePolicy { get; set; } = ArchiveMergeOutputNamePolicy.CommonStem;

    public ArchiveMergeCompressionLevel ArchiveMergeCompressionLevel { get; set; } = ArchiveMergeCompressionLevel.Default;

    public bool ArchiveMergeDeleteOriginals { get; set; }

    public bool ContextMenuArchiveMergeGroupByArchiveName { get; set; } = true;

    public bool ContextMenuArchiveMergePreserveInternalPaths { get; set; } = true;

    public FileCompareOptions FileCompareOptions { get; set; } = new();

    public bool IsContextMenuToolEnabled(ToolMode mode)
    {
        return mode switch
        {
            ToolMode.FileNameCorrection => ContextMenuFileNameCorrection,
            ToolMode.FolderStructure => ContextMenuFolderStructure && IsAnyContextMenuFolderOperationEnabled,
            ToolMode.AutoRelocation => ContextMenuAutoRelocation && IsAnyContextMenuAutoRelocationOperationEnabled,
            ToolMode.ArchiveMerge => IsAnyContextMenuArchiveMergeOperationEnabled,
            _ => false
        };
    }

    public bool IsAnyContextMenuFolderOperationEnabled =>
        ContextMenuFolderWrapFiles ||
        ContextMenuFolderUnwrapSameNameSingleFile ||
        ContextMenuFolderUnwrapSingleFile ||
        ContextMenuFolderMoveInnerFilesUp ||
        ContextMenuFolderMergeSelectedTargets;

    public bool IsAnyContextMenuAutoRelocationOperationEnabled =>
        ContextMenuAutoRelocationCurrentFolder ||
        ContextMenuAutoRelocationChooseTarget;

    public bool IsAnyContextMenuArchiveMergeOperationEnabled =>
        ContextMenuArchiveMergeGroupByArchiveName ||
        ContextMenuArchiveMergePreserveInternalPaths;

    public FileToolsSettings Clone()
    {
        return new FileToolsSettings
        {
            FolderStructureOperation = FolderStructureOperation,
            FolderUnwrapNameMismatchMode = FolderUnwrapNameMismatchMode,
            FolderWrapFolderNameTemplate = FolderWrapFolderNameTemplate,
            FolderUnwrapMismatchFileNameTemplate = FolderUnwrapMismatchFileNameTemplate,
            FolderStructureConflictPolicy = FolderStructureConflictPolicy,
            FolderStructureConflictNameTemplate = FolderStructureConflictNameTemplate,
            FolderStructureConflictIndexStyle = FolderStructureConflictIndexStyle,
            AutoRelocationTemplateId = AutoRelocationTemplateId,
            AutoRelocationTargetRootPath = AutoRelocationTargetRootPath,
            FileKindExtensionRules = AutoRelocationFileTypeClassifier
                .NormalizeExtensionRules(FileKindExtensionRules)
                .Select(static rule => new FileKindExtensionRule
                {
                    Kind = rule.Kind,
                    Extensions = rule.Extensions.ToList()
                })
                .ToList(),
            RegisterContextMenu = RegisterContextMenu,
            ContextMenuLayout = ContextMenuLayout,
            ContextMenuOpenApp = ContextMenuOpenApp,
            ContextMenuFileNameCorrection = ContextMenuFileNameCorrection,
            ContextMenuFolderStructure = ContextMenuFolderStructure,
            ContextMenuFolderWrapFiles = ContextMenuFolderWrapFiles,
            ContextMenuFolderUnwrapSameNameSingleFile = ContextMenuFolderUnwrapSameNameSingleFile,
            ContextMenuFolderUnwrapSingleFile = ContextMenuFolderUnwrapSingleFile,
            ContextMenuFolderMoveInnerFilesUp = ContextMenuFolderMoveInnerFilesUp,
            ContextMenuFolderMergeSelectedTargets = ContextMenuFolderMergeSelectedTargets,
            ContextMenuAutoRelocation = ContextMenuAutoRelocation,
            ContextMenuAutoRelocationCurrentFolder = ContextMenuAutoRelocationCurrentFolder,
            ContextMenuAutoRelocationChooseTarget = ContextMenuAutoRelocationChooseTarget,
            RenameReviewMode = RenameReviewMode,
            RenameUseDictionary = RenameUseDictionary,
            RenameCorrectionPlugins = RenameCorrectionPlugins?.Clone() ?? new RenameCorrectionPluginOptions(),
            RenamePatternLearningEnabled = RenamePatternLearningEnabled,
            RenamePatternFeedbackLimit = RenamePatternFeedbackLimit,
            ArchiveMergeLayout = ArchiveMergeLayout,
            ArchiveMergeCollisionPolicy = ArchiveMergeCollisionPolicy,
            ArchiveMergeDuplicatePolicy = ArchiveMergeDuplicatePolicy,
            ArchiveMergeFailurePolicy = ArchiveMergeFailurePolicy,
            ArchiveMergeOutputNamePolicy = ArchiveMergeOutputNamePolicy,
            ArchiveMergeCompressionLevel = ArchiveMergeCompressionLevel,
            ArchiveMergeDeleteOriginals = ArchiveMergeDeleteOriginals,
            ContextMenuArchiveMergeGroupByArchiveName = ContextMenuArchiveMergeGroupByArchiveName,
            ContextMenuArchiveMergePreserveInternalPaths = ContextMenuArchiveMergePreserveInternalPaths,
            FileCompareOptions = FileCompareOptions.Clone()
        };
    }
}

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string SettingsPath => Path.Combine(FileToolsEnvironment.AppDataDir, "settings.json");

    public static FileToolsSettings Load()
    {
        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        AutoRelocationTemplateStore.EnsureTemplatesInitialized();

        if (!File.Exists(SettingsPath))
        {
            var settings = new FileToolsSettings();
            Save(settings);
            return settings;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<FileToolsSettings>(
                File.ReadAllText(SettingsPath),
                JsonOptions) ?? new FileToolsSettings();
            Normalize(settings);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            FileToolsEnvironment.Log("SETTINGS", ex.Message);
            return new FileToolsSettings();
        }
    }

    public static void Save(FileToolsSettings settings)
    {
        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        Normalize(settings);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static void Normalize(FileToolsSettings settings)
    {
        settings.FolderWrapFolderNameTemplate = NormalizeTemplate(
            settings.FolderWrapFolderNameTemplate,
            NameTemplateDefaults.FolderWrapFolderNameTemplate);
        settings.FolderUnwrapMismatchFileNameTemplate = NormalizeTemplate(
            settings.FolderUnwrapMismatchFileNameTemplate,
            NameTemplateDefaults.FolderUnwrapPrefixFolderNameTemplate);
        settings.FolderStructureConflictNameTemplate = NormalizeTemplate(
            settings.FolderStructureConflictNameTemplate,
            NameTemplateDefaults.DefaultConflictNameTemplate);
        if (settings.FolderStructureConflictPolicy is NameCollisionPolicy.MergeIntoExisting or NameCollisionPolicy.Ask)
        {
            settings.FolderStructureConflictPolicy = NameCollisionPolicy.Skip;
        }

        settings.FileKindExtensionRules = AutoRelocationFileTypeClassifier
            .NormalizeExtensionRules(settings.FileKindExtensionRules)
            .Select(static rule => new FileKindExtensionRule
            {
                Kind = rule.Kind,
                Extensions = rule.Extensions.ToList()
            })
            .ToList();
        settings.FileCompareOptions ??= new FileCompareOptions();
        settings.RenameCorrectionPlugins = RenameCorrectionPluginDefaults.Normalize(settings.RenameCorrectionPlugins);
        settings.RenamePatternFeedbackLimit = Math.Max(
            FileNamePatternFeedbackStore.MinimumFeedbackLimit,
            settings.RenamePatternFeedbackLimit);
        settings.FileCompareOptions.CommonNameMinimumCharacters = Math.Max(1, settings.FileCompareOptions.CommonNameMinimumCharacters);
        settings.FileCompareOptions.CommonNameMinimumPercent = Math.Clamp(settings.FileCompareOptions.CommonNameMinimumPercent, 0.01, 1);
        settings.FileCompareOptions.RangeBytes = Math.Max(1, settings.FileCompareOptions.RangeBytes);
        settings.FileCompareOptions.RangeOffsetBytes = Math.Max(0, settings.FileCompareOptions.RangeOffsetBytes);
        settings.FileCompareOptions.PartialMatchThreshold = Math.Clamp(settings.FileCompareOptions.PartialMatchThreshold, 0.10, 1);
        settings.FileCompareOptions.ByteToBytePrefilterRatio = Math.Clamp(settings.FileCompareOptions.ByteToBytePrefilterRatio, 0, 1);
        settings.FileCompareOptions.ArchiveEntryLimitCount = Math.Max(1, settings.FileCompareOptions.ArchiveEntryLimitCount);
    }

    private static string NormalizeTemplate(string? template, string fallback)
    {
        return string.IsNullOrWhiteSpace(template) ? fallback : template.Trim();
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

