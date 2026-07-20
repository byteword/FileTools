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
    [InlineData("ㅇr", "아")]
    [InlineData("ㅎH", "해")]
    [InlineData("ㅇr이돌", "아이돌")]
    [InlineData("혀ㄴ주ㅇ", "현중")]
    [InlineData("혀ㄴ주ㅇ구l호rㄴ로ㄱ", "현중귀환록")]
    [InlineData("[소설 - 텍] 혀ㄴ주ㅇ구l호rㄴ로ㄱ - 완", "[소설 - 텍] 현중귀환록 - 완")]
    public void ObfuscatedHangulCandidateGenerator_RestoresYaminJungeumTokens(string input, string expected)
    {
        var generator = new ObfuscatedHangulCandidateGenerator();

        var candidate = Assert.Single(generator.Generate(input));

        Assert.Equal(expected, candidate.Value);
        Assert.True(candidate.RequiresReview);
    }

    [Fact]
    public void KoreanFileNameCorrector_OffersYaminJungeumAsReviewCandidate()
    {
        var corrector = new KoreanFileNameCorrector();

        var preview = corrector.CreatePreview(@"C:\Temp\ㅎH 1화.zip");

        Assert.Contains(preview.Candidates, candidate =>
            candidate.Value == "해 1화.zip" &&
            candidate.Reason == "왜곡 한글 복원 후보" &&
            candidate.RequiresReview);
        Assert.Equal(RenamePreviewStatus.NeedsReview, preview.Status);
    }

    [Fact]
    public void KoreanFileNameCorrector_OffersMixedYaminJungeumAsReviewCandidate()
    {
        var corrector = new KoreanFileNameCorrector();

        var preview = corrector.CreatePreview(@"C:\Temp\[소설 - 텍] 혀ㄴ주ㅇ구l호rㄴ로ㄱ - 완.txt");

        Assert.Contains(preview.Candidates, candidate =>
            candidate.Value == "[소설 - 텍] 현중귀환록 - 완.txt" &&
            candidate.Reason == "왜곡 한글 복원 후보" &&
            candidate.RequiresReview);
        Assert.Equal(RenamePreviewStatus.NeedsReview, preview.Status);
    }

    [Fact]
    public void AdvancedNameEditDialog_BuildsRecommendationsFromOriginalNameTokensOnly()
    {
        var recommendations = AdvancedNameEditDialog.BuildRecommendationsForName(
            "[S로맨스] 임시 결혼 시작했습니다ㄴr 1권 - 3권 06.23");

        Assert.Contains("[S로맨스]", recommendations);
        Assert.Contains("S로맨스", recommendations);
        Assert.Contains("임시", recommendations);
        Assert.Contains("결혼", recommendations);
        Assert.Contains("시작했습니다ㄴr", recommendations);
        Assert.Contains("1권", recommendations);
        Assert.Contains("3권", recommendations);
        Assert.Contains("06.23", recommendations);
        Assert.DoesNotContain("[S로맨스] 임시 결혼 시작했습니다ㄴr 1권", recommendations);
    }

    [Fact]
    public void AdvancedNameEditDialog_AutomaticCorrectionRestoresYaminJungeum()
    {
        var corrected = AdvancedNameEditDialog.CreateAutomaticCorrectionForName(
            "[S로맨스] 임시 결혼 시작했습니다ㄴr 1권 - 3권 06.23");

        Assert.Equal("[S로맨스] 임시 결혼 시작했습니다나 1권 - 3권 06.23", corrected);
    }

    [Fact]
    public void AdvancedNameEditDialog_AutomaticCorrectionUsesTheSharedFallbackOrder()
    {
        var corrected = AdvancedNameEditDialog.GetAutomaticCorrection(
            new NameEditRequest(
                OriginalName: "시작했습니다ㄴr.txt",
                SuggestedName: "suggested-name.txt",
                AutomaticName: "automatic-name.txt"),
            "suggested-name.txt");

        Assert.Equal("시작했습니다나.txt", corrected);
    }

    [Fact]
    public void SimpleRenameReviewDialog_ConstructsWithSinglePreview()
    {
        RunInStaThread(() =>
        {
            using var dialog = new SimpleRenameReviewDialog(
                [
                    CreatePreview(
                        Path.Combine(Path.GetTempPath(), "source-" + Guid.NewGuid().ToString("N") + ".txt"),
                        Path.Combine(Path.GetTempPath(), "target-" + Guid.NewGuid().ToString("N") + ".txt"))
                ],
                applyOnOk: false);
        });
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

    private static void RunInStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
