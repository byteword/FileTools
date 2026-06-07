using FileTools.Correction;
using FileTools.Correction.SymSpellPlugin;

namespace FileTools.Tests;

public sealed class RenameCorrectionPluginTests
{
    [Fact]
    public void NameCorrectionPluginCatalog_DiscoversPluginFromPluginsFolder()
    {
        EnsureSymSpellPluginInTestPluginFolder();
        NameCorrectionPluginCatalog.ResetForTests();

        var plugins = NameCorrectionPluginCatalog.Discover();

        Assert.Contains(plugins, plugin => plugin.Descriptor.Id == "filetools.symspell");
    }

    [Fact]
    public void RenameOperations_AddsPluginCandidatesAsReviewOnly()
    {
        EnsureSymSpellPluginInTestPluginFolder();
        NameCorrectionPluginCatalog.ResetForTests();

        using var temp = TempDirectory.Create();
        var dictionaryPath = temp.GetPath("frequency.txt");
        var sourcePath = temp.GetPath("helo 01.txt");
        File.WriteAllLines(dictionaryPath, ["hello 100"]);
        File.WriteAllText(sourcePath, "");

        var settings = new FileToolsSettings
        {
            RenameUseDictionary = false,
            RenameCorrectionPlugins = new RenameCorrectionPluginOptions
            {
                Enabled = true,
                Language = "en-US",
                Plugins =
                [
                    new RenameCorrectionPluginConfiguration
                    {
                        PluginId = "filetools.symspell",
                        Enabled = true,
                        Settings = new Dictionary<string, string>
                        {
                            ["dictionaryPath"] = dictionaryPath,
                            ["sourceMode"] = "frequency",
                            ["maxEditDistance"] = "1",
                            ["minimumScore"] = "0.1"
                        }
                    }
                ]
            }
        };

        var preview = Assert.Single(RenameOperations.CreatePlan([sourcePath], settings));

        Assert.Equal(RenamePreviewStatus.NeedsReview, preview.Status);
        Assert.Contains(preview.Candidates, candidate =>
            candidate.Value == "hello 01.txt" &&
            candidate.RequiresReview &&
            candidate.Reason.Contains("SymSpell", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SymSpellPlugin_GeneratesReviewCandidateFromUserDictionary()
    {
        using var temp = TempDirectory.Create();
        var dictionaryPath = temp.GetPath("frequency.txt");
        File.WriteAllLines(dictionaryPath, ["hello 100", "world 80"]);

        var plugin = new SymSpellNameCorrectionPlugin();
        var candidates = plugin.GenerateCandidates(
            new NameCorrectionRequest
            {
                OriginalPath = temp.GetPath("helo 01.txt"),
                OriginalFileName = "helo 01.txt",
                OriginalStem = "helo 01",
                SuggestedFileName = "helo 01.txt",
                SuggestedStem = "helo 01",
                Extension = ".txt",
                Title = "helo",
                EpisodeRange = "01",
                Language = "en-US"
            },
            new Dictionary<string, string>
            {
                ["dictionaryPath"] = dictionaryPath,
                ["sourceMode"] = "frequency",
                ["maxEditDistance"] = "1",
                ["minimumScore"] = "0.1"
            },
            CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal("hello 01", candidate.Value);
        Assert.True(candidate.RequiresReview);
        Assert.Contains("corrected", candidate.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenameCorrectionPluginDefaults_NormalizesDuplicatePluginSettings()
    {
        var options = RenameCorrectionPluginDefaults.Normalize(new RenameCorrectionPluginOptions
        {
            Enabled = true,
            Language = "",
            Plugins =
            [
                new RenameCorrectionPluginConfiguration
                {
                    PluginId = " filetools.symspell ",
                    Enabled = true
                },
                new RenameCorrectionPluginConfiguration
                {
                    PluginId = "FILETOOLS.SYMSPELL",
                    Enabled = false
                }
            ]
        });

        Assert.Equal("ko-KR", options.Language);
        var plugin = Assert.Single(options.Plugins);
        Assert.Equal("filetools.symspell", plugin.PluginId);
        Assert.True(plugin.Enabled);
    }

    private static void EnsureSymSpellPluginInTestPluginFolder()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var pluginDirectory = Path.Combine(
            baseDirectory,
            "Plugins",
            "FileTools.Correction.SymSpellPlugin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pluginDirectory);
        File.Copy(
            Path.Combine(baseDirectory, "FileTools.Correction.SymSpellPlugin.dll"),
            Path.Combine(pluginDirectory, "FileTools.Correction.SymSpellPlugin.dll"),
            overwrite: true);
    }
}
