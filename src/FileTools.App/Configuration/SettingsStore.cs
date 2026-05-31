using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

internal sealed class FileToolsSettings
{
    public FolderStructureOperation FolderStructureOperation { get; set; } = FolderStructureOperation.Auto;

    public FolderUnwrapNameMismatchMode FolderUnwrapNameMismatchMode { get; set; } = FolderUnwrapNameMismatchMode.KeepFileName;

    public string AutoRelocationTemplateId { get; set; } = AutoRelocationTemplateDefaults.DefaultTemplateId;

    public string? AutoRelocationTargetRootPath { get; set; }

    public bool RegisterContextMenu { get; set; } = true;

    public ContextMenuLayout ContextMenuLayout { get; set; } = ContextMenuLayout.Grouped;

    public bool ContextMenuOpenApp { get; set; } = true;

    public bool ContextMenuFileNameCorrection { get; set; } = true;

    public bool ContextMenuFolderStructure { get; set; } = true;

    public bool ContextMenuFolderWrapFiles { get; set; } = true;

    public bool ContextMenuFolderUnwrapSameNameSingleFile { get; set; } = true;

    public bool ContextMenuFolderUnwrapSingleFile { get; set; } = true;

    public bool ContextMenuFolderMoveInnerFilesUp { get; set; } = true;

    public bool ContextMenuAutoRelocation { get; set; } = true;

    public bool ContextMenuAutoRelocationCurrentFolder { get; set; } = true;

    public bool ContextMenuAutoRelocationChooseTarget { get; set; } = true;

    public bool RenameReviewBeforeApply { get; set; } = true;

    public bool RenameUseDictionary { get; set; } = true;

    public bool IsContextMenuToolEnabled(ToolMode mode)
    {
        return mode switch
        {
            ToolMode.FileNameCorrection => ContextMenuFileNameCorrection,
            ToolMode.FolderStructure => ContextMenuFolderStructure && IsAnyContextMenuFolderOperationEnabled,
            ToolMode.AutoRelocation => ContextMenuAutoRelocation && IsAnyContextMenuAutoRelocationOperationEnabled,
            _ => false
        };
    }

    public bool IsAnyContextMenuFolderOperationEnabled =>
        ContextMenuFolderWrapFiles ||
        ContextMenuFolderUnwrapSameNameSingleFile ||
        ContextMenuFolderUnwrapSingleFile ||
        ContextMenuFolderMoveInnerFilesUp;

    public bool IsAnyContextMenuAutoRelocationOperationEnabled =>
        ContextMenuAutoRelocationCurrentFolder ||
        ContextMenuAutoRelocationChooseTarget;

    public FileToolsSettings Clone()
    {
        return new FileToolsSettings
        {
            FolderStructureOperation = FolderStructureOperation,
            FolderUnwrapNameMismatchMode = FolderUnwrapNameMismatchMode,
            AutoRelocationTemplateId = AutoRelocationTemplateId,
            AutoRelocationTargetRootPath = AutoRelocationTargetRootPath,
            RegisterContextMenu = RegisterContextMenu,
            ContextMenuLayout = ContextMenuLayout,
            ContextMenuOpenApp = ContextMenuOpenApp,
            ContextMenuFileNameCorrection = ContextMenuFileNameCorrection,
            ContextMenuFolderStructure = ContextMenuFolderStructure,
            ContextMenuFolderWrapFiles = ContextMenuFolderWrapFiles,
            ContextMenuFolderUnwrapSameNameSingleFile = ContextMenuFolderUnwrapSameNameSingleFile,
            ContextMenuFolderUnwrapSingleFile = ContextMenuFolderUnwrapSingleFile,
            ContextMenuFolderMoveInnerFilesUp = ContextMenuFolderMoveInnerFilesUp,
            ContextMenuAutoRelocation = ContextMenuAutoRelocation,
            ContextMenuAutoRelocationCurrentFolder = ContextMenuAutoRelocationCurrentFolder,
            ContextMenuAutoRelocationChooseTarget = ContextMenuAutoRelocationChooseTarget,
            RenameReviewBeforeApply = RenameReviewBeforeApply,
            RenameUseDictionary = RenameUseDictionary
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
            return JsonSerializer.Deserialize<FileToolsSettings>(
                File.ReadAllText(SettingsPath),
                JsonOptions) ?? new FileToolsSettings();
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
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
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
