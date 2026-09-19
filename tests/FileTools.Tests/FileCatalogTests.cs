using Microsoft.VisualBasic.FileIO;

namespace FileTools.Tests;

public sealed class FileCatalogTests
{
    [Fact]
    public void Scan_RejectsRelativeExclusionsAndCollectionOnlyAcceptsExistingFiles()
    {
        using var temp = TempDirectory.Create();
        var file = Write(temp, "a.txt");
        Assert.Throws<ArgumentException>(() => FileCatalog.Scan([temp.Root], new(), new() { ExcludedPaths = ["relative"] }));
        Assert.True(FileCatalog.IsRegularFile(file));
        Assert.False(FileCatalog.IsRegularFile(temp.Root));
        Assert.False(FileCatalog.IsRegularFile(temp.GetPath("missing")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Save_RejectsDestinationChangedAfterUserReview(bool existed)
    {
        using var temp = TempDirectory.Create(); var output = temp.GetPath("list.csv");
        if (existed) File.WriteAllText(output, "old");
        var review = FileListDestinationReview.Capture(output);
        File.WriteAllText(output, "new content after review");
        Assert.Throws<IOException>(() => FileListExport.Save([], output, new(), overwrite: true, destinationReview: review));
        Assert.Equal("new content after review", File.ReadAllText(output));
    }

    [Fact]
    public void Scan_DeduplicatesOverlappingRootsAndUsesOuterRelativePath()
    {
        using var temp = TempDirectory.Create();
        Directory.CreateDirectory(temp.GetPath("inner"));
        var file = Write(temp, "inner/한글.txt");
        var result = FileCatalog.Scan([file, temp.GetPath("inner"), temp.Root, file], new(), new());
        var row = Assert.Single(result.Entries);
        Assert.Empty(result.Errors);
        Assert.Equal(temp.Root, row.RootPath);
        Assert.Equal(Path.Combine("inner", "한글.txt"), row.RelativePath);
        Assert.Equal("Text", row.Kind);
        Assert.Equal(TimeSpan.Zero, row.CreatedUtc.Offset);
    }

    [Fact]
    public void Scan_NonRecursiveStillIncludesExplicitlySelectedDeepFolder()
    {
        using var temp = TempDirectory.Create();
        Directory.CreateDirectory(temp.GetPath("inner/deeper"));
        Write(temp, "top.txt"); Write(temp, "inner/skip.txt"); Write(temp, "inner/deeper/keep.txt");
        var result = FileCatalog.Scan([temp.Root, temp.GetPath("inner/deeper")], new(), new() { Recursive = false });
        Assert.Equal(["top.txt", "keep.txt"], result.Entries.Select(row => row.Name));
    }

    [Fact]
    public void Scan_HiddenAndSystemOptionsAndCustomClassification()
    {
        using var temp = TempDirectory.Create();
        var file = Write(temp, "custom.XYZ");
        File.SetAttributes(file, FileAttributes.Hidden | FileAttributes.System);
        var settings = new FileToolsSettings { FileKindExtensionRules = [new() { Kind = "Custom", Extensions = [".xyz"] }] };
        Assert.Empty(FileCatalog.Scan([temp.Root], settings, new()).Entries);
        Assert.Empty(FileCatalog.Scan([temp.Root], settings, new() { IncludeHidden = true }).Entries);
        var row = Assert.Single(FileCatalog.Scan([temp.Root], settings, new() { IncludeHidden = true, IncludeSystem = true }).Entries);
        Assert.Equal("Custom", row.Kind);
        Assert.Equal(".xyz", row.Extension);
    }

    [Fact]
    public void Scan_ExcludedDirectoryUsesPathBoundaries()
    {
        using var temp = TempDirectory.Create();
        Directory.CreateDirectory(temp.GetPath("skip")); Directory.CreateDirectory(temp.GetPath("skip-other"));
        Write(temp, "skip/a.txt"); var keep = Write(temp, "skip-other/a.txt");
        var result = FileCatalog.Scan([temp.Root], new(), new() { ExcludedPaths = [temp.GetPath("skip")] });
        Assert.Equal(Path.GetFullPath(keep), Assert.Single(result.Entries).FullPath);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void Scan_ReportsMissingPathsSeparatelyAndCancels()
    {
        using var temp = TempDirectory.Create();
        var file = Write(temp, "good.txt");
        var result = FileCatalog.Scan([file, temp.GetPath("missing")], new(), new());
        Assert.Single(result.Entries); Assert.Single(result.Errors);
        Assert.Throws<OperationCanceledException>(() => FileCatalog.Scan([temp.Root], new(), new(), new CancellationToken(true)));
    }

    [Fact]
    public void Sort_UsesNaturalNamesAndNumericSizes()
    {
        var entries = new[] { Entry("file10.txt") with { Size = 5 }, Entry("file2.txt") with { Size = 100 } };
        Assert.Equal("file2.txt", FileCatalog.Sort(entries, FileCatalogSort.Name)[0].Name);
        Assert.Equal(100, FileCatalog.Sort(entries, FileCatalogSort.Size, true)[0].Size);
    }

    [Fact]
    public void Csv_RoundTripsQuotesCommasUnicodeLargeSizeAndUtcDates()
    {
        using var temp = TempDirectory.Create();
        var output = temp.GetPath("list.csv");
        var row = Entry("한글,\"문서\".txt") with { Size = 9_000_000_001 };
        var saved = FileListExport.Save([row], output, new());
        Assert.Equal(1, saved.Written);
        Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, File.ReadAllBytes(output).Take(3));
        using var parser = new TextFieldParser(output) { HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        Assert.Equal(9, parser.ReadFields()!.Length);
        var values = parser.ReadFields()!;
        Assert.Equal(row.Name, values[0]);
        Assert.Equal("9000000001", values[6]);
        Assert.Equal(row.CreatedUtc, DateTimeOffset.Parse(values[7]));
        Assert.True(parser.EndOfData);
    }

    [Theory]
    [InlineData("=1+1.txt")]
    [InlineData("+SUM(1).txt")]
    [InlineData("-1.txt")]
    [InlineData("@formula.txt")]
    [InlineData("  =formula.txt")]
    [InlineData("\tformula")]
    public void Csv_ProtectsFormulaLikeText(string name)
    {
        using var temp = TempDirectory.Create();
        var output = temp.GetPath("list.csv");
        FileListExport.Save([Entry(name)], output, new() { Columns = [FileListColumn.Name] });
        using var parser = new TextFieldParser(output) { TrimWhiteSpace = false };
        parser.SetDelimiters(","); parser.ReadFields();
        Assert.Equal("'" + name, Assert.Single(parser.ReadFields()!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Text_UsesChosenFieldWithoutCsvTransformation(int field)
    {
        using var temp = TempDirectory.Create();
        var row = Entry("=한글.txt"); var output = temp.GetPath("list.txt");
        FileListExport.Save([row], output, new() { Format = FileListFormat.Text, TextField = (FileListTextField)field });
        var expected = field switch { 1 => row.RelativePath, 2 => row.Name, _ => row.FullPath };
        Assert.Equal(expected + "\r\n", File.ReadAllText(output));
    }

    [Fact]
    public void Save_ExcludesOwnOutputAndAllowsEmptyLists()
    {
        using var temp = TempDirectory.Create(); var output = temp.GetPath("list.csv");
        var saved = FileListExport.Save([Entry("list.csv") with { FullPath = output }], output, new());
        Assert.Equal(0, saved.Written); Assert.Equal(1, saved.ExcludedOutput);
        Assert.Single(File.ReadAllLines(output));
    }

    [Fact]
    public void Save_RejectsOverwriteWithoutApprovalAndPreservesDestinationOnFailure()
    {
        using var temp = TempDirectory.Create(); var output = Write(temp, "list.csv");
        Assert.Throws<IOException>(() => FileListExport.Save([], output, new()));
        Assert.Throws<IOException>(() => FileListExport.Save(FailingRows(), output, new(), overwrite: true));
        Assert.Equal("list.csv", File.ReadAllText(output));
        Assert.Empty(Directory.GetFiles(temp.Root, ".filetools-list-*.tmp"));
        static IEnumerable<FileCatalogEntry> FailingRows() { yield return Entry("a.txt"); throw new IOException("read failed"); }
    }

    [Fact]
    public void Save_CancellationDuringWritePreservesOldOutputAndRemovesTemporaryFile()
    {
        using var temp = TempDirectory.Create(); var output = Write(temp, "list.csv");
        using var cancellation = new CancellationTokenSource();
        var rows = Enumerable.Range(0, 256).Select(i => Entry(i + ".txt"));
        Assert.Throws<OperationCanceledException>(() => FileListExport.Save(rows, output, new(), overwrite: true,
            cancellationToken: cancellation.Token, progress: new CallbackProgress<int>(_ => cancellation.Cancel())));
        Assert.Equal("list.csv", File.ReadAllText(output));
        Assert.Empty(Directory.GetFiles(temp.Root, ".filetools-list-*.tmp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Save_DoesNotOverwriteOutputCreatedOrChangedDuringWrite(bool existed)
    {
        using var temp = TempDirectory.Create(); var output = temp.GetPath("list.csv");
        if (existed) File.WriteAllText(output, "old");
        var rows = Enumerable.Range(0, 128).Select(i => Entry(i + ".txt"));
        Assert.Throws<IOException>(() => FileListExport.Save(rows, output, new(), overwrite: existed,
            progress: new CallbackProgress<int>(_ => File.WriteAllText(output, "new content"))));
        Assert.Equal("new content", File.ReadAllText(output));
    }

    [Fact]
    public void Save_ApprovedReplacementUsesSelectedColumnOrder()
    {
        using var temp = TempDirectory.Create(); var output = Write(temp, "list.csv");
        FileListExport.Save([Entry("a.txt")], output, new() { Columns = [FileListColumn.Size, FileListColumn.Name] }, overwrite: true);
        using var parser = new TextFieldParser(output); parser.SetDelimiters(","); parser.ReadFields();
        Assert.Equal(["1", "a.txt"], parser.ReadFields());
    }

    internal static FileCatalogEntry Entry(string name) => new(Path.Combine("C:\\data", name), "C:\\data", name, name,
        Path.GetExtension(name), "Text", 1, DateTimeOffset.Parse("2024-01-01T00:00:00+00:00"), DateTimeOffset.Parse("2024-01-02T00:00:00+00:00"));
    private static string Write(TempDirectory temp, string name) { var path = temp.GetPath(name); File.WriteAllText(path, Path.GetFileName(name)); return path; }
    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T> { public void Report(T value) => callback(value); }
}
