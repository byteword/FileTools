using FileTools;

namespace FileTools.Tests;

public sealed class FileNameRenderPatternTests
{
    [Fact]
    public void Generate_CreatesPlainLeadingBracketedTextCandidate()
    {
        var candidates = FileNameRenderPatternGenerator.Generate("[Author] Series 001.zip");

        Assert.Contains(candidates, static candidate => candidate.FileName == "Author - Series 001.zip");
    }

    [Fact]
    public void Generate_ReconstructsMixedInputIntoSharedCandidate()
    {
        var first = FileNameRenderPatternGenerator.Generate("[Author] Series 001.zip");
        var second = FileNameRenderPatternGenerator.Generate("Series - 001 (Author).zip");

        Assert.Contains(first, static candidate => candidate.FileName == "Author - Series 001.zip");
        Assert.Contains(second, static candidate => candidate.FileName == "Author - Series 001.zip");
    }

    [Fact]
    public void Generate_PreservesDetectedNumberWidth()
    {
        var candidates = FileNameRenderPatternGenerator.Generate("Series - 7 (Author).zip");

        Assert.Contains(candidates, static candidate => candidate.FileName == "Author - Series 7.zip");
        Assert.DoesNotContain(candidates, static candidate => candidate.FileName == "Author - Series 007.zip");
    }

    [Fact]
    public void Generate_AppliesCustomNumberFormat()
    {
        var pattern = new FileNameRenderPattern
        {
            DisplayName = "Padded custom",
            Template = "{BracketedText} - {Text} {Number:000}{Extension}",
            BaseScore = 1.0
        };

        var candidates = FileNameRenderPatternGenerator.Generate(
            "Series - 7 (Author).zip",
            [pattern]);

        Assert.Single(candidates);
        Assert.Equal("Author - Series 007.zip", candidates[0].FileName);
    }
}
