using FileTools;

namespace FileTools.Tests;

public sealed class WorkPlanPreviewBuilderTests
{
    [Fact]
    public void Build_FolderWrapStepPreviewsTargetFolder()
    {
        using var temp = TempDirectory.Create();
        var source = temp.GetPath("Book 01.txt");
        File.WriteAllText(source, "book");
        var target = new WorkTargetPlan(source);
        target.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FolderWrap,
            FolderOperation = FolderStructureOperation.WrapFiles
        });

        var previews = new WorkPlanPreviewBuilder(new FileToolsSettings()).Build(target);

        var preview = Assert.Single(previews);
        Assert.False(preview.HasWarning);
        Assert.Contains("Book 01", preview.PreviewText, StringComparison.Ordinal);
        Assert.Contains(temp.GetPath("Book 01"), preview.ToolTipText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_FolderUnwrapStepPreviewsPrefixedFileName()
    {
        using var temp = TempDirectory.Create();
        var folder = temp.GetPath("Volume");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Inside.txt"), "inside");
        var target = new WorkTargetPlan(folder);
        target.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FolderUnwrap,
            FolderOperation = FolderStructureOperation.UnwrapSingleFileFolder,
            FolderUnwrapNameMismatchMode = FolderUnwrapNameMismatchMode.PrefixFolderName
        });

        var previews = new WorkPlanPreviewBuilder(new FileToolsSettings()).Build(target);

        var preview = Assert.Single(previews);
        Assert.False(preview.HasWarning);
        Assert.Contains("Volume-Inside.txt", preview.PreviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkPlanStep_CloneDeepCopiesArchiveMergeOptions()
    {
        var step = new WorkPlanStep
        {
            Kind = WorkPlanStepKind.ArchiveMerge,
            ArchiveMergeOptions = new ArchiveMergeOptions
            {
                SourcePaths = ["a.zip", "b.zip"],
                OutputPath = "merged.zip"
            }
        };

        var clone = step.Clone();
        clone.ArchiveMergeOptions!.SourcePaths.Add("c.zip");

        Assert.Equal(2, step.ArchiveMergeOptions!.SourcePaths.Count);
        Assert.Equal(3, clone.ArchiveMergeOptions.SourcePaths.Count);
    }
}
