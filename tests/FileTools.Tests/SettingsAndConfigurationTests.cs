using FileTools;

namespace FileTools.Tests;

public sealed class SettingsAndConfigurationTests
{
    [Fact]
    public void FileToolsSettings_CloneDeepCopiesExtensionRules()
    {
        var settings = new FileToolsSettings
        {
            FileCompareOptions = new FileCompareOptions
            {
                RangeBytes = 4096
            },
            FileKindExtensionRules =
            [
                new FileKindExtensionRule
                {
                    Kind = "Text",
                    Extensions = [".txt"]
                }
            ]
        };

        var clone = settings.Clone();
        clone.FileKindExtensionRules[0].Extensions.Add(".md");
        clone.FileCompareOptions.RangeBytes = 8192;

        Assert.Equal([".txt"], settings.FileKindExtensionRules[0].Extensions);
        Assert.Equal([".txt", ".md"], clone.FileKindExtensionRules[0].Extensions);
        Assert.Equal(4096, settings.FileCompareOptions.RangeBytes);
        Assert.Equal(8192, clone.FileCompareOptions.RangeBytes);
    }

    [Fact]
    public void RenameRuleStore_NormalizeRulesKeepsRequiredRuleEnabledAndAddsUserRule()
    {
        var rules = RenameRuleStore.NormalizeRules(
            [
                new RenameCorrectionRule
                {
                    Id = RenameRuleIds.WindowsSafeFileName,
                    Enabled = false,
                    Mode = RenameCorrectionRuleMode.Review
                },
                new RenameCorrectionRule
                {
                    Id = "user.trim-prefix",
                    DisplayName = " Trim prefix ",
                    Kind = RenameCorrectionRuleKind.PrefixTrim,
                    Stage = RenameCorrectionRuleStage.UserRewrite,
                    Source = "raw-",
                    Replacement = "",
                    Enabled = true
                }
            ]);

        var required = rules.Single(rule => rule.Id == RenameRuleIds.WindowsSafeFileName);
        var userRule = rules.Single(rule => rule.Id == "user.trim-prefix");
        Assert.True(required.Enabled);
        Assert.Equal(RenameCorrectionRuleMode.Automatic, required.Mode);
        Assert.Equal("Trim prefix", userRule.DisplayName);
        Assert.Equal("raw-", userRule.Source);
    }

    [Fact]
    public void ContextMenuCommandLine_TryParseCommandAcceptsFileCompare()
    {
        var parsed = ContextMenuCommandLine.TryParseCommand("FileCompare", out var command);

        Assert.True(parsed);
        Assert.Equal(ContextMenuCommand.FileCompare, command);
    }

    [Fact]
    public void ContextMenuCommandLine_CreateRegistryCommandBuildsFileCompareContextLaunch()
    {
        var commandLine = ContextMenuCommandLine.CreateRegistryCommand(
            @"C:\Tools\FileTools.exe",
            ContextMenuCommand.FileCompare);

        Assert.Equal(@"""C:\Tools\FileTools.exe"" /context FileCompare ""%1""", commandLine);
    }
}
