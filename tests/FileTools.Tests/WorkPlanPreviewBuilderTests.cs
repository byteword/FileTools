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
    public void Build_FolderMoveInnerFilesUpPreviewsFolderWithSingleChildFolder()
    {
        using var temp = TempDirectory.Create();
        var folder = temp.GetPath("Outer");
        Directory.CreateDirectory(Path.Combine(folder, "Child"));
        var target = new WorkTargetPlan(folder);
        target.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FolderUnwrap,
            FolderOperation = FolderStructureOperation.MoveInnerFilesUp
        });

        var previews = new WorkPlanPreviewBuilder(new FileToolsSettings()).Build(target);

        var preview = Assert.Single(previews);
        Assert.False(preview.HasWarning);
        Assert.Contains(temp.Root, preview.ToolTipText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DuplicateDeleteStepShowsDeleteCandidate()
    {
        using var temp = TempDirectory.Create();
        var source = temp.GetPath("Copy.txt");
        File.WriteAllText(source, "copy");
        var target = new WorkTargetPlan(source);
        target.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.DuplicateDelete });

        var previews = new WorkPlanPreviewBuilder(new FileToolsSettings()).Build(target);

        var preview = Assert.Single(previews);
        Assert.False(preview.HasWarning);
        Assert.Equal(Localizer.Format("DuplicateDeletePreviewFormat", "Copy.txt"), preview.PreviewText);
        Assert.Contains(Localizer.Get("DuplicateDeleteRecycleBinOnly"), preview.ToolTipText, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateDeleteStepSelection_SyncsDeleteAndKeepTargets()
    {
        using var temp = TempDirectory.Create();
        var deletePath = temp.GetPath("Delete.txt");
        var keepPath = temp.GetPath("Keep.txt");
        File.WriteAllText(deletePath, "same");
        File.WriteAllText(keepPath, "same");

        var deleteTarget = new WorkTargetPlan(deletePath);
        deleteTarget.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.FolderWrap });
        var keepTarget = new WorkTargetPlan(keepPath);
        keepTarget.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.DuplicateDelete });
        keepTarget.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.DuplicateDelete });

        var changed = DuplicateDeleteStepSelection.Apply([deleteTarget, keepTarget], [deletePath]);

        Assert.Equal(2, changed);
        Assert.Single(deleteTarget.Steps.Where(static step => step.Kind == WorkPlanStepKind.DuplicateDelete));
        Assert.Contains(deleteTarget.Steps, static step => step.Kind == WorkPlanStepKind.FolderWrap);
        Assert.Empty(keepTarget.Steps.Where(static step => step.Kind == WorkPlanStepKind.DuplicateDelete));
    }

    [Fact]
    public void DuplicateDeleteStepSelection_ScopedApplyMovesDeleteStepToSelectedTarget()
    {
        using var temp = TempDirectory.Create();
        var oldDeletePath = temp.GetPath("OldDelete.txt");
        var newDeletePath = temp.GetPath("NewDelete.txt");
        var unrelatedPath = temp.GetPath("Unrelated.txt");
        File.WriteAllText(oldDeletePath, "same");
        File.WriteAllText(newDeletePath, "same");
        File.WriteAllText(unrelatedPath, "other");

        var scopePaths = new[] { oldDeletePath, newDeletePath };
        var oldDeleteTarget = new WorkTargetPlan(oldDeletePath);
        oldDeleteTarget.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.DuplicateDelete,
            DuplicateDeleteGroupPaths = scopePaths
        });
        var newDeleteTarget = new WorkTargetPlan(newDeletePath);
        var unrelatedTarget = new WorkTargetPlan(unrelatedPath);
        unrelatedTarget.Steps.Add(new WorkPlanStep
        {
            Kind = WorkPlanStepKind.DuplicateDelete,
            DuplicateDeleteGroupPaths = [unrelatedPath]
        });

        var changed = DuplicateDeleteStepSelection.Apply(
            [oldDeleteTarget, newDeleteTarget, unrelatedTarget],
            [newDeletePath],
            scopePaths);

        Assert.Equal(2, changed);
        Assert.Empty(oldDeleteTarget.Steps.Where(static step => step.Kind == WorkPlanStepKind.DuplicateDelete));
        var newStep = Assert.Single(newDeleteTarget.Steps.Where(static step => step.Kind == WorkPlanStepKind.DuplicateDelete));
        Assert.Equal(
            scopePaths.OrderBy(static path => path),
            newStep.DuplicateDeleteGroupPaths.OrderBy(static path => path));
        Assert.Single(unrelatedTarget.Steps.Where(static step => step.Kind == WorkPlanStepKind.DuplicateDelete));
    }

    [Fact]
    public void DuplicateDeleteStepSelection_CreateCandidatesIncludesFileState()
    {
        using var temp = TempDirectory.Create();
        var deletePath = temp.GetPath("Delete.txt");
        var keepPath = temp.GetPath("Keep.txt");
        var folderPath = temp.GetPath("Folder");
        File.WriteAllText(deletePath, "same");
        File.WriteAllText(keepPath, "same");
        Directory.CreateDirectory(folderPath);

        var deleteTarget = new WorkTargetPlan(deletePath);
        deleteTarget.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.DuplicateDelete });
        var keepTarget = new WorkTargetPlan(keepPath);
        var folderTarget = new WorkTargetPlan(folderPath);

        var candidates = DuplicateDeleteStepSelection.CreateCandidates([deleteTarget, keepTarget, folderTarget]);

        Assert.Equal(2, candidates.Count);
        Assert.True(candidates.Single(candidate => candidate.Path == deletePath).Delete);
        Assert.False(candidates.Single(candidate => candidate.Path == keepPath).Delete);
    }

    [Fact]
    public void WorkPlanStep_CloneCopiesDuplicateDeleteGroupPaths()
    {
        var step = new WorkPlanStep
        {
            Kind = WorkPlanStepKind.DuplicateDelete,
            DuplicateDeleteGroupPaths = ["a.txt", "b.txt"]
        };

        var clone = step.Clone();
        step.DuplicateDeleteGroupPaths = ["changed.txt"];

        Assert.Equal(["a.txt", "b.txt"], clone.DuplicateDeleteGroupPaths);
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
