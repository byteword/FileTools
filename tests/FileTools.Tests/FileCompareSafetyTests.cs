using System.Runtime.InteropServices;
using System.Text.Json;

namespace FileTools.Tests;

public sealed class FileCompareSafetyTests
{
    private static FileCompareOptions ContentOnly(FileCompareContentMode mode = FileCompareContentMode.Hash)
    {
        var options = new FileCompareOptions();
        options.ApplyContentOnlyPreset();
        options.ContentMode = mode;
        return options;
    }

    private static string Write(TempDirectory temp, string name, string content)
    {
        var path = temp.GetPath(name);
        File.WriteAllText(path, content);
        return path;
    }

    private static DuplicateDeleteVerification Verification(params string[] paths) =>
        DuplicateDeleteVerification.Create(paths, paths.Skip(1),
            paths.ToDictionary(path => path, RenameFileSnapshot.Capture, StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData((int)FileCompareContentMode.Hash)]
    [InlineData((int)FileCompareContentMode.ByteToByte)]
    public void ContentOnly_FindsDifferentNamesAndDates(int mode)
    {
        using var temp = TempDirectory.Create();
        var first = Write(temp, "original.txt", "same content");
        var second = Write(temp, "renamed.bin", "same content");
        File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddDays(-30));
        var report = FileCompareOperations.Compare([first, second], ContentOnly((FileCompareContentMode)mode));
        Assert.True(Assert.Single(report.Pairs).WholeContentEqual);
        Assert.Single(FileCompareResultActions.BuildDuplicateGroups(report));
    }

    [Theory]
    [InlineData((int)FileCompareRangeMode.FrontBytes)]
    [InlineData((int)FileCompareRangeMode.BackBytes)]
    [InlineData((int)FileCompareRangeMode.MiddleBytes)]
    [InlineData((int)FileCompareRangeMode.FrontAndBackBytes)]
    public void PartialRanges_NeverBecomeDeleteCandidates(int range)
    {
        using var temp = TempDirectory.Create();
        var first = Write(temp, "a", "identical");
        var second = Write(temp, "b", "identical");
        var options = ContentOnly();
        options.RangeMode = (FileCompareRangeMode)range;
        options.RangeBytes = 3;
        var report = FileCompareOperations.Compare([first, second], options);
        Assert.Equal(FileCompareStatus.Same, Assert.Single(report.Pairs).Status);
        Assert.Equal(FileCompareScope.SelectedRange, report.Pairs[0].Scope);
        Assert.False(report.Pairs[0].WholeContentEqual);
        Assert.Empty(FileCompareResultActions.BuildDuplicateGroups(report));
    }

    [Fact]
    public void ZipFirstEntryMatch_IsNotWholeFileEvidence()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("first.zip");
        var second = temp.GetPath("second.zip");
        ZipTestData.CreateStoredZip(first, new TestZipEntry("a", "same"), new TestZipEntry("b", "first"));
        ZipTestData.CreateStoredZip(second, new TestZipEntry("a", "same"), new TestZipEntry("b", "other"));
        var options = ContentOnly();
        options.ArchiveMode = FileCompareArchiveMode.ExtractEntries;
        options.ArchiveEntryLimitMode = FileCompareArchiveEntryLimitMode.FirstN;
        options.ArchiveEntryLimitCount = 1;
        var report = FileCompareOperations.Compare([first, second], options);
        Assert.Equal(FileCompareStatus.Same, Assert.Single(report.Pairs).Status);
        Assert.Equal(FileCompareScope.ArchiveEntries, report.Pairs[0].Scope);
        Assert.Empty(FileCompareResultActions.BuildDuplicateGroups(report));
    }

    [Fact]
    public void MetadataOnly_DoesNotClaimContentEquality()
    {
        using var temp = TempDirectory.Create();
        var first = Write(temp, "a", "abc");
        var second = Write(temp, "b", "xyz");
        var options = ContentOnly();
        options.CompareContent = false;
        var report = FileCompareOperations.Compare([first, second], options);
        Assert.Equal(FileCompareStatus.Same, Assert.Single(report.Pairs).Status);
        Assert.Equal(FileCompareScope.MetadataOnly, report.Pairs[0].Scope);
        Assert.Empty(FileCompareResultActions.BuildDuplicateGroups(report));
    }

    [Theory]
    [InlineData("", "", true)]
    [InlineData("same", "same", true)]
    [InlineData("abcd", "abce", false)]
    [InlineData("a", "aa", false)]
    public void WholeFile_HandlesEmptyAndDifferentLengths(string left, string right, bool same)
    {
        using var temp = TempDirectory.Create();
        var report = FileCompareOperations.Compare([Write(temp, "a", left), Write(temp, "b", right)], ContentOnly());
        Assert.Equal(same, Assert.Single(report.Pairs).WholeContentEqual);
        Assert.Equal(same ? 1 : 0, FileCompareResultActions.BuildDuplicateGroups(report).Count);
    }

    [Fact]
    public void Preset_ResetsArchiveAndPartialOptionsWithoutChangingSavedOptions()
    {
        var saved = new FileCompareOptions { ArchiveMode = FileCompareArchiveMode.ExtractEntries,
            RangeMode = FileCompareRangeMode.FrontBytes, CompareCreatedTime = true };
        var run = saved.Clone();
        run.ApplyContentOnlyPreset();
        Assert.Equal(FileCompareRangeMode.Full, run.RangeMode);
        Assert.Equal(FileCompareArchiveMode.AsFile, run.ArchiveMode);
        Assert.False(run.CompareFileName || run.CompareCreatedTime || run.CompareModifiedTime);
        Assert.True(saved.CompareFileName && saved.CompareCreatedTime);
        Assert.Equal(FileCompareRangeMode.FrontBytes, saved.RangeMode);
    }

    [Fact]
    public void WholeContentProof_IsIndependentOfMetadataStatus()
    {
        using var temp = TempDirectory.Create();
        var options = ContentOnly();
        options.CompareFileName = true;
        options.EnableEarlyExit = false;
        var report = FileCompareOperations.Compare([Write(temp, "a", "same"), Write(temp, "b", "same")], options);
        Assert.Equal(FileCompareStatus.PartialMatch, Assert.Single(report.Pairs).Status);
        Assert.True(report.Pairs[0].WholeContentEqual);
        Assert.Single(FileCompareResultActions.BuildDuplicateGroups(report));
    }

    [Fact]
    public void CancelledBeforeCollection_ThrowsEvenForNoTargets()
    {
        Assert.Throws<OperationCanceledException>(() => FileCompareOperations.Compare([], ContentOnly(),
            cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void CancellationBetweenPairs_DoesNotReturnCompleteReport()
    {
        using var temp = TempDirectory.Create();
        using var cancel = new CancellationTokenSource();
        var paths = new[] { Write(temp, "a", "same"), Write(temp, "b", "same"), Write(temp, "c", "same") };
        Assert.Throws<OperationCanceledException>(() => FileCompareOperations.Compare(paths, ContentOnly(),
            new InlineProgress(_ => cancel.Cancel()), cancel.Token));
    }

    [Fact]
    public void FileChangedBetweenPairs_IsFailedInsteadOfCachedEquality()
    {
        using var temp = TempDirectory.Create();
        var first = Write(temp, "a", "same");
        var paths = new[] { first, Write(temp, "b", "same"), Write(temp, "c", "same") };
        var report = FileCompareOperations.Compare(paths, ContentOnly(), new InlineProgress(progress =>
        {
            if (progress.CompletedPairs == 1) File.WriteAllText(first, "changed length");
        }));
        Assert.Contains(report.Pairs, pair => pair.Left.Path == first && pair.Status == FileCompareStatus.Failed);
    }

    [Fact]
    public void JsonExport_StatesScopeAndWholeContentEvidence()
    {
        using var temp = TempDirectory.Create();
        var report = FileCompareOperations.Compare([Write(temp, "a", "same"), Write(temp, "b", "same")], ContentOnly());
        var path = temp.GetPath("result.json");
        FileCompareResultExport.Save(path, report, ContentOnly(), FileCompareDuplicateKeepMode.ComparisonOrder,
            FileCompareResultActions.BuildDuplicateGroups(report));
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(3, json.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("WholeFile", json.RootElement.GetProperty("Pairs")[0].GetProperty("Scope").GetString());
        Assert.True(json.RootElement.GetProperty("Pairs")[0].GetProperty("WholeContentEqual").GetBoolean());
    }

    [Theory]
    [InlineData("missing-keeper")]
    [InlineData("changed-candidate")]
    [InlineData("replaced-candidate")]
    [InlineData("changed-keeper")]
    public void Recycle_RechecksBeforeCallingShell(string change)
    {
        using var temp = TempDirectory.Create();
        var keep = Write(temp, "keep", "same");
        var candidate = Write(temp, "copy", "same");
        var proof = Verification(keep, candidate);
        switch (change)
        {
            case "missing-keeper": File.Delete(keep); break;
            case "changed-candidate": File.WriteAllText(candidate, "different"); break;
            case "changed-keeper": File.WriteAllText(keep, "different"); break;
            case "replaced-candidate": File.Move(candidate, candidate + ".old"); File.WriteAllText(candidate, "same"); break;
        }
        var called = false;
        var result = DuplicateDeleteOperations.MoveFileToRecycleBin(candidate, proof,
            recycle: (_, _) => called = true);
        Assert.True(result.HasErrors);
        Assert.False(called);
        Assert.True(File.Exists(candidate));
    }

    [Fact]
    public void Recycle_RechecksBytesEvenWhenSizeAndTimestampRestored()
    {
        using var temp = TempDirectory.Create();
        var keep = Write(temp, "keep", "same");
        var candidate = Write(temp, "copy", "same");
        var modified = File.GetLastWriteTimeUtc(candidate);
        var proof = Verification(keep, candidate);
        File.WriteAllText(candidate, "fake");
        File.SetLastWriteTimeUtc(candidate, modified);
        var called = false;
        var result = DuplicateDeleteOperations.MoveFileToRecycleBin(candidate, proof, recycle: (_, _) => called = true);
        Assert.True(result.HasErrors);
        Assert.False(called);
    }

    [Fact]
    public void Recycle_HoldsKeeperAndChecksAtShellBoundary()
    {
        using var temp = TempDirectory.Create();
        var keep = Write(temp, "keep", "same");
        var candidate = Write(temp, "copy", "same");
        var proof = Verification(keep, candidate);
        var called = false;
        var result = DuplicateDeleteOperations.MoveFileToRecycleBin(candidate, proof, recycle: (_, verify) =>
        {
            Assert.Throws<IOException>(() => File.Delete(keep));
            Assert.Throws<IOException>(() => File.WriteAllText(candidate, "fake"));
            verify();
            called = true;
        });
        Assert.False(result.HasErrors);
        Assert.True(called);
    }

    [Fact]
    public void Recycle_CancelledOrMissingProofNeverCallsShell()
    {
        using var temp = TempDirectory.Create();
        var keep = Write(temp, "keep", "same");
        var candidate = Write(temp, "copy", "same");
        var called = false;
        Assert.True(DuplicateDeleteOperations.MoveFileToRecycleBin(candidate, recycle: (_, _) => called = true).HasErrors);
        Assert.Throws<OperationCanceledException>(() => DuplicateDeleteOperations.MoveFileToRecycleBin(candidate,
            Verification(keep, candidate), new CancellationToken(true), (_, _) => called = true));
        Assert.False(called);
    }

    [Fact]
    public void Selection_RejectsAllDeleteWithoutMutatingExistingSteps()
    {
        using var temp = TempDirectory.Create();
        var paths = new[] { Write(temp, "a", "same"), Write(temp, "b", "same") };
        var targets = paths.Select(path => new WorkTargetPlan(path)).ToArray();
        var snapshots = Verification(paths).Snapshots;
        DuplicateDeleteStepSelection.Apply(targets, [paths[1]], paths, snapshots);
        var original = Assert.Single(targets[1].Steps);
        Assert.Throws<InvalidOperationException>(() => DuplicateDeleteStepSelection.Apply(targets, paths, paths, snapshots));
        Assert.Same(original, Assert.Single(targets[1].Steps));
        Assert.Empty(targets[0].Steps);
    }

    [Fact]
    public void Selection_CanSwapKeeperAndCarriesEvidenceThroughClone()
    {
        using var temp = TempDirectory.Create();
        var paths = new[] { Write(temp, "a", "same"), Write(temp, "b", "same") };
        var targets = paths.Select(path => new WorkTargetPlan(path)).ToArray();
        var snapshots = Verification(paths).Snapshots;
        DuplicateDeleteStepSelection.Apply(targets, [paths[1]], paths, snapshots);
        DuplicateDeleteStepSelection.Apply(targets, [paths[0]], paths, snapshots);
        Assert.Empty(targets[1].Steps);
        var cloned = Assert.Single(targets[0].Steps).Clone();
        Assert.Equal(paths[1], Assert.Single(cloned.DuplicateDeleteVerification!.KeepPaths));
        Assert.Equal(snapshots[paths[0]], cloned.DuplicateDeleteVerification.Snapshots[paths[0]]);
    }

    [Fact]
    public void ConflictingRenamePlan_BlocksRecycle()
    {
        using var temp = TempDirectory.Create();
        var paths = new[] { Write(temp, "a", "same"), Write(temp, "b", "same") };
        var targets = paths.Select(path => new WorkTargetPlan(path)).ToArray();
        DuplicateDeleteStepSelection.Apply(targets, [paths[1]], paths, Verification(paths).Snapshots);
        targets[0].Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.FileNameCorrection, ManualRenameFileName = "renamed" });
        // Candidate is first: the future keeper rename must already block its cleanup.
        var result = new WorkPlanExecutor(new FileToolsSettings()).Run(targets.Reverse());
        Assert.True(result.HasErrors);
        Assert.True(File.Exists(paths[1]));
    }

    [Fact]
    public void HardLinks_AreNotIndependentDuplicateCandidates()
    {
        using var temp = TempDirectory.Create();
        var first = Write(temp, "a", "same");
        var second = temp.GetPath("b");
        Assert.True(CreateHardLink(second, first, IntPtr.Zero));
        var report = FileCompareOperations.Compare([first, second], ContentOnly());
        Assert.True(Assert.Single(report.Pairs).WholeContentEqual);
        Assert.Empty(FileCompareResultActions.BuildDuplicateGroups(report));
        Assert.Throws<InvalidOperationException>(() => Verification(first, second));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

    [Fact]
    public void Hash_TruncatedStreamFailsAndDoesNotPopulateCache()
    {
        var cache = new FileCompareOperations.FileCompareHashCache(true);
        Assert.Throws<EndOfStreamException>(() => FileCompareOperations.GetHash(
            () => new MemoryStream(new byte[3]), "file", [new(0, 10)], cache, CancellationToken.None));
        Assert.Equal(0, cache.Misses);
    }

    [Fact]
    public void Hash_ShortReadsAreNotMistakenForEndOfStream()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("short reads still contain the whole content");
        var actual = FileCompareOperations.GetHash(() => new ShortReadStream(data), null,
            [new(0, data.Length)], new(false), CancellationToken.None);
        Assert.Equal(System.Security.Cryptography.SHA256.HashData(data), actual);
    }

    [Fact]
    public void Hash_CancelsDuringAFileReadWithoutCachingPartialContent()
    {
        using var cancel = new CancellationTokenSource();
        var cache = new FileCompareOperations.FileCompareHashCache(true);
        Assert.Throws<OperationCanceledException>(() => FileCompareOperations.GetHash(
            () => new ShortReadStream(new byte[1024], cancel.Cancel), "file", [new(0, 1024)], cache, cancel.Token));
        Assert.Equal(0, cache.Misses);
    }

    [Fact]
    public void LockedFile_IsFailedAndCannotBeCleanedUp()
    {
        using var temp = TempDirectory.Create();
        var a = Write(temp, "a", "same");
        var b = Write(temp, "b", "same");
        using var locked = new FileStream(b, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var report = FileCompareOperations.Compare([a, b], ContentOnly());
        Assert.Equal(FileCompareStatus.Failed, Assert.Single(report.Pairs).Status);
        Assert.Empty(FileCompareResultActions.BuildDuplicateGroups(report));
    }

    private sealed class ShortReadStream(byte[] data, Action? afterRead = null) : MemoryStream(data)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, Math.Min(count, 3));
            afterRead?.Invoke();
            return read;
        }
    }

    [Fact]
    public void ContentCriterionNameAlone_IsNotDeletionEvidence()
    {
        using var temp = TempDirectory.Create();
        var targets = FileCompareOperations.CollectTargets([Write(temp, "a", "same"), Write(temp, "b", "same")]);
        var pair = new FileComparePairResult(targets[0], targets[1], FileCompareStatus.Same, 1, "legacy",
            [new("Content", FileCompareStatus.Same, 1, "legacy")]);
        Assert.Empty(FileCompareResultActions.BuildDuplicateGroups(new(targets, [pair], 0, 0)));
    }

    [Fact]
    public void EarlyMetadataExit_DoesNotClaimWholeFileWasCompared()
    {
        using var temp = TempDirectory.Create();
        var report = FileCompareOperations.Compare([Write(temp, "a", "same"), Write(temp, "b", "same")]);
        Assert.Equal(FileCompareScope.MetadataOnly, Assert.Single(report.Pairs).Scope);
        Assert.False(report.Pairs[0].WholeContentEqual);
    }

    [Fact]
    public void ArchiveOutputOverlappingKeeper_IsAConflict()
    {
        using var temp = TempDirectory.Create();
        var keep = Write(temp, "keep", "same");
        var copy = Write(temp, "copy", "same");
        var unrelated = new WorkTargetPlan(Write(temp, "unrelated", "other"));
        unrelated.Steps.Add(new() { Kind = WorkPlanStepKind.ArchiveMerge,
            ArchiveMergeOptions = new() { OutputPath = keep } });
        Assert.True(Verification(keep, copy).ConflictsWith([unrelated]));
    }

    [Fact]
    public void ShellBoundaryReplacement_IsRejected()
    {
        using var temp = TempDirectory.Create();
        var keep = Write(temp, "keep", "same");
        var copy = Write(temp, "copy", "same");
        var proof = Verification(keep, copy);
        var result = DuplicateDeleteOperations.MoveFileToRecycleBin(copy, proof, recycle: (_, verify) =>
        {
            File.Move(copy, copy + ".old");
            File.WriteAllText(copy, "same");
            verify();
            throw new Exception("Replacement was allowed");
        });
        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, error => error.Contains(Localizer.Get("DuplicateRecheckRequired")));
        Assert.True(File.Exists(copy));
    }

    [Fact]
    public void UnavailableRecycle_DoesNotFallBackToDeletingFile()
    {
        using var temp = TempDirectory.Create();
        var keep = Write(temp, "keep", "same");
        var copy = Write(temp, "copy", "same");
        var result = DuplicateDeleteOperations.MoveFileToRecycleBin(copy, Verification(keep, copy),
            recycle: (_, _) => throw new IOException("Recycle unavailable"));
        Assert.True(result.HasErrors);
        Assert.Equal(0, result.AppliedCount);
        Assert.True(File.Exists(copy) && File.Exists(keep));
    }

    [Fact]
    public void CancelAtShellBoundary_DoesNotCompleteOperation()
    {
        using var temp = TempDirectory.Create();
        using var cancel = new CancellationTokenSource();
        var keep = Write(temp, "keep", "same");
        var copy = Write(temp, "copy", "same");
        Assert.Throws<OperationCanceledException>(() => DuplicateDeleteOperations.MoveFileToRecycleBin(copy,
            Verification(keep, copy), cancel.Token, (_, verify) => { cancel.Cancel(); verify(); }));
        Assert.True(File.Exists(copy) && File.Exists(keep));
    }

    private sealed class InlineProgress(Action<FileCompareProgress> report) : IProgress<FileCompareProgress>
    {
        public void Report(FileCompareProgress value) => report(value);
    }
}
