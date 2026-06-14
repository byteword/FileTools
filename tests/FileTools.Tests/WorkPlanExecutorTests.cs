using FileTools;

namespace FileTools.Tests;

public sealed class WorkPlanExecutorTests
{
    [Fact]
    public void RunDetailed_ReportsCompletedRenameStepAndFinalPath()
    {
        using var temp = TempDirectory.Create();
        var source = temp.GetPath("Book.txt");
        var targetPath = temp.GetPath("Book Renamed.txt");
        File.WriteAllText(source, "book");
        var step = new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FileNameCorrection,
            ManualRenameFileName = Path.GetFileName(targetPath)
        };
        var target = new WorkTargetPlan(source);
        target.Steps.Add(step);

        var execution = new WorkPlanExecutor(new FileToolsSettings())
            .RunDetailed([target], CancellationToken.None, progress: null);

        Assert.Equal(1, execution.Result.AppliedCount);
        Assert.True(File.Exists(targetPath));
        Assert.False(File.Exists(source));
        var targetResult = Assert.Single(execution.Targets);
        Assert.Same(target, targetResult.Target);
        Assert.Equal(targetPath, targetResult.FinalPath);
        Assert.Same(step, Assert.Single(targetResult.CompletedSteps));
        Assert.Single(target.Steps);
    }

    [Fact]
    public void RunDetailed_DoesNotCompleteSkippedStep()
    {
        using var temp = TempDirectory.Create();
        var source = temp.GetPath("Book.txt");
        File.WriteAllText(source, "book");
        var step = new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FolderUnwrap,
            FolderOperation = FolderStructureOperation.UnwrapSameNameSingleFile
        };
        var target = new WorkTargetPlan(source);
        target.Steps.Add(step);

        var execution = new WorkPlanExecutor(new FileToolsSettings())
            .RunDetailed([target], CancellationToken.None, progress: null);

        Assert.Equal(1, execution.Result.SkippedCount);
        var targetResult = Assert.Single(execution.Targets);
        Assert.Empty(targetResult.CompletedSteps);
        Assert.Equal(source, targetResult.FinalPath);
        Assert.Single(target.Steps);
    }
}
