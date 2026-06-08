using FileTools;

namespace FileTools.Tests;

public sealed class FileNamePatternDiscoveryTests
{
    [Fact]
    public void Tokenize_SeparatesBracketTextTextAndNumber()
    {
        var tokens = FileNamePatternDiscovery.Tokenize("[Author] Series 001.zip");

        Assert.Equal(
            [
                FileNamePatternTokenKind.BracketedText,
                FileNamePatternTokenKind.Separator,
                FileNamePatternTokenKind.Text,
                FileNamePatternTokenKind.Separator,
                FileNamePatternTokenKind.Number
            ],
            tokens.Select(static token => token.Kind).ToArray());
        Assert.Equal("Author", tokens[0].Text);
        Assert.Equal("Series", tokens[2].Text);
        Assert.Equal("001", tokens[4].Text);
    }

    [Fact]
    public void Discover_GroupsFileNamesByStructuralPattern()
    {
        var patterns = FileNamePatternDiscovery.Discover(
            [
                "[Author] Series 001.zip",
                "[Author] Series 002.zip",
                "Series - 003 (Author).zip"
            ]);

        var top = patterns[0];

        Assert.Equal("[{BracketedText}] {Text} {Number:000}", top.Signature);
        Assert.Equal(2, top.MatchCount);
        Assert.True(top.HasSequentialNumberSlot);
        Assert.Equal(2, top.StableValueSlotCount);
    }

    [Fact]
    public void Discover_KeepsMixedPatternsAsSeparateCandidates()
    {
        var patterns = FileNamePatternDiscovery.Discover(
            [
                "[Author] Series 001.zip",
                "[Author] Series 002.zip",
                "Series - 003 (Author).zip",
                "Series - 004 (Author).zip"
            ]);

        Assert.Contains(patterns, static pattern => pattern.Signature == "[{BracketedText}] {Text} {Number:000}");
        Assert.Contains(patterns, static pattern => pattern.Signature == "{Text} - {Number:000} ({BracketedText})");
    }
}
