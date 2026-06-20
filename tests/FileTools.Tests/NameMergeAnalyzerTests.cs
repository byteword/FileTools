using FileTools;

namespace FileTools.Tests;

public sealed class NameMergeAnalyzerTests
{
    [Fact]
    public void ProposalBuilder_PreservesRangeAnalysisWithDefaultSettings()
    {
        var proposal = MergeNameProposalBuilder.CreateForPaths(
            [@"C:\Temp\Series__01.txt", @"C:\Temp\Series__02.txt"],
            new FileToolsSettings());

        Assert.Equal("Series 01~02", proposal.Stem);
        Assert.Equal(NameMergeAnalysisKind.NumericRange, proposal.Analysis.Kind);
    }

    [Fact]
    public void Analyze_MergesContiguousNumericRanges()
    {
        var stem = NameMergeAnalyzer.CreateCommonStem(["A 01~03", "A 04~06"]);

        Assert.Equal("A 01~06", stem);
    }

    [Fact]
    public void Analyze_KeepsDisjointNumericRanges()
    {
        var stem = NameMergeAnalyzer.CreateCommonStem(["A 01~03", "A 05~08"]);

        Assert.Equal("A 01~03, 05~08", stem);
    }

    [Fact]
    public void Analyze_UsesMiddleCommonTokenWhenPrefixDoesNotMatch()
    {
        var stem = NameMergeAnalyzer.CreateCommonStem(["test이름 tt", "이름abc"]);

        Assert.Equal("이름", stem);
    }

    [Fact]
    public void Analyze_MergesSingleLetterVariableBetweenCommonText()
    {
        var stem = NameMergeAnalyzer.CreateCommonStem(["이름 a태그", "이름 b태그"]);

        Assert.Equal("이름 a~b 태그", stem);
    }

    [Fact]
    public void Analyze_MergesSingleLetterVariableAfterCommonPrefix()
    {
        var stem = NameMergeAnalyzer.CreateCommonStem(["Folder A", "Folder B"]);

        Assert.Equal("Folder A~B", stem);
    }

    [Fact]
    public void Analyze_PreservesNumericPadding()
    {
        var stem = NameMergeAnalyzer.CreateCommonStem(["A_001", "A_002"]);

        Assert.Equal("A 001~002", stem);
    }

    [Fact]
    public void Analyze_ReturnsEmptyWhenNoCommonStemAndNoFallback()
    {
        var stem = NameMergeAnalyzer.CreateCommonStem(["cat", "dog"]);

        Assert.Equal("", stem);
    }

    [Fact]
    public void Analyze_UsesFallbackWhenNoCommonStem()
    {
        var stem = NameMergeAnalyzer.CreateCommonStem(["cat", "dog"], "Merged");

        Assert.Equal("Merged", stem);
    }
}
