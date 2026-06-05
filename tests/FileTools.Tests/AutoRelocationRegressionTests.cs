using FileTools;

namespace FileTools.Tests;

public sealed class AutoRelocationRegressionTests
{
    [Fact]
    public void FileTypeClassifier_UsesCustomNormalizedExtensionRules()
    {
        var settings = new FileToolsSettings
        {
            FileKindExtensionRules =
            [
                new FileKindExtensionRule
                {
                    Kind = "Raw Data",
                    Extensions = ["csv", "*.tsv", ".CSV"]
                }
            ]
        };

        var kind = AutoRelocationFileTypeClassifier.GetKnownFileKind("report.CSV", settings);
        var extensions = AutoRelocationFileTypeClassifier.NormalizeExtensions(["csv", "*.tsv", ".CSV"]);

        Assert.Equal("Raw Data", kind);
        Assert.Equal([".csv", ".tsv"], extensions);
    }

    [Fact]
    public void AutoRelocationPlanBuilder_BuildsNestedFoldersFromInitialAndEpisodeRange()
    {
        using var temp = TempDirectory.Create();
        var source = temp.GetPath("series.txt");
        var template = new AutoRelocationTemplateDocument
        {
            Id = "Test",
            DisplayName = "Test",
            PathRules =
            [
                new AutoRelocationPathRule
                {
                    Source = AutoRelocationValueSource.Title,
                    Transform = AutoRelocationValueTransform.InitialBucket,
                    Language = AutoRelocationLanguageProfile.KoreanEnglish,
                    Format = "[{value}]",
                    FallbackFolderName = "[0A]"
                },
                new AutoRelocationPathRule
                {
                    Source = AutoRelocationValueSource.EpisodeRange,
                    Transform = AutoRelocationValueTransform.NumberFloor,
                    Format = "{value}",
                    Options = new AutoRelocationTransformOptions
                    {
                        NumberStep = 10,
                        NumberUnit = "화",
                        NumberLabelFormat = "{value}대"
                    }
                }
            ]
        };
        var context = new AutoRelocationItemContext(
            source,
            new Dictionary<string, string?>
            {
                ["title"] = "가나다",
                ["episodeRange"] = "12화"
            });

        var result = new AutoRelocationPlanBuilder().Build(temp.Root, template, [context]);

        var item = Assert.Single(result.Items);
        Assert.False(item.RequiresReview);
        Assert.True(item.CreateTargetFolder);
        Assert.Equal(Path.Combine(temp.Root, "[ㄱ]", "10화대", "series.txt"), item.TargetPath);
    }

    [Fact]
    public void AutoRelocationPlanBuilder_AppliesPrefilterActionsBeforePathRules()
    {
        using var temp = TempDirectory.Create();
        var excluded = temp.GetPath("skip.txt");
        var routed = temp.GetPath("route.txt");
        var template = new AutoRelocationTemplateDocument
        {
            Id = "Prefilter",
            DisplayName = "Prefilter",
            Prefilters =
            [
                new AutoRelocationPrefilterRule
                {
                    Source = AutoRelocationValueSource.Tags,
                    Operator = AutoRelocationFilterOperator.Contains,
                    Value = "skip",
                    Action = AutoRelocationPrefilterAction.Exclude
                },
                new AutoRelocationPrefilterRule
                {
                    Source = AutoRelocationValueSource.Tags,
                    Operator = AutoRelocationFilterOperator.Contains,
                    Value = "route",
                    Action = AutoRelocationPrefilterAction.RouteToFolder,
                    TargetFolderName = "Routed"
                }
            ],
            PathRules =
            [
                new AutoRelocationPathRule
                {
                    Source = AutoRelocationValueSource.Title,
                    Transform = AutoRelocationValueTransform.Full
                }
            ]
        };

        var result = new AutoRelocationPlanBuilder().Build(
            temp.Root,
            template,
            [
                CreateContext(excluded, "skip"),
                CreateContext(routed, "route")
            ]);

        Assert.Equal(1, result.ExcludedCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(Path.Combine(temp.Root, "Routed", "route.txt"), item.TargetPath);
    }

    private static AutoRelocationItemContext CreateContext(string sourcePath, string tags)
    {
        return new AutoRelocationItemContext(
            sourcePath,
            new Dictionary<string, string?>
            {
                ["tags"] = tags,
                ["title"] = Path.GetFileNameWithoutExtension(sourcePath)
            });
    }
}
