using System.Reflection;
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

    [Fact]
    public void Build_DuplicateDeleteDisplaysAsGroupedInputs()
    {
        using var temp = TempDirectory.Create();
        var firstPath = temp.GetPath("First.txt");
        var secondPath = temp.GetPath("Second.txt");
        File.WriteAllText(firstPath, "first");
        File.WriteAllText(secondPath, "second");
        var target = new WorkTargetPlan(firstPath);
        target.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.DuplicateDelete,
            DuplicateDeleteGroupPaths = [firstPath, secondPath]
        });

        var rows = new WorkPlanDisplayBuilder(new FileToolsSettings()).Build([target]);

        Assert.Equal(3, rows.Count);
        var operationGroup = Assert.Single(rows.Where(static row => row.Kind == WorkPlanDisplayRowKind.OperationGroup));
        Assert.Equal(WorkPlanStepKind.DuplicateDelete, operationGroup.Step?.Kind);
        Assert.Equal(2, rows.Count(static row => row.Kind == WorkPlanDisplayRowKind.Input));
    }

    [Fact]
    public void Build_DuplicateDeleteSelectedFilterShowsWholeGroup()
    {
        using var temp = TempDirectory.Create();
        var firstPath = temp.GetPath("First.txt");
        var secondPath = temp.GetPath("Second.txt");
        File.WriteAllText(firstPath, "first");
        File.WriteAllText(secondPath, "second");
        var target = new WorkTargetPlan(firstPath);
        target.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.DuplicateDelete,
            DuplicateDeleteGroupPaths = [firstPath, secondPath]
        });

        var rows = new WorkPlanDisplayBuilder(new FileToolsSettings()).Build(
            [target],
            WorkPlanDisplayFilter.SelectedTargets,
            [target]);

        Assert.Equal(3, rows.Count);
        Assert.True(rows.Any(static row => row.MatchesFilter));
        Assert.Single(rows.Where(static row => row.Kind == WorkPlanDisplayRowKind.OperationGroup));
    }

    [Fact]
    public void CreateInputGroupLookup_GroupsByOrderAndOperationForInputRowsOnly()
    {
        var rows = new List<WorkPlanDisplayRow>
        {
            new(
                1,
                WorkPlanDisplayRowKind.OperationGroup,
                "op:1",
                null,
                null,
                null,
                "ZIP merge",
                "A.zip",
                "",
                false,
                true),
            new(
                1,
                WorkPlanDisplayRowKind.Input,
                "op:1",
                null,
                null,
                null,
                "-",
                "A01.zip",
                "",
                false,
                true),
            new(
                1,
                WorkPlanDisplayRowKind.Input,
                "op:1",
                null,
                null,
                null,
                "-",
                "A02.zip",
                "",
                false,
                true),
            new(
                2,
                WorkPlanDisplayRowKind.OperationGroup,
                "op:2",
                null,
                null,
                null,
                "Duplicate delete",
                "3 files",
                "",
                false,
                true),
            new(
                2,
                WorkPlanDisplayRowKind.Input,
                "op:2",
                null,
                null,
                null,
                "-",
                "X01.txt",
                "",
                false,
                true),
            new(
                2,
                WorkPlanDisplayRowKind.Input,
                "op:2",
                null,
                null,
                null,
                "-",
                "X02.txt",
                "",
                false,
                true),
            new(
                2,
                WorkPlanDisplayRowKind.Input,
                "op:2",
                null,
                null,
                null,
                "-",
                "X03.txt",
                "",
                false,
                true),
        };

        var actual = MainForm.CreateInputGroupLookup(rows);
        var expected = new Dictionary<int, (int InputIndex, int InputCount)>
        {
            [1] = (0, 2),
            [2] = (1, 2),
            [4] = (0, 3),
            [5] = (1, 3),
            [6] = (2, 3)
        };

        Assert.Equal(expected.Count, actual.Count);
        foreach (var pair in expected)
        {
            Assert.True(actual.TryGetValue(pair.Key, out var actualInfo));
            Assert.Equal(pair.Value, actualInfo);
        }
    }

    [Fact]
    public void GetInputGroupPrefix_ReturnsExpectedCharacters()
    {
        Assert.Equal("  ", MainForm.GetInputGroupPrefix(0, 1));
        Assert.Equal("  ", MainForm.GetInputGroupPrefix(3, 1));

        Assert.Equal("├ ", MainForm.GetInputGroupPrefix(0, 3));
        Assert.Equal("├ ", MainForm.GetInputGroupPrefix(1, 3));
        Assert.Equal("└ ", MainForm.GetInputGroupPrefix(2, 3));
    }

    [Fact]
    public void GetInputGroupPrefix_MatchCreatePlanActionCellTextForInputRows()
    {
        var row = new WorkPlanDisplayRow(
            1,
            WorkPlanDisplayRowKind.Input,
            "op",
            null,
            null,
            null,
            "merge",
            "A.zip",
            "",
            false,
            true);

        var expectedFirst = MainForm.GetInputGroupPrefix(0, 2) + "merge";
        var expectedSecond = MainForm.GetInputGroupPrefix(1, 2) + "merge";

        var actualFirst = InvokeCreatePlanActionCellText(row, 0, 2);
        var actualSecond = InvokeCreatePlanActionCellText(row, 1, 2);

        Assert.Equal(expectedFirst, actualFirst);
        Assert.Equal(expectedSecond, actualSecond);
    }

    private static string InvokeCreatePlanActionCellText(WorkPlanDisplayRow row, int groupIndex, int groupSize)
    {
        var method = typeof(MainForm).GetMethod(
            "CreatePlanActionCellText",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            [typeof(WorkPlanDisplayRow), typeof(int), typeof(int)],
            null);
        Assert.NotNull(method);
        var value = method.Invoke(null, [row, groupIndex, groupSize]);

        return Assert.IsType<string>(value);
    }

    [Fact]
    public void GetInputGroupPrefix_UsesDefaultSpaceForNonGroupedInputRows()
    {
        var row = new WorkPlanDisplayRow(
            1,
            WorkPlanDisplayRowKind.Input,
            "op",
            null,
            null,
            null,
            "merge",
            "A.zip",
            "",
            false,
            true);

        Assert.Equal("  merge", InvokeCreatePlanActionCellText(row, 0, 1));
        Assert.Equal("  merge", InvokeCreatePlanActionCellText(row, 5, 1));
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
