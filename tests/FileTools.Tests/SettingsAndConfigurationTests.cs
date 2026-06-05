using FileTools;

namespace FileTools.Tests;

public sealed class SettingsAndConfigurationTests
{
    [Fact]
    public void FileToolsSettings_CloneDeepCopiesExtensionRules()
    {
        var settings = new FileToolsSettings
        {
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

        Assert.Equal([".txt"], settings.FileKindExtensionRules[0].Extensions);
        Assert.Equal([".txt", ".md"], clone.FileKindExtensionRules[0].Extensions);
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
}
