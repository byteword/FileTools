using FileTools;

namespace FileTools.Tests;

public sealed class NamingRegressionTests
{
    [Fact]
    public void KoreanJamoNormalizer_ComposesCompatibilityJamoIntoSyllables()
    {
        var normalized = KoreanJamoNormalizer.Normalize("ㄱㅏㄴㅏ");

        Assert.Equal("가나", normalized);
    }

    [Theory]
    [InlineData("CON.txt", "CON_.txt")]
    [InlineData("a<b>|c?.txt", "a b c.txt")]
    [InlineData("   .txt", "untitled.txt")]
    public void WindowsFileNameSafety_RewritesInvalidOrReservedFileNames(string input, string expected)
    {
        var safe = WindowsFileNameSafety.MakeSafeFileName(input);

        Assert.Equal(expected, safe);
    }

    [Fact]
    public void KoreanFileNameCorrector_ExtractsTagsAuthorTitleAndEpisode()
    {
        var corrector = new KoreanFileNameCorrector();

        var parts = corrector.ParseParts("[완결] [작가A] 멋진 작품 10화", ".txt");

        Assert.Equal("멋진 작품", parts.Title);
        Assert.Equal("10화", parts.EpisodeRange);
        Assert.Contains("완결", parts.Tags);
        Assert.Equal("작가A", parts.Author);
        Assert.Equal("멋진 작품 10화 [완결][작가A].txt", parts.Compose());
    }

    [Fact]
    public void RenamePlanner_MarksConflictingSuggestions()
    {
        var planner = new RenamePlanner();
        var first = CreatePreview(@"C:\Temp\first.txt", @"C:\Temp\target.txt");
        var second = CreatePreview(@"C:\Temp\second.txt", @"C:\Temp\target.txt");

        var plan = planner.ResolveConflicts([first, second]);

        Assert.Contains(plan, static preview => preview.Status == RenamePreviewStatus.Conflict);
    }

    private static RenamePreview CreatePreview(string originalPath, string suggestedPath)
    {
        return new RenamePreview
        {
            OriginalPath = originalPath,
            OriginalFileName = Path.GetFileName(originalPath),
            Parts = new FileNameParts
            {
                Title = Path.GetFileNameWithoutExtension(suggestedPath),
                Extension = Path.GetExtension(suggestedPath)
            },
            SuggestedFileName = Path.GetFileName(suggestedPath),
            SuggestedPath = suggestedPath,
            Status = RenamePreviewStatus.Ready
        };
    }
}
