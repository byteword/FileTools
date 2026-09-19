namespace FileTools.Tests;

public sealed class FileCollectionTests
{
    [Fact]
    public void Matcher_CombinesCategoriesWithAndAndValuesWithOr()
    {
        var query = new FileCollectionQuery { Kinds = ["Image", "Document"], Extensions = [".jpg", "PNG"], Name = "여행",
            MinimumSize = 10, MaximumSize = 100 };
        var matches = query.CreateMatcher(TimeZoneInfo.Utc);
        var row = FileCatalogTests.Entry("여행01.JPG") with { Extension = ".jpg", Kind = "image", Size = 10 };
        Assert.True(matches(row));
        Assert.True(matches(row with { Size = 100 }));
        Assert.False(matches(row with { Size = 101 }));
        Assert.False(matches(row with { Kind = "Other" }));
        Assert.False(matches(row with { Extension = ".pdf" }));
        Assert.False(matches(row with { Name = "다른사진.jpg" }));
    }

    [Theory]
    [InlineData(0, "book", "MyBook.txt", true)]
    [InlineData(1, "book.txt", "Book.txt", true)]
    [InlineData(1, "book", "Book.txt", false)]
    [InlineData(2, "book", "Book.txt", true)]
    [InlineData(3, ".txt", "Book.txt", true)]
    [InlineData(4, "여행??.*", "여행01.JPG", true)]
    [InlineData(4, "여행?.*", "여행01.JPG", false)]
    [InlineData(4, "[a]*.txt", "[a]one.txt", true)]
    [InlineData(4, "[a]*.txt", "aone.txt", false)]
    public void Matcher_NameModesUseWholeFilenameAndLiteralWildcardCharacters(int mode, string pattern, string name, bool expected)
    {
        var query = new FileCollectionQuery { NameMode = (FileCollectionNameMode)mode, Name = pattern };
        Assert.Equal(expected, query.CreateMatcher()(FileCatalogTests.Entry(name)));
    }

    [Fact]
    public void Matcher_CaseSensitiveAndEmptyConditions()
    {
        var row = FileCatalogTests.Entry("Book.txt");
        Assert.False(new FileCollectionQuery { Name = "book", IgnoreCase = false }.CreateMatcher()(row));
        Assert.False(new FileCollectionQuery { Name = "book*", NameMode = FileCollectionNameMode.Wildcard, IgnoreCase = false }.CreateMatcher()(row));
        Assert.True(new FileCollectionQuery().CreateMatcher()(row));
    }

    [Fact]
    public void Matcher_LocalDateIncludesWholeEndDateWithUtcComparison()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("TestKorea", TimeSpan.FromHours(9), "TestKorea", "TestKorea");
        var query = new FileCollectionQuery { ModifiedFrom = new(2024, 1, 2), ModifiedThrough = new(2024, 1, 2) };
        var matches = query.CreateMatcher(zone);
        var row = FileCatalogTests.Entry("a.txt");
        var start = DateTimeOffset.Parse("2024-01-01T15:00:00Z");
        Assert.False(matches(row with { ModifiedUtc = start.AddTicks(-1) }));
        Assert.True(matches(row with { ModifiedUtc = start }));
        Assert.True(matches(row with { ModifiedUtc = start.AddDays(1).AddTicks(-1) }));
        Assert.False(matches(row with { ModifiedUtc = start.AddDays(1) }));
    }

    [Theory]
    [InlineData("2024-03-10", "2024-03-10T05:00:00Z", 23)]
    [InlineData("2024-11-03", "2024-11-03T04:00:00Z", 25)]
    public void Matcher_DaylightSavingUsesCalendarDayBounds(string day, string startText, int hours)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var query = new FileCollectionQuery { CreatedFrom = DateOnly.Parse(day), CreatedThrough = DateOnly.Parse(day) };
        var matches = query.CreateMatcher(zone);
        var row = FileCatalogTests.Entry("a.txt"); var start = DateTimeOffset.Parse(startText);
        Assert.True(matches(row with { CreatedUtc = start }));
        Assert.True(matches(row with { CreatedUtc = start.AddHours(hours).AddTicks(-1) }));
        Assert.False(matches(row with { CreatedUtc = start.AddHours(hours) }));
    }

    [Fact]
    public void Matcher_CreatedAndModifiedRangesAreIndependentAndCombined()
    {
        var row = FileCatalogTests.Entry("a.txt");
        var query = new FileCollectionQuery { CreatedThrough = new(2024, 1, 1), ModifiedFrom = new(2024, 1, 2) };
        var matches = query.CreateMatcher(TimeZoneInfo.Utc);
        Assert.True(matches(row));
        Assert.False(matches(row with { CreatedUtc = row.CreatedUtc.AddDays(1) }));
        Assert.False(matches(row with { ModifiedUtc = row.ModifiedUtc.AddDays(-1) }));
    }

    [Fact]
    public void Matcher_RejectsInvalidRangesAndExtensions()
    {
        Assert.Throws<ArgumentException>(() => new FileCollectionQuery { MinimumSize = 2, MaximumSize = 1 }.CreateMatcher());
        Assert.Throws<ArgumentException>(() => new FileCollectionQuery { MinimumSize = -1 }.CreateMatcher());
        Assert.Throws<ArgumentException>(() => new FileCollectionQuery { CreatedFrom = new(2024, 2, 1), CreatedThrough = new(2024, 1, 1) }.CreateMatcher());
        Assert.Throws<ArgumentException>(() => new FileCollectionQuery { Extensions = ["jpg", "bad/path"] }.CreateMatcher());
    }

    [Fact]
    public void Scan_FilterKeepsCountsErrorsAndCustomKindsDistinct()
    {
        using var temp = TempDirectory.Create();
        File.WriteAllText(temp.GetPath("match.XYZ"), "match"); File.WriteAllText(temp.GetPath("other.txt"), "other");
        var settings = new FileToolsSettings { FileKindExtensionRules = [new() { Kind = "Custom", Extensions = [".xyz"] }] };
        var query = new FileCollectionQuery { Kinds = ["Custom"], Extensions = ["*.xyz"] };
        var result = FileCatalog.Scan([temp.Root, temp.GetPath("missing")], settings, new(), predicate: query.CreateMatcher());
        Assert.Equal("match.XYZ", Assert.Single(result.Entries).Name);
        Assert.Equal(2, result.ScannedFiles);
        Assert.Single(result.Errors);
        Assert.Equal("match", File.ReadAllText(temp.GetPath("match.XYZ")));
        Assert.Equal(2, Directory.GetFiles(temp.Root).Length);
    }

    [Fact]
    public void Scan_CancelsEvenWhenNoFilesMatch()
    {
        using var temp = TempDirectory.Create();
        File.WriteAllText(temp.GetPath("a.txt"), "a"); File.WriteAllText(temp.GetPath("b.txt"), "b");
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => FileCatalog.Scan([temp.Root], new(), new(), cancellation.Token,
            predicate: _ => { cancellation.Cancel(); return false; }));
    }
}
