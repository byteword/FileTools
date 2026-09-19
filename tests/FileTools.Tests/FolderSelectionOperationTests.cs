using FileTools;

namespace FileTools.Tests;

public sealed class FolderSelectionOperationTests
{
    [Fact]
    public void Merge_DefaultCombinesMatchingSubfoldersAndPreservesConflictingFiles()
    {
        using var temp = TempDirectory.Create();
        var first = CreateFolder(temp, "First", "Shared", "same.txt", "one");
        var second = CreateFolder(temp, "Second", "Shared", "same.txt", "two");
        File.WriteAllText(Path.Combine(second, "Shared", "other.txt"), "other");

        var merge = FolderMergeOperations.MergeIntoFolder([first, second], new FileToolsSettings());

        Assert.Empty(merge.OperationResult.Errors);
        Assert.NotNull(merge.TargetFolderPath);
        var shared = Path.Combine(merge.TargetFolderPath, "Shared");
        Assert.Equal("one", File.ReadAllText(Path.Combine(shared, "same.txt")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(shared, "same (2).txt")));
        Assert.Equal("other", File.ReadAllText(Path.Combine(shared, "other.txt")));
        Assert.Single(Directory.GetDirectories(merge.TargetFolderPath));
        Assert.False(Directory.Exists(first));
        Assert.False(Directory.Exists(second));
    }

    [Fact]
    public void Merge_SkipCollisionLeavesOnlyConflictingSourceInPlace()
    {
        using var temp = TempDirectory.Create();
        var first = CreateFolder(temp, "First", "Shared", "same.txt", "one");
        var second = CreateFolder(temp, "Second", "Shared", "same.txt", "two");
        File.WriteAllText(Path.Combine(second, "Shared", "other.txt"), "other");

        var merge = FolderMergeOperations.MergeIntoFolder([first, second], new FileToolsSettings(),
            new FolderMergeOptions("Combined", FolderMergeMode.MergeFolderContentsOnly, NameCollisionPolicy.Skip));

        Assert.Empty(merge.OperationResult.Errors);
        Assert.Equal(1, merge.OperationResult.SkippedCount);
        Assert.Equal("one", File.ReadAllText(temp.GetPath("Combined/Shared/same.txt")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(second, "Shared", "same.txt")));
        Assert.Equal("other", File.ReadAllText(temp.GetPath("Combined/Shared/other.txt")));
        Assert.False(File.Exists(temp.GetPath("Combined/Shared/same (2).txt")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Merge_FileAndFolderNameCollisionUsesSelectedPolicy(bool autoNumber)
    {
        var policy = autoNumber ? NameCollisionPolicy.AutoNumber : NameCollisionPolicy.Skip;
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("First");
        Directory.CreateDirectory(first);
        File.WriteAllText(Path.Combine(first, "Clash"), "file");
        var second = CreateFolder(temp, "Second", "Clash", "inner.txt", "nested");

        var merge = FolderMergeOperations.MergeIntoFolder([first, second], new FileToolsSettings(),
            new FolderMergeOptions("Combined", FolderMergeMode.MergeFolderContentsOnly, policy));

        Assert.Empty(merge.OperationResult.Errors);
        Assert.Equal("file", File.ReadAllText(temp.GetPath("Combined/Clash")));
        var nestedPath = policy == NameCollisionPolicy.AutoNumber
            ? temp.GetPath("Combined/Clash (2)/inner.txt")
            : Path.Combine(second, "Clash", "inner.txt");
        Assert.Equal("nested", File.ReadAllText(nestedPath));
    }

    [Fact]
    public void Merge_LockedNestedFileDoesNotStopSiblingsOrOtherSources()
    {
        using var temp = TempDirectory.Create();
        var first = CreateFolder(temp, "First", "Shared", "00-locked.txt", "locked");
        var second = CreateFolder(temp, "Second", "Shared", "second.txt", "second");
        var lockedPath = Path.Combine(first, "Shared", "00-locked.txt");
        File.WriteAllText(Path.Combine(first, "Shared", "10-free.txt"), "free");
        using var fileLock = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var merge = FolderMergeOperations.MergeIntoFolder([first, second], new FileToolsSettings(),
            new FolderMergeOptions("Combined", FolderMergeMode.MergeFolderContentsOnly));

        Assert.Contains(lockedPath, Assert.Single(merge.OperationResult.Errors));
        Assert.Equal(2, merge.OperationResult.AppliedCount);
        Assert.True(File.Exists(lockedPath));
        Assert.Equal("free", File.ReadAllText(temp.GetPath("Combined/Shared/10-free.txt")));
        Assert.Equal("second", File.ReadAllText(temp.GetPath("Combined/Shared/second.txt")));
        Assert.False(Directory.Exists(second));
    }

    [Fact]
    public void Unwrap_LockedEntryPreservesCountsAndCanResumeAfterUnlocking()
    {
        using var temp = TempDirectory.Create();
        var outer = CreateFolder(temp, "A", "A", "inner.txt", "inner");
        var lockedPath = Path.Combine(outer, "00-locked.txt");
        File.WriteAllText(lockedPath, "locked");
        File.WriteAllText(Path.Combine(outer, "10-free.txt"), "free");
        var runner = new FileToolRunner(new FileToolsSettings
        {
            FolderStructureOperation = FolderStructureOperation.MoveInnerFilesUp
        });

        using (var fileLock = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var partial = runner.Run(ToolMode.FolderStructure, [outer]);
            Assert.Equal(1, partial.AppliedCount);
            Assert.Contains(lockedPath, Assert.Single(partial.Errors));
            Assert.Equal("free", File.ReadAllText(temp.GetPath("10-free.txt")));
            Assert.True(Directory.Exists(Path.Combine(outer, "A")));
            Assert.Empty(Directory.GetDirectories(temp.Root, ".FileTools.MoveUp.*"));
        }

        var retry = runner.Run(ToolMode.FolderStructure, [outer]);

        Assert.Empty(retry.Errors);
        Assert.Equal(2, retry.AppliedCount);
        Assert.Equal("locked", File.ReadAllText(temp.GetPath("00-locked.txt")));
        Assert.Equal("inner", File.ReadAllText(Path.Combine(outer, "inner.txt")));
        Assert.False(Directory.Exists(Path.Combine(outer, "A")));
        Assert.Empty(Directory.GetDirectories(temp.Root, ".FileTools.MoveUp.*"));
    }

    [Fact]
    public void WorkPlan_PartialUnwrapKeepsStepAndStopsDependentRename()
    {
        using var temp = TempDirectory.Create();
        var source = temp.GetPath("Source");
        Directory.CreateDirectory(source);
        var lockedPath = Path.Combine(source, "locked.txt");
        File.WriteAllText(lockedPath, "locked");
        File.WriteAllText(Path.Combine(source, "free.txt"), "free");
        using var fileLock = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var target = new WorkTargetPlan(source);
        target.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.FolderUnwrap,
            FolderOperation = FolderStructureOperation.MoveInnerFilesUp });
        target.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.FileNameCorrection,
            ManualRenameFileName = "ShouldNotRun" });

        var execution = new WorkPlanExecutor(new FileToolsSettings())
            .RunDetailed([target], CancellationToken.None, null);

        Assert.Equal(1, execution.Result.AppliedCount);
        Assert.Empty(Assert.Single(execution.Targets).CompletedSteps);
        Assert.True(File.Exists(lockedPath));
        Assert.False(Directory.Exists(temp.GetPath("ShouldNotRun")));
    }

    [Fact]
    public void Wrap_MixedSelectionPreservesWholeFolderStructureAndExistingDestination()
    {
        using var temp = TempDirectory.Create();
        var folder = CreateFolder(temp, "Folder", "Nested", "inner.txt", "inner");
        var file = temp.GetPath("file.txt");
        File.WriteAllText(file, "file");
        Directory.CreateDirectory(temp.GetPath("Wrapped"));
        File.WriteAllText(temp.GetPath("Wrapped/existing.txt"), "existing");

        var result = FolderMergeOperations.MergeIntoFolder([file, folder], new FileToolsSettings(),
            new FolderMergeOptions("Wrapped", FolderMergeMode.MergeFolderUnits));

        Assert.Empty(result.OperationResult.Errors);
        Assert.Equal(temp.GetPath("Wrapped (2)"), result.TargetFolderPath);
        Assert.Equal("file", File.ReadAllText(temp.GetPath("Wrapped (2)/file.txt")));
        Assert.Equal("inner", File.ReadAllText(temp.GetPath("Wrapped (2)/Folder/Nested/inner.txt")));
        Assert.Equal("existing", File.ReadAllText(temp.GetPath("Wrapped/existing.txt")));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void Wrap_AllowsSingleFolderAndMergeRejectsFiles()
    {
        using var temp = TempDirectory.Create();
        var folder = CreateFolder(temp, "Folder", "Nested", "inner.txt", "inner");
        var file = temp.GetPath("file.txt");
        File.WriteAllText(file, "file");
        var invalid = FolderMergeOperations.CreateMergePlanPreview([folder, file], new FileToolsSettings());
        Assert.False(invalid.IsReady);

        var wrap = FolderMergeOperations.MergeIntoFolder([folder], new FileToolsSettings(),
            new FolderMergeOptions("Wrapped", FolderMergeMode.MergeFolderUnits));

        Assert.Empty(wrap.OperationResult.Errors);
        Assert.Equal("inner", File.ReadAllText(temp.GetPath("Wrapped/Folder/Nested/inner.txt")));
    }

    [Fact]
    public void Wrap_LockedFileStaysInPlaceWhileOtherSelectedFilesMove()
    {
        using var temp = TempDirectory.Create();
        var locked = temp.GetPath("locked.txt");
        var free = temp.GetPath("free.txt");
        File.WriteAllText(locked, "locked");
        File.WriteAllText(free, "free");
        using var fileLock = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read);

        var result = FolderMergeOperations.MergeIntoFolder([locked, free], new FileToolsSettings(),
            new FolderMergeOptions("Wrapped", FolderMergeMode.MergeFolderUnits));

        Assert.Contains(locked, Assert.Single(result.OperationResult.Errors));
        Assert.Equal(1, result.OperationResult.AppliedCount);
        Assert.True(File.Exists(locked));
        Assert.Equal("free", File.ReadAllText(temp.GetPath("Wrapped/free.txt")));
    }

    [Fact]
    public void Merge_WhenEveryNestedFileIsLockedLeavesNoEmptyDestination()
    {
        using var temp = TempDirectory.Create();
        var first = CreateFolder(temp, "First", "Shared", "one.txt", "one");
        var second = CreateFolder(temp, "Second", "Shared", "two.txt", "two");
        using var firstLock = new FileStream(Path.Combine(first, "Shared", "one.txt"),
            FileMode.Open, FileAccess.Read, FileShare.Read);
        using var secondLock = new FileStream(Path.Combine(second, "Shared", "two.txt"),
            FileMode.Open, FileAccess.Read, FileShare.Read);

        var result = FolderMergeOperations.MergeIntoFolder([first, second], new FileToolsSettings(),
            new FolderMergeOptions("Combined", FolderMergeMode.MergeFolderContentsOnly));

        Assert.Equal(2, result.OperationResult.Errors.Count);
        Assert.Equal(0, result.OperationResult.AppliedCount);
        Assert.Null(result.TargetFolderPath);
        Assert.False(Directory.Exists(temp.GetPath("Combined")));
    }

    [Fact]
    public void Merge_RejectsOverlappingFoldersBeforeMovingAnything()
    {
        using var temp = TempDirectory.Create();
        var folder = CreateFolder(temp, "Folder", "Nested", "inner.txt", "inner");

        var result = FolderMergeOperations.MergeIntoFolder(
            [Path.Combine(folder, "Nested"), folder], new FileToolsSettings());

        Assert.Null(result.TargetFolderPath);
        Assert.Equal(0, result.OperationResult.AppliedCount);
        Assert.Equal("inner", File.ReadAllText(Path.Combine(folder, "Nested", "inner.txt")));
        Assert.Single(Directory.GetDirectories(folder));
    }

    private static string CreateFolder(TempDirectory temp, string name, string child, string file, string content)
    {
        var folder = temp.GetPath(name);
        var nested = Path.Combine(folder, child);
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, file), content);
        return folder;
    }

    [Fact]
    public void Wrap_RejectsDestinationCreatedAfterConfirmation()
    {
        using var temp = TempDirectory.Create();
        var file = temp.GetPath("source.txt");
        File.WriteAllText(file, "source");
        var destination = temp.GetPath("Wrapped");
        var options = new FolderMergeOptions("Wrapped", FolderMergeMode.MergeFolderUnits)
        {
            ConfirmedTargetFolderPath = destination
        };
        Directory.CreateDirectory(destination);

        var result = FolderMergeOperations.MergeIntoFolder([file], new FileToolsSettings(), options);

        Assert.Null(result.TargetFolderPath);
        Assert.Equal(0, result.OperationResult.AppliedCount);
        Assert.Equal("source", File.ReadAllText(file));
        Assert.False(Directory.Exists(temp.GetPath("Wrapped (2)")));
    }
}
