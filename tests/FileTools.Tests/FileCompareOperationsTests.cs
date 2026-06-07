using FileTools;

namespace FileTools.Tests;

public sealed class FileCompareOperationsTests
{
    [Fact]
    public void CollectTargets_ExpandsFoldersAndCompareCreatesAllFilePairs()
    {
        using var temp = TempDirectory.Create();
        var leftFolder = temp.GetPath("left");
        var rightFolder = temp.GetPath("right");
        Directory.CreateDirectory(leftFolder);
        Directory.CreateDirectory(rightFolder);
        File.WriteAllText(Path.Combine(leftFolder, "a.txt"), "a");
        File.WriteAllText(Path.Combine(leftFolder, "b.txt"), "b");
        File.WriteAllText(Path.Combine(rightFolder, "c.txt"), "c");

        var report = FileCompareOperations.Compare(
            [leftFolder, rightFolder],
            ContentOnlyOptions(FileCompareContentMode.Hash));

        Assert.Equal(3, report.Targets.Count);
        Assert.Equal(3, report.Pairs.Count);
        Assert.All(report.Targets, target => Assert.NotNull(target.RootPath));
    }

    [Fact]
    public void Compare_ByteToByteReportsPartialMatchOnlyAfterPrefilterMatches()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("first.bin");
        var second = temp.GetPath("second.bin");
        File.WriteAllText(first, "abcdefghij");
        File.WriteAllText(second, "abcxxxxxxx");

        var report = FileCompareOperations.Compare(
            [first, second],
            ContentOnlyOptions(FileCompareContentMode.ByteToByte));

        var result = Assert.Single(report.Pairs);
        Assert.Equal(FileCompareStatus.PartialMatch, result.Status);
        Assert.InRange(result.MatchRatio, 0.10, 0.99);
    }

    [Fact]
    public void Compare_ByteToByteUsesLeadingHashPrefilterForEarlyDifference()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("first.bin");
        var second = temp.GetPath("second.bin");
        File.WriteAllText(first, "abcdefghij");
        File.WriteAllText(second, "xbcdefghij");

        var report = FileCompareOperations.Compare(
            [first, second],
            ContentOnlyOptions(FileCompareContentMode.ByteToByte));

        var result = Assert.Single(report.Pairs);
        Assert.Equal(FileCompareStatus.Different, result.Status);
        Assert.Equal(0, result.MatchRatio);
        Assert.Contains("prefilter", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_HashModeReusesRunCacheAcrossPairwiseComparisons()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("first.bin");
        var second = temp.GetPath("second.bin");
        var third = temp.GetPath("third.bin");
        File.WriteAllText(first, "same payload");
        File.WriteAllText(second, "same payload");
        File.WriteAllText(third, "same payload");

        var report = FileCompareOperations.Compare(
            [first, second, third],
            ContentOnlyOptions(FileCompareContentMode.Hash));

        Assert.Equal(3, report.Pairs.Count);
        Assert.All(report.Pairs, pair => Assert.Equal(FileCompareStatus.Same, pair.Status));
        Assert.Equal(3, report.HashCacheMisses);
        Assert.Equal(3, report.HashCacheHits);
    }

    [Fact]
    public void Compare_ArchiveEntryOrderCanUseOriginalOrderOrFileNameOrder()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("first.zip");
        var second = temp.GetPath("second.zip");
        ZipTestData.CreateStoredZip(
            first,
            new TestZipEntry("b.txt", "bravo"),
            new TestZipEntry("a.txt", "alpha"));
        ZipTestData.CreateStoredZip(
            second,
            new TestZipEntry("a.txt", "alpha"),
            new TestZipEntry("b.txt", "bravo"));

        var originalOrderReport = FileCompareOperations.Compare(
            [first, second],
            ArchiveOptions(FileCompareArchiveEntryOrder.Original));
        var fileNameOrderReport = FileCompareOperations.Compare(
            [first, second],
            ArchiveOptions(FileCompareArchiveEntryOrder.FileName));

        Assert.Equal(FileCompareStatus.Different, Assert.Single(originalOrderReport.Pairs).Status);
        Assert.Equal(FileCompareStatus.Same, Assert.Single(fileNameOrderReport.Pairs).Status);
    }

    [Fact]
    public void BuildDuplicateGroups_ConnectsSameContentPairsTransitively()
    {
        var first = CreateTarget(@"C:\data\a.bin");
        var second = CreateTarget(@"C:\data\b.bin");
        var third = CreateTarget(@"C:\data\c.bin");
        var report = new FileCompareReport(
            [first, second, third],
            [
                CreatePair(first, second, "Content", FileCompareStatus.Same),
                CreatePair(second, third, "Content", FileCompareStatus.Same)
            ],
            HashCacheHits: 0,
            HashCacheMisses: 0);

        var group = Assert.Single(FileCompareResultActions.BuildDuplicateGroups(report));

        Assert.Equal(3, group.Paths.Count);
        Assert.Equal(first.Path, group.KeepPath);
        Assert.Equal([second.Path, third.Path], group.DeleteCandidates);
    }

    [Fact]
    public void BuildDuplicateGroups_IgnoresMetadataOnlySamePairs()
    {
        var first = CreateTarget(@"C:\data\a.bin");
        var second = CreateTarget(@"C:\data\b.bin");
        var report = new FileCompareReport(
            [first, second],
            [CreatePair(first, second, "File size", FileCompareStatus.Same)],
            HashCacheHits: 0,
            HashCacheMisses: 0);

        Assert.Empty(FileCompareResultActions.BuildDuplicateGroups(report));
    }

    private static FileCompareOptions ContentOnlyOptions(FileCompareContentMode contentMode)
    {
        return new FileCompareOptions
        {
            CompareFileName = false,
            CompareCreatedTime = false,
            CompareModifiedTime = false,
            CompareFileSize = false,
            CompareContent = true,
            ContentMode = contentMode,
            PartialMatchThreshold = 0.10
        };
    }

    private static FileCompareOptions ArchiveOptions(FileCompareArchiveEntryOrder order)
    {
        return new FileCompareOptions
        {
            CompareFileName = true,
            NameMatchMode = FileCompareNameMatchMode.RelativePath,
            CompareCreatedTime = false,
            CompareModifiedTime = false,
            CompareFileSize = true,
            CompareContent = true,
            ContentMode = FileCompareContentMode.Hash,
            ArchiveMode = FileCompareArchiveMode.ExtractEntries,
            ArchiveEntryOrder = order
        };
    }

    private static FileCompareTarget CreateTarget(string path)
    {
        return new FileCompareTarget(path, Path.GetFileName(path), Path.GetDirectoryName(path));
    }

    private static FileComparePairResult CreatePair(
        FileCompareTarget left,
        FileCompareTarget right,
        string criterionName,
        FileCompareStatus status)
    {
        var ratio = status == FileCompareStatus.Same ? 1 : 0;
        return new FileComparePairResult(
            left,
            right,
            status,
            ratio,
            "test",
            [new FileCompareCriterionResult(criterionName, status, ratio, "test")]);
    }
}
