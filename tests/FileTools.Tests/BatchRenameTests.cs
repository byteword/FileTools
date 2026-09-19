namespace FileTools.Tests;

public sealed class BatchRenameTests
{
    [Fact]
    public void Build_ComposesLiteralEditsAndPreservesExtension()
    {
        using var temp = TempDirectory.Create();
        var file = Create(temp, "OLD_book_draft.TXT");
        var row = Assert.Single(BatchRenamePlanBuilder.Build([file], new()
        {
            RemovePrefix = "old_", RemoveSuffix = "_draft", Find = "book", ReplaceWith = "자료",
            Prefix = "완료_", Suffix = "_확인", Pattern = "{Index}_{Stem}", Start = 7, Digits = 2
        }).Rows);
        Assert.Equal("07_완료_자료_확인.TXT", Path.GetFileName(row.TargetPath));
        Assert.Equal(BatchRenameStatus.Ready, row.Status);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Build_NaturalOrderAndExclusionAssignStableNumbers()
    {
        using var temp = TempDirectory.Create();
        var files = new[] { Create(temp, "book10.txt"), Create(temp, "book2.txt"), Create(temp, "book1.txt") };
        var rows = BatchRenamePlanBuilder.Build(files, new() { Pattern = "{Index:000}-{Stem}", Start = 5, Increment = 2 }, [files[1]]).Rows;
        Assert.Equal(["005-book1.txt", "book2.txt", "007-book10.txt"], rows.Select(row => Path.GetFileName(row.TargetPath)));
        Assert.Equal(BatchRenameStatus.Excluded, rows[1].Status);
        Assert.Null(rows[1].Number);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Build_DateOrderUsesOldestFirst(int order)
    {
        using var temp = TempDirectory.Create();
        var first = Create(temp, "a.txt");
        var second = Create(temp, "b.txt");
        File.SetLastWriteTimeUtc(second, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(second, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var rows = BatchRenamePlanBuilder.Build([first, second], new() { Order = (BatchRenameOrder)order, Pattern = "{Index}" }).Rows;
        Assert.Equal(second, rows[0].OriginalPath);
        Assert.Equal("001.txt", Path.GetFileName(rows[0].TargetPath));
    }

    [Fact]
    public void Build_InputOrderAndExtensionEditing()
    {
        using var temp = TempDirectory.Create();
        var files = new[] { Create(temp, "b.TXT"), Create(temp, "a.TXT") };
        var rows = BatchRenamePlanBuilder.Build(files, new() { Order = BatchRenameOrder.Selection,
            PreserveExtension = false, Find = ".TXT", ReplaceWith = ".md", Pattern = "{Stem}-{Index}", Digits = 1 }).Rows;
        Assert.Equal(["b.md-1", "a.md-2"], rows.Select(row => Path.GetFileName(row.TargetPath)));
    }

    [Fact]
    public void Build_CaseSensitiveReplacementAndUnchangedAreDistinct()
    {
        using var temp = TempDirectory.Create();
        var file = Create(temp, "Book.txt");
        var row = Assert.Single(BatchRenamePlanBuilder.Build([file], new() { Find = "book", ReplaceWith = "Other", IgnoreCase = false }).Rows);
        Assert.Equal(BatchRenameStatus.Unchanged, row.Status);
    }

    [Theory]
    [InlineData("CON.tar.gz")]
    [InlineData("LPT¹.txt")]
    [InlineData("CON .txt")]
    [InlineData("../escape")]
    [InlineData("name.")]
    [InlineData("name ")]
    [InlineData("{Unknown}")]
    [InlineData("{Index:abc}")]
    [InlineData("")]
    public void Build_BlocksInvalidNamesAndPatterns(string pattern)
    {
        using var temp = TempDirectory.Create();
        var row = Assert.Single(BatchRenamePlanBuilder.Build([Create(temp, "file.txt")], new() { Pattern = pattern, PreserveExtension = false }).Rows);
        Assert.Equal(BatchRenameStatus.Blocked, row.Status);
    }

    [Fact]
    public void Build_BlocksDuplicateResultsAndExistingDestination()
    {
        using var temp = TempDirectory.Create();
        var files = new[] { Create(temp, "one.txt"), Create(temp, "two.txt") };
        var rows = BatchRenamePlanBuilder.Build(files, new() { Pattern = "same" }).Rows;
        Assert.All(rows, row => Assert.Equal(BatchRenameStatus.Blocked, row.Status));
        var existing = Create(temp, "same.txt");
        rows = BatchRenamePlanBuilder.Build(files, new() { Pattern = "same" }).Rows;
        Assert.All(rows, row => { Assert.Equal(BatchRenameStatus.Blocked, row.Status); Assert.Equal(existing, row.TargetPath); });
        Assert.Equal("same.txt", File.ReadAllText(existing));
    }

    [Fact]
    public void Build_SameNewNameInDifferentDirectoriesIsAllowed()
    {
        using var temp = TempDirectory.Create();
        Directory.CreateDirectory(temp.GetPath("other"));
        var rows = BatchRenamePlanBuilder.Build([Create(temp, "a.txt"), Create(temp, "other/b.txt")], new() { Pattern = "same" }).Rows;
        Assert.All(rows, row => Assert.Equal(BatchRenameStatus.Ready, row.Status));
    }

    [Fact]
    public void Build_ReportsMissingFileAndOverflowAndHonorsCancellation()
    {
        using var temp = TempDirectory.Create();
        var missing = BatchRenamePlanBuilder.Build([temp.GetPath("missing.txt")], new()).Rows[0];
        Assert.Equal(BatchRenameStatus.Blocked, missing.Status);
        var files = new[] { Create(temp, "a.txt"), Create(temp, "b.txt") };
        var rows = BatchRenamePlanBuilder.Build(files, new() { Start = int.MaxValue, Pattern = "{Index}" }).Rows;
        Assert.Equal(BatchRenameStatus.Ready, rows[0].Status);
        Assert.Equal(BatchRenameStatus.Blocked, rows[1].Status);
        Assert.Throws<OperationCanceledException>(() => BatchRenamePlanBuilder.Build(files, new(), cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void Apply_DoesNotOverwriteDestinationCreatedAfterPreview()
    {
        using var temp = TempDirectory.Create();
        var file = Create(temp, "a.txt");
        var row = BatchRenamePlanBuilder.Build([file], new() { Pattern = "new" }).Rows[0];
        File.WriteAllText(row.TargetPath, "keep");
        var result = BatchRenameOperations.Apply(row, file);
        Assert.True(result.HasErrors);
        Assert.True(File.Exists(file));
        Assert.Equal("keep", File.ReadAllText(row.TargetPath));
    }

    [Fact]
    public void Apply_RejectsChangedSource()
    {
        using var temp = TempDirectory.Create();
        var file = Create(temp, "a.txt");
        var row = BatchRenamePlanBuilder.Build([file], new() { Pattern = "new" }).Rows[0];
        File.AppendAllText(file, "changed");
        Assert.True(BatchRenameOperations.Apply(row, file).HasErrors);
        Assert.False(File.Exists(row.TargetPath));
    }

    [Fact]
    public void Apply_RejectsReplacementEvenWithSameSizeAndDates()
    {
        using var temp = TempDirectory.Create();
        var file = Create(temp, "a.txt");
        var row = BatchRenamePlanBuilder.Build([file], new() { Pattern = "new" }).Rows[0];
        var created = File.GetCreationTimeUtc(file); var modified = File.GetLastWriteTimeUtc(file);
        File.Move(file, temp.GetPath("saved.txt"));
        File.WriteAllText(file, "a.txt");
        File.SetCreationTimeUtc(file, created); File.SetLastWriteTimeUtc(file, modified);
        Assert.True(BatchRenameOperations.Apply(row, file).HasErrors);
        Assert.False(File.Exists(row.TargetPath));
    }

    [Fact]
    public void Executor_UsesFrozenSequenceRegardlessOfExecutionOrder()
    {
        using var temp = TempDirectory.Create();
        var files = new[] { Create(temp, "book10.txt"), Create(temp, "book2.txt") };
        var plan = BatchRenamePlanBuilder.Build(files, new() { Pattern = "{Index}-{Stem}" });
        var targets = plan.Rows.Reverse().Select(row => Target(plan, row)).ToArray();
        var result = new WorkPlanExecutor(new()).RunDetailed(targets, CancellationToken.None, null);
        Assert.False(result.Result.HasErrors);
        Assert.Equal(2, result.Result.AppliedCount);
        Assert.Equal("book2.txt", File.ReadAllText(temp.GetPath("001-book2.txt")));
        Assert.Equal("book10.txt", File.ReadAllText(temp.GetPath("002-book10.txt")));
        Assert.All(result.Targets, target => Assert.Single(target.CompletedSteps));
    }

    [Fact]
    public void Executor_CaseOnlyRenameUpdatesFinalPathAndKeepsContent()
    {
        using var temp = TempDirectory.Create();
        var file = Create(temp, "Book.txt");
        var plan = BatchRenamePlanBuilder.Build([file], new() { Pattern = "book" });
        var result = new WorkPlanExecutor(new()).RunDetailed([Target(plan, plan.Rows[0])], CancellationToken.None, null);
        Assert.False(result.Result.HasErrors);
        Assert.Equal(temp.GetPath("book.txt"), Assert.Single(result.Targets).FinalPath);
        Assert.Equal("book.txt", Path.GetFileName(Assert.Single(Directory.GetFiles(temp.Root))));
        Assert.Equal("Book.txt", File.ReadAllText(file));
    }

    [Fact]
    public void Executor_FailedStepRemainsPendingAndDoesNotApplyFollowingStep()
    {
        using var temp = TempDirectory.Create();
        var file = Create(temp, "a.txt");
        var plan = BatchRenamePlanBuilder.Build([file], new() { Pattern = "new" });
        var target = Target(plan, plan.Rows[0]);
        target.Steps.Add(new() { Kind = WorkPlanStepKind.FileNameCorrection, ManualRenameFileName = "later.txt" });
        File.AppendAllText(file, "changed");
        var result = new WorkPlanExecutor(new()).RunDetailed([target], CancellationToken.None, null);
        Assert.True(result.Result.HasErrors);
        Assert.Empty(Assert.Single(result.Targets).CompletedSteps);
        Assert.False(File.Exists(temp.GetPath("later.txt")));
    }

    private static WorkTargetPlan Target(BatchRenamePlan plan, BatchRenamePreview row)
    {
        var target = new WorkTargetPlan(row.OriginalPath);
        target.Steps.Add(new() { Kind = WorkPlanStepKind.BatchRename, BatchRenamePlan = plan, BatchRenameItem = row });
        return target;
    }

    private static string Create(TempDirectory temp, string name)
    {
        var path = temp.GetPath(name);
        File.WriteAllText(path, Path.GetFileName(name));
        return path;
    }
}
