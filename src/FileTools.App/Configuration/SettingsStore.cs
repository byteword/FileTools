using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

internal sealed class FileToolsSettings
{
    public FolderStructureOperation FolderStructureOperation { get; set; } = FolderStructureOperation.Auto;

    public string AutoRelocationTemplateId { get; set; } = AutoRelocationTemplateDefaults.DefaultTemplateId;
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
