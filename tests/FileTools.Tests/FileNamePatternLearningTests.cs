using FileTools;

namespace FileTools.Tests;

public sealed class FileNamePatternLearningTests
{
    private const string ParsePattern = "[{BracketedText}] {Text} {Number:000}";
    private const string BracketPrefixRender = "[{BracketedText}] {Text} {Number:000}{Extension}";
    private const string PlainPrefixRender = "{BracketedText} - {Text} {Number:000}{Extension}";

    [Fact]
    public void Rank_PromotesPreviouslySelectedParseRenderPair()
    {
        var now = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);
        var feedback = new[]
        {
            new FileNamePatternFeedback
            {
                OriginalFileName = "[Author] Series 001.zip",
                SelectedFileName = "Author - Series 001.zip",
                ParsePattern = ParsePattern,
                RenderPattern = PlainPrefixRender,
                ConfirmedAtUtc = now.AddDays(-1)
            }
        };
        var candidates = new[]
        {
            new FileNamePatternRankCandidate
            {
                ParsePattern = ParsePattern,
                RenderPattern = BracketPrefixRender,
                CandidateFileName = "[Author] Series 002.zip",
                BaseScore = 0.70
            },
            new FileNamePatternRankCandidate
            {
                ParsePattern = ParsePattern,
                RenderPattern = PlainPrefixRender,
                CandidateFileName = "Author - Series 002.zip",
                BaseScore = 0.68
            }
        };

        var ranked = FileNamePatternStatisticsRanker.Rank(candidates, feedback, now);

        Assert.Equal("Author - Series 002.zip", ranked[0].Candidate.CandidateFileName);
        Assert.Contains(ranked[0].Reasons, static reason => reason.Contains("parse/render history", StringComparison.Ordinal));
    }

    [Fact]
    public void Rank_UsesRecencyWeightedFeedback()
    {
        var now = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);
        var feedback = new[]
        {
            new FileNamePatternFeedback
            {
                OriginalFileName = "[Author] Series 001.zip",
                SelectedFileName = "[Author] Series 001.zip",
                ParsePattern = ParsePattern,
                RenderPattern = BracketPrefixRender,
                ConfirmedAtUtc = now.AddDays(-100)
            },
            new FileNamePatternFeedback
            {
                OriginalFileName = "[Author] Series 002.zip",
                SelectedFileName = "Author - Series 002.zip",
                ParsePattern = ParsePattern,
                RenderPattern = PlainPrefixRender,
                ConfirmedAtUtc = now.AddDays(-1)
            }
        };
        var candidates = new[]
        {
            new FileNamePatternRankCandidate
            {
                ParsePattern = ParsePattern,
                RenderPattern = BracketPrefixRender,
                CandidateFileName = "[Author] Series 003.zip",
                BaseScore = 0.70
            },
            new FileNamePatternRankCandidate
            {
                ParsePattern = ParsePattern,
                RenderPattern = PlainPrefixRender,
                CandidateFileName = "Author - Series 003.zip",
                BaseScore = 0.68
            }
        };

        var ranked = FileNamePatternStatisticsRanker.Rank(candidates, feedback, now);

        Assert.Equal("Author - Series 003.zip", ranked[0].Candidate.CandidateFileName);
    }

    [Fact]
    public void Normalize_RemovesEmptyPatternsAndDeduplicatesCandidatePatterns()
    {
        var feedback = FileNamePatternFeedbackNormalizer.Normalize(
            [
                new FileNamePatternFeedback
                {
                    OriginalFileName = " original.zip ",
                    SelectedFileName = " selected.zip ",
                    ParsePattern = " ",
                    RenderPattern = PlainPrefixRender
                },
                new FileNamePatternFeedback
                {
                    OriginalFileName = " original.zip ",
                    SelectedFileName = " selected.zip ",
                    ParsePattern = " " + ParsePattern + " ",
                    RenderPattern = " " + PlainPrefixRender + " ",
                    CandidatePatterns = [ParsePattern, " ", ParsePattern]
                }
            ]);

        Assert.Single(feedback);
        Assert.Equal("original.zip", feedback[0].OriginalFileName);
        Assert.Equal(ParsePattern, feedback[0].ParsePattern);
        Assert.Equal(PlainPrefixRender, feedback[0].RenderPattern);
        Assert.Equal([ParsePattern], feedback[0].CandidatePatterns);
    }
}
