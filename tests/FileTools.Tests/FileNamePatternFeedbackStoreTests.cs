using FileTools;

namespace FileTools.Tests;

public sealed class FileNamePatternFeedbackStoreTests
{
    private const string ParsePattern = "[{BracketedText}] {Text} {Number:000}";
    private const string RenderPattern = "{BracketedText} - {Text} {Number:000}{Extension}";

    [Fact]
    public void SaveAndLoad_RoundTripsNormalizedFeedback()
    {
        using var temp = TempDirectory.Create();
        var path = temp.GetPath("rename-pattern-feedback.jsonl");
        var confirmedAt = new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.FromHours(9));

        FileNamePatternFeedbackStore.Save(
            [
                new FileNamePatternFeedback
                {
                    OriginalFileName = " [Author] Series 001.zip ",
                    SelectedFileName = " Author - Series 001.zip ",
                    ParsePattern = " " + ParsePattern + " ",
                    RenderPattern = " " + RenderPattern + " ",
                    CandidatePatterns = [ParsePattern, " ", ParsePattern],
                    ConfirmedAtUtc = confirmedAt
                }
            ],
            path);

        var loaded = FileNamePatternFeedbackStore.Load(path);

        Assert.Single(loaded);
        Assert.Equal("[Author] Series 001.zip", loaded[0].OriginalFileName);
        Assert.Equal("Author - Series 001.zip", loaded[0].SelectedFileName);
        Assert.Equal(ParsePattern, loaded[0].ParsePattern);
        Assert.Equal(RenderPattern, loaded[0].RenderPattern);
        Assert.Equal([ParsePattern], loaded[0].CandidatePatterns);
        Assert.Equal(TimeSpan.Zero, loaded[0].ConfirmedAtUtc.Offset);
    }

    [Fact]
    public void Append_IgnoresInvalidFeedbackRows()
    {
        using var temp = TempDirectory.Create();
        var path = temp.GetPath("rename-pattern-feedback.jsonl");

        FileNamePatternFeedbackStore.Append(CreateFeedback("001"), path);
        FileNamePatternFeedbackStore.Append(
            CreateFeedback("002") with
            {
                ParsePattern = " "
            },
            path);

        var loaded = FileNamePatternFeedbackStore.Load(path);

        Assert.Single(loaded);
        Assert.Equal("[Author] Series 001.zip", loaded[0].OriginalFileName);
    }

    [Fact]
    public void Load_SkipsMalformedJsonLines()
    {
        using var temp = TempDirectory.Create();
        var path = temp.GetPath("rename-pattern-feedback.jsonl");
        File.WriteAllText(path, "{bad json" + Environment.NewLine);
        FileNamePatternFeedbackStore.Append(CreateFeedback("001"), path);

        var loaded = FileNamePatternFeedbackStore.Load(path);

        Assert.Single(loaded);
        Assert.Equal("Author - Series 001.zip", loaded[0].SelectedFileName);
    }

    [Fact]
    public void Save_TrimsToLatestFeedbackLimitWithMinimumFloor()
    {
        using var temp = TempDirectory.Create();
        var path = temp.GetPath("rename-pattern-feedback.jsonl");
        var feedback = Enumerable
            .Range(1, FileNamePatternFeedbackStore.MinimumFeedbackLimit + 5)
            .Select(index => CreateFeedback(index.ToString("000")) with
            {
                ConfirmedAtUtc = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero).AddMinutes(index)
            })
            .ToArray();

        FileNamePatternFeedbackStore.Save(
            feedback,
            path,
            new FileNamePatternFeedbackStoreOptions
            {
                FeedbackLimit = 5
            });

        var loaded = FileNamePatternFeedbackStore.Load(path);

        Assert.Equal(FileNamePatternFeedbackStore.MinimumFeedbackLimit, loaded.Count);
        Assert.Equal("[Author] Series 006.zip", loaded[0].OriginalFileName);
        Assert.Equal("[Author] Series 105.zip", loaded[^1].OriginalFileName);
    }

    [Fact]
    public void Append_DoesNothingWhenDisabled()
    {
        using var temp = TempDirectory.Create();
        var path = temp.GetPath("rename-pattern-feedback.jsonl");

        FileNamePatternFeedbackStore.Append(
            CreateFeedback("001"),
            path,
            new FileNamePatternFeedbackStoreOptions
            {
                Enabled = false
            });

        Assert.False(File.Exists(path));
        Assert.Empty(FileNamePatternFeedbackStore.Load(
            path,
            new FileNamePatternFeedbackStoreOptions
            {
                Enabled = false
            }));
    }

    [Fact]
    public void Append_TrimsExistingRowsWhenLimitIsReached()
    {
        using var temp = TempDirectory.Create();
        var path = temp.GetPath("rename-pattern-feedback.jsonl");
        var options = new FileNamePatternFeedbackStoreOptions
        {
            FeedbackLimit = FileNamePatternFeedbackStore.MinimumFeedbackLimit
        };

        for (var index = 1; index <= FileNamePatternFeedbackStore.MinimumFeedbackLimit + 1; index++)
        {
            FileNamePatternFeedbackStore.Append(
                CreateFeedback(index.ToString("000")) with
                {
                    ConfirmedAtUtc = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero).AddMinutes(index)
                },
                path,
                options);
        }

        var loaded = FileNamePatternFeedbackStore.Load(path, options);

        Assert.Equal(FileNamePatternFeedbackStore.MinimumFeedbackLimit, loaded.Count);
        Assert.Equal("[Author] Series 002.zip", loaded[0].OriginalFileName);
        Assert.Equal("[Author] Series 101.zip", loaded[^1].OriginalFileName);
    }

    private static FileNamePatternFeedback CreateFeedback(string number)
    {
        return new FileNamePatternFeedback
        {
            OriginalFileName = $"[Author] Series {number}.zip",
            SelectedFileName = $"Author - Series {number}.zip",
            ParsePattern = ParsePattern,
            RenderPattern = RenderPattern,
            ConfirmedAtUtc = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero)
        };
    }
}
