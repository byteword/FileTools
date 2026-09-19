namespace FileTools.Tests;

public sealed class EmptyFolderCleanupTests
{
    [Fact]
    public void Scan_DirectModeAlsoScansExplicitDeepRoots()
    {
        using var temp = TempDirectory.Create();
        var deep = Directory.CreateDirectory(temp.GetPath("a/b/selected")).FullName;
        var leaf = Directory.CreateDirectory(Path.Combine(deep, "empty")).FullName;
        var scan = EmptyFolderCleanupOperations.Scan([temp.Root, deep], new(IncludeSubfolders: false));
        Assert.Equal(leaf, Assert.Single(scan.Candidates).Path);
        var plan = Assert.Single(EmptyFolderCleanupOperations.CreatePlans(scan, new(IncludeSubfolders: false), [leaf]));
        Assert.Equal(temp.Root, plan.RootPath);
    }

    [Fact]
    public void Scan_DirectModeOverlappingRootsHaveOneOrderedPlan()
    {
        using var temp = TempDirectory.Create();
        var child = Directory.CreateDirectory(temp.GetPath("child")).FullName;
        var leaf = Directory.CreateDirectory(Path.Combine(child, "leaf")).FullName;
        var options = new EmptyFolderCleanupOptions(IncludeSubfolders: false, IncludeSelectedRoots: true);
        var scan = EmptyFolderCleanupOperations.Scan([temp.Root, child], options);
        Assert.Equal([leaf, child, temp.Root], scan.Candidates.Select(candidate => candidate.Path));
        Assert.Single(EmptyFolderCleanupOperations.CreatePlans(scan, options, scan.Candidates.Select(candidate => candidate.Path)));
    }

    [Fact]
    public void Scan_FindsEmptyBranchesButPreservesRootAndHiddenZeroByteFiles()
    {
        using var temp = TempDirectory.Create();
        var leaf = Directory.CreateDirectory(temp.GetPath("empty/leaf")).FullName;
        Directory.CreateDirectory(temp.GetPath("occupied"));
        var hidden = temp.GetPath("occupied/hidden.txt");
        File.WriteAllText(hidden, "");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        var scan = EmptyFolderCleanupOperations.Scan([temp.Root], new());
        Assert.Empty(scan.Errors);
        Assert.Equal([leaf, temp.GetPath("empty")], scan.Candidates.Select(c => c.Path));
        Assert.False(scan.Candidates[0].AfterChildren);
        Assert.True(scan.Candidates[1].AfterChildren);
    }

    [Fact]
    public void Scan_OverlappingRootsAreNotDuplicatedAndEachSelectedRootIsProtected()
    {
        using var temp = TempDirectory.Create();
        var selected = Directory.CreateDirectory(temp.GetPath("parent/selected")).FullName;
        var leaf = Directory.CreateDirectory(Path.Combine(selected, "leaf")).FullName;
        var scan = EmptyFolderCleanupOperations.Scan([selected, temp.Root, selected], new());
        Assert.Equal(leaf, Assert.Single(scan.Candidates).Path);
        var plan = Assert.Single(EmptyFolderCleanupOperations.CreatePlans(scan, new(), [leaf]));
        Assert.Equal(2, plan.ScanRoots.Count);
    }

    [Fact]
    public void Scan_IncludeRootsAllowsAnEntireEmptyTree()
    {
        using var temp = TempDirectory.Create();
        Directory.CreateDirectory(temp.GetPath("a/b"));
        var scan = EmptyFolderCleanupOperations.Scan([temp.Root], new(IncludeSelectedRoots: true));
        Assert.Equal(3, scan.Candidates.Count);
        Assert.Equal(temp.Root, scan.Candidates[^1].Path);
    }

    [Fact]
    public void Scan_DirectChildrenModeDoesNotDescendFurther()
    {
        using var temp = TempDirectory.Create();
        var direct = Directory.CreateDirectory(temp.GetPath("direct")).FullName;
        Directory.CreateDirectory(temp.GetPath("nested/leaf"));
        var scan = EmptyFolderCleanupOperations.Scan([temp.Root], new(IncludeSubfolders: false));
        Assert.Equal(direct, Assert.Single(scan.Candidates).Path);
    }

    [Fact]
    public void Scan_MissingRootIsAnErrorNotAnEmptyCandidate()
    {
        using var temp = TempDirectory.Create();
        var scan = EmptyFolderCleanupOperations.Scan([temp.GetPath("missing")], new());
        Assert.Empty(scan.Candidates);
        Assert.Single(scan.Errors);
    }

    [Fact]
    public void Scan_CancellationIsObserved()
    {
        using var temp = TempDirectory.Create();
        Assert.Throws<OperationCanceledException>(() => EmptyFolderCleanupOperations.Scan([temp.Root], new(), new(true)));
    }

    [Fact]
    public void CreatePlans_ExcludingAChildKeepsItsAncestors()
    {
        using var temp = TempDirectory.Create();
        var leaf = Directory.CreateDirectory(temp.GetPath("parent/leaf")).FullName;
        var sibling = Directory.CreateDirectory(temp.GetPath("sibling")).FullName;
        var scan = EmptyFolderCleanupOperations.Scan([temp.Root], new());
        var plans = EmptyFolderCleanupOperations.CreatePlans(scan, new(), scan.Candidates.Where(c => c.Path != leaf).Select(c => c.Path));
        Assert.Equal([sibling], Assert.Single(plans).CandidatePaths);
    }

    [Fact]
    public void Apply_RechecksContentsAndKeepsOnlyPendingCandidatesForRetry()
    {
        using var temp = TempDirectory.Create();
        var leaf = Directory.CreateDirectory(temp.GetPath("parent/leaf")).FullName;
        var sibling = Directory.CreateDirectory(temp.GetPath("sibling")).FullName;
        var scan = EmptyFolderCleanupOperations.Scan([temp.Root], new());
        var plan = Assert.Single(EmptyFolderCleanupOperations.CreatePlans(scan, new(), scan.Candidates.Select(c => c.Path)));
        File.WriteAllText(Path.Combine(leaf, "arrived.txt"), "keep");
        var recycled = new List<string>();
        void Recycle(string path) { Assert.StartsWith(temp.Root, path); recycled.Add(path); Directory.Delete(path, false); }
        var first = EmptyFolderCleanupOperations.Apply(plan, recycle: Recycle);
        Assert.Equal(1, first.AppliedCount);
        Assert.Equal(2, first.SkippedCount);
        Assert.Equal([sibling], recycled);
        File.Delete(Path.Combine(leaf, "arrived.txt"));
        var second = EmptyFolderCleanupOperations.Apply(plan, recycle: Recycle);
        Assert.Equal(2, second.AppliedCount);
        Assert.Empty(plan.CandidatePaths);
        Assert.True(Directory.Exists(temp.Root));
    }

    [Fact]
    public void Apply_RefusesOutsideScopeAndSelectedRootWithoutCallingRecycler()
    {
        using var temp = TempDirectory.Create();
        var root = Directory.CreateDirectory(temp.GetPath("inside")).FullName;
        var outside = Directory.CreateDirectory(temp.GetPath("outside")).FullName;
        var plan = new EmptyFolderCleanupPlan { RootPath = root, CandidatePaths = [outside, root] };
        var result = EmptyFolderCleanupOperations.Apply(plan, recycle: _ => throw new InvalidOperationException("Must not be called"));
        Assert.Equal(2, result.Errors.Count);
        Assert.True(Directory.Exists(outside));
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public void Apply_FailureDoesNotStopSiblingAndNeverRetriesPermanently()
    {
        using var temp = TempDirectory.Create();
        var fail = Directory.CreateDirectory(temp.GetPath("a")).FullName;
        var success = Directory.CreateDirectory(temp.GetPath("b")).FullName;
        var plan = new EmptyFolderCleanupPlan { RootPath = temp.Root, CandidatePaths = [fail, success] };
        var result = EmptyFolderCleanupOperations.Apply(plan, recycle: path =>
        {
            if (path == fail) throw new IOException("Recycle Bin unavailable");
            Assert.StartsWith(temp.Root, path);
            Directory.Delete(path, false);
        });
        Assert.Single(result.Errors);
        Assert.Equal(1, result.AppliedCount);
        Assert.Equal([fail], plan.CandidatePaths);
        Assert.True(Directory.Exists(fail));
    }

    [Fact]
    public void Apply_CancelRetainsUnprocessedCandidates()
    {
        using var temp = TempDirectory.Create();
        var a = Directory.CreateDirectory(temp.GetPath("a")).FullName;
        var b = Directory.CreateDirectory(temp.GetPath("b")).FullName;
        var plan = new EmptyFolderCleanupPlan { RootPath = temp.Root, CandidatePaths = [a, b] };
        using var cancellation = new CancellationTokenSource();
        var result = EmptyFolderCleanupOperations.Apply(plan, cancellation.Token, path =>
        {
            Assert.StartsWith(temp.Root, path);
            Directory.Delete(path, false);
            cancellation.Cancel();
        });
        Assert.Equal(1, result.AppliedCount);
        Assert.Single(plan.CandidatePaths);
    }

    [Fact]
    public void Apply_MissingCandidatesAreResolvedWithoutRecyclingAnything()
    {
        using var temp = TempDirectory.Create();
        var plan = new EmptyFolderCleanupPlan { RootPath = temp.Root, CandidatePaths = [temp.GetPath("gone")] };
        var result = EmptyFolderCleanupOperations.Apply(plan, recycle: _ => throw new InvalidOperationException());
        Assert.Empty(result.Errors);
        Assert.Empty(plan.CandidatePaths);
    }

    [Fact]
    public void Preview_DeletingRootBlocksFollowingSteps_AndCloneDoesNotShareCandidates()
    {
        using var temp = TempDirectory.Create();
        var step = new WorkPlanStep
        {
            Kind = WorkPlanStepKind.EmptyFolderCleanup,
            EmptyFolderCleanupPlan = new() { RootPath = temp.Root, Options = new(IncludeSelectedRoots: true), CandidatePaths = [temp.Root] }
        };
        var clone = step.Clone();
        clone.EmptyFolderCleanupPlan!.CandidatePaths.Clear();
        Assert.Single(step.EmptyFolderCleanupPlan.CandidatePaths);
        var target = new WorkTargetPlan(temp.Root);
        target.Steps.Add(step);
        target.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.FolderUnwrap });
        var previews = new WorkPlanPreviewBuilder(new FileToolsSettings()).Build(target);
        Assert.False(previews[0].HasWarning);
        Assert.True(previews[1].HasWarning);
    }

    [Fact]
    public void RootBoundary_DoesNotConfuseSiblingPrefixesAndSupportsDriveRoot()
    {
        Assert.False(FileOperationPathGuard.IsWithin(@"C:\data-other\x", @"C:\data"));
        Assert.True(FileOperationPathGuard.IsWithin(@"C:\data", @"C:\"));
    }
}
