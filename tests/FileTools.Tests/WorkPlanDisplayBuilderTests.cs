using FileTools;

namespace FileTools.Tests;

public sealed class WorkPlanDisplayBuilderTests
{
    [Fact]
    public void Build_AllPlanRowsKeepTargetThenStepOrder()
    {
        using var temp = TempDirectory.Create();
        var firstPath = temp.GetPath("First.txt");
        var secondPath = temp.GetPath("Second.txt");
        File.WriteAllText(firstPath, "first");
        File.WriteAllText(secondPath, "second");
        var firstTarget = new WorkTargetPlan(firstPath);
        firstTarget.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FileNameCorrection,
            ManualRenameFileName = "First-renamed.txt"
        });
        firstTarget.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FolderWrap,
            FolderOperation = FolderStructureOperation.WrapFiles
        });
        var secondTarget = new WorkTargetPlan(secondPath);
        secondTarget.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FileNameCorrection,
            ManualRenameFileName = "Second-renamed.txt"
        });

        var rows = new WorkPlanDisplayBuilder(new FileToolsSettings()).Build([firstTarget, secondTarget]);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal(1, row.Order);
                Assert.Equal(firstTarget, row.Target);
                Assert.Equal(WorkPlanStepKind.FileNameCorrection, row.Step?.Kind);
            },
            row =>
            {
                Assert.Equal(2, row.Order);
                Assert.Equal(firstTarget, row.Target);
                Assert.Equal(WorkPlanStepKind.FolderWrap, row.Step?.Kind);
            },
            row =>
            {
                Assert.Equal(3, row.Order);
                Assert.Equal(secondTarget, row.Target);
                Assert.Equal(WorkPlanStepKind.FileNameCorrection, row.Step?.Kind);
            });
    }

    [Fact]
    public void Build_SelectedTargetsFilterKeepsGlobalOrderForSimpleRows()
    {
        using var temp = TempDirectory.Create();
        var firstPath = temp.GetPath("First.txt");
        var secondPath = temp.GetPath("Second.txt");
        File.WriteAllText(firstPath, "first");
        File.WriteAllText(secondPath, "second");
        var firstTarget = CreateRenameTarget(firstPath, "First-renamed.txt");
        var secondTarget = CreateRenameTarget(secondPath, "Second-renamed.txt");

        var rows = new WorkPlanDisplayBuilder(new FileToolsSettings()).Build(
            [firstTarget, secondTarget],
            WorkPlanDisplayFilter.SelectedTargets,
            [secondTarget]);

        var row = Assert.Single(rows);
        Assert.Equal(2, row.Order);
        Assert.Equal(secondTarget, row.Target);
        Assert.True(row.MatchesFilter);
    }

    [Fact]
    public void Build_DeduplicatesSharedArchiveMergeByPlanId()
    {
        using var temp = TempDirectory.Create();
        var sourceA = temp.GetPath("A 01.zip");
        var sourceB = temp.GetPath("A 02.zip");
        ZipTestData.CreateStoredZip(sourceA, new TestZipEntry("a.txt", "a"));
        ZipTestData.CreateStoredZip(sourceB, new TestZipEntry("b.txt", "b"));
        var options = CreateArchiveMergeOptions(sourceA, sourceB, temp.GetPath("A.zip"));
        var firstTarget = CreateArchiveMergeTarget(sourceA, options);
        var secondTarget = CreateArchiveMergeTarget(sourceB, options.Clone());

        var rows = new WorkPlanDisplayBuilder(new FileToolsSettings()).Build([firstTarget, secondTarget]);

        var group = Assert.Single(rows.Where(static row => row.Kind == WorkPlanDisplayRowKind.OperationGroup));
        Assert.Equal(1, group.Order);
        Assert.Equal(WorkPlanStepKind.ArchiveMerge, group.Step?.Kind);
        Assert.Equal(options.OutputPath, group.OutputText);
        Assert.Equal(2, rows.Count(static row => row.Kind == WorkPlanDisplayRowKind.Input));
    }

    [Fact]
    public void Build_SelectedTargetsFilterShowsWholeSharedArchiveMerge()
    {
        using var temp = TempDirectory.Create();
        var sourceA = temp.GetPath("A 01.zip");
        var sourceB = temp.GetPath("A 02.zip");
        ZipTestData.CreateStoredZip(sourceA, new TestZipEntry("a.txt", "a"));
        ZipTestData.CreateStoredZip(sourceB, new TestZipEntry("b.txt", "b"));
        var options = CreateArchiveMergeOptions(sourceA, sourceB, temp.GetPath("A.zip"));
        var firstTarget = CreateArchiveMergeTarget(sourceA, options);
        var secondTarget = CreateArchiveMergeTarget(sourceB, options.Clone());

        var rows = new WorkPlanDisplayBuilder(new FileToolsSettings()).Build(
            [firstTarget, secondTarget],
            WorkPlanDisplayFilter.SelectedTargets,
            [secondTarget]);

        Assert.Equal(3, rows.Count);
        Assert.Single(rows.Where(static row => row.Kind == WorkPlanDisplayRowKind.OperationGroup));
        var inputRows = rows.Where(static row => row.Kind == WorkPlanDisplayRowKind.Input).ToArray();
        Assert.Equal(2, inputRows.Length);
        Assert.False(inputRows.Single(row => row.InputText == sourceA).MatchesFilter);
        Assert.True(inputRows.Single(row => row.InputText == sourceB).MatchesFilter);
    }

    [Fact]
    public void Build_WarningsFilterIncludesArchiveMergePreviewWarnings()
    {
        using var temp = TempDirectory.Create();
        var sourceA = temp.GetPath("A 01.zip");
        var missingSource = temp.GetPath("A 02.zip");
        ZipTestData.CreateStoredZip(sourceA, new TestZipEntry("a.txt", "a"));
        var options = CreateArchiveMergeOptions(sourceA, missingSource, temp.GetPath("A.zip"));
        var target = CreateArchiveMergeTarget(sourceA, options);

        var rows = new WorkPlanDisplayBuilder(new FileToolsSettings()).Build(
            [target],
            WorkPlanDisplayFilter.Warnings);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, static row => Assert.True(row.HasWarning));
        var group = Assert.Single(rows.Where(static row => row.Kind == WorkPlanDisplayRowKind.OperationGroup));
        Assert.Contains(Path.GetFileName(missingSource), group.OutputText + group.Preview?.PreviewText, StringComparison.Ordinal);
    }

    private static WorkTargetPlan CreateRenameTarget(string path, string fileName)
    {
        var target = new WorkTargetPlan(path);
        target.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FileNameCorrection,
            ManualRenameFileName = fileName
        });
        return target;
    }

    private static WorkTargetPlan CreateArchiveMergeTarget(string path, ArchiveMergeOptions options)
    {
        var target = new WorkTargetPlan(path);
        target.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.ArchiveMerge,
            ArchiveMergeOptions = options
        });
        return target;
    }

    private static ArchiveMergeOptions CreateArchiveMergeOptions(string sourceA, string sourceB, string output)
    {
        return new ArchiveMergeOptions
        {
            PlanId = "shared-plan",
            SourcePaths = [sourceA, sourceB],
            OutputPath = output
        };
    }
}
