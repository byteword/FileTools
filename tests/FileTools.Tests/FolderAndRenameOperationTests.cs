using FileTools;

namespace FileTools.Tests;

public sealed class FolderAndRenameOperationTests
{
    [Fact]
    public void RenameOperations_ApplyRenamesReadyFile()
    {
        using var temp = TempDirectory.Create();
        var source = temp.GetPath("source.txt");
        var target = temp.GetPath("target.txt");
        File.WriteAllText(source, "content");
        var preview = CreateRenamePreview(source, target, RenamePreviewStatus.Ready);

        var result = RenameOperations.Apply([preview]);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.AppliedCount);
        Assert.False(File.Exists(source));
        Assert.Equal("content", File.ReadAllText(target));
    }

    [Fact]
    public void RenameOperations_ApplySkipsWhenTargetAlreadyExists()
    {
        using var temp = TempDirectory.Create();
        var source = temp.GetPath("source.txt");
        var target = temp.GetPath("target.txt");
        File.WriteAllText(source, "source");
        File.WriteAllText(target, "existing");
        var preview = CreateRenamePreview(source, target, RenamePreviewStatus.Ready);

        var result = RenameOperations.Apply([preview]);

        Assert.Equal(1, result.SkippedCount);
        Assert.True(File.Exists(source));
        Assert.Equal("existing", File.ReadAllText(target));
    }

    [Fact]
    public void FolderMergeOperations_MergeIntoFolderMovesFilesToCommonStemFolder()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("Series 01.txt");
        var second = temp.GetPath("Series 02.txt");
        File.WriteAllText(first, "one");
        File.WriteAllText(second, "two");

        var result = FolderMergeOperations.MergeIntoFolder([first, second], new FileToolsSettings());

        var targetFolder = temp.GetPath("Series");
        Assert.Equal(targetFolder, result.TargetFolderPath);
        Assert.Equal(2, result.OperationResult.AppliedCount);
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.Equal("one", File.ReadAllText(Path.Combine(targetFolder, "Series 01.txt")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(targetFolder, "Series 02.txt")));
    }

    [Fact]
    public void FolderMergeOperations_CreateMergePlanPreviewStripsNumericSuffixes()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("Series 01.txt");
        var second = temp.GetPath("Series 02.txt");
        File.WriteAllText(first, "one");
        File.WriteAllText(second, "two");

        var preview = FolderMergeOperations.CreateMergePlanPreview([first, second], new FileToolsSettings());

        Assert.True(preview.IsReady);
        Assert.Null(preview.FailureReason);
        Assert.Equal(2, preview.SourcePaths.Count);
        Assert.Equal(temp.GetPath("Series"), preview.TargetFolderPath);
    }

    [Fact]
    public void FolderMergeOperations_MergeIntoFolder_ContentsOnlyFlattensSelectedFolders()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("Episode 01");
        var second = temp.GetPath("Episode 02");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        Directory.CreateDirectory(Path.Combine(first, "Chapter01"));
        Directory.CreateDirectory(Path.Combine(second, "Chapter02"));
        File.WriteAllText(Path.Combine(first, "Chapter01", "episode1.txt"), "first");
        File.WriteAllText(Path.Combine(second, "Chapter02", "episode2.txt"), "second");

        var result = FolderMergeOperations.MergeIntoFolder(
            [first, second],
            new FileToolsSettings(),
            new FolderMergeOptions(null, FolderMergeMode.MergeFolderContentsOnly));

        var targetFolder = result.TargetFolderPath;
        Assert.NotNull(targetFolder);
        Assert.Equal(Path.Combine(temp.Root, "Episode"), targetFolder);
        Assert.True(Directory.Exists(Path.Combine(targetFolder, "Chapter01")));
        Assert.True(Directory.Exists(Path.Combine(targetFolder, "Chapter02")));
        Assert.False(Directory.Exists(Path.Combine(targetFolder, "Episode 01")));
        Assert.False(Directory.Exists(Path.Combine(targetFolder, "Episode 02")));
        Assert.True(File.Exists(Path.Combine(targetFolder, "Chapter01", "episode1.txt")));
        Assert.True(File.Exists(Path.Combine(targetFolder, "Chapter02", "episode2.txt")));
    }

    [Fact]
    public void FolderMergeOperations_MergeIntoFolder_WithCustomTargetName()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("Episode01.txt");
        var second = temp.GetPath("Episode02.txt");
        File.WriteAllText(first, "one");
        File.WriteAllText(second, "two");

        var result = FolderMergeOperations.MergeIntoFolder(
            [first, second],
            new FileToolsSettings(),
            new FolderMergeOptions("CUSTOM_TARGET", FolderMergeMode.MergeFolderUnits));

        Assert.Equal(Path.Combine(temp.Root, "CUSTOM_TARGET"), result.TargetFolderPath);
        Assert.True(File.Exists(Path.Combine(result.TargetFolderPath ?? temp.Root, "Episode01.txt")));
        Assert.True(File.Exists(Path.Combine(result.TargetFolderPath ?? temp.Root, "Episode02.txt")));
    }

    [Fact]
    public void FolderMergeOperations_MergeIntoFolderKeepsSourceFolderStructure()
    {
        using var temp = TempDirectory.Create();
        var sourceFolder = temp.GetPath("Folder 01");
        var sourceFile = temp.GetPath("Folder 02.txt");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(Path.Combine(sourceFolder, "Nested"));
        File.WriteAllText(Path.Combine(sourceFolder, "Nested", "inner.txt"), "nested");
        File.WriteAllText(sourceFile, "file");

        var result = FolderMergeOperations.MergeIntoFolder([sourceFolder, sourceFile], new FileToolsSettings());

        var targetFolder = result.TargetFolderPath;
        Assert.NotNull(targetFolder);
        Assert.True(Directory.Exists(targetFolder));
        Assert.True(Directory.Exists(Path.Combine(targetFolder, "Folder 01")));
        Assert.True(File.Exists(Path.Combine(targetFolder, "Folder 02.txt")));
        Assert.True(File.Exists(Path.Combine(targetFolder, "Folder 01", "Nested", "inner.txt")));
    }

    [Fact]
    public void FolderMergeOperations_PreviewUsesFirstParentAndFlagsCrossParentSelection()
    {
        using var temp = TempDirectory.Create();
        var firstParent = temp.GetPath("Left");
        var secondParent = temp.GetPath("Right");
        Directory.CreateDirectory(firstParent);
        Directory.CreateDirectory(secondParent);

        var first = Path.Combine(firstParent, "Series 01.txt");
        var second = Path.Combine(secondParent, "Series 02.txt");
        File.WriteAllText(first, "one");
        File.WriteAllText(second, "two");

        var preview = FolderMergeOperations.CreateMergePlanPreview([first, second], new FileToolsSettings());

        Assert.True(preview.IsReady);
        Assert.True(preview.HasMultipleParents);
        Assert.Equal(firstParent, preview.TargetParentPath);
        Assert.Equal(Path.Combine(firstParent, "Series"), preview.TargetFolderPath);
    }

    [Fact]
    public void FolderMergeOperations_PreviewSkipsWhenTargetFolderWouldBeInsideSelectedFolder()
    {
        using var temp = TempDirectory.Create();
        var sourceFolder = temp.GetPath("Parent");
        var sourceFile = Path.Combine(sourceFolder, "Anchor.txt");
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllText(sourceFile, "file");

        var result = FolderMergeOperations.MergeIntoFolder([sourceFile, sourceFolder], new FileToolsSettings());

        Assert.Equal(Path.Combine(sourceFolder, "Merged"), result.TargetFolderPath);
        Assert.True(result.OperationResult.AppliedCount > 0);
        Assert.True(result.OperationResult.SkippedCount > 0);
        Assert.True(Directory.Exists(sourceFolder));
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(Path.Combine(sourceFolder, "Merged", "Anchor.txt")));
    }

    [Fact]
    public void FolderMergeOperations_PreviewTargetFolderPathAutoNumbersExistingTarget()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("Series 01.txt");
        var second = temp.GetPath("Series 02.txt");
        Directory.CreateDirectory(temp.GetPath("Series"));
        File.WriteAllText(first, "one");
        File.WriteAllText(second, "two");

        var target = FolderMergeOperations.PreviewTargetFolderPath([first, second], new FileToolsSettings());

        Assert.Equal(temp.GetPath("Series (2)"), target);
    }

    private static RenamePreview CreateRenamePreview(string sourcePath, string targetPath, RenamePreviewStatus status)
    {
        return new RenamePreview
        {
            OriginalPath = sourcePath,
            OriginalFileName = Path.GetFileName(sourcePath),
            Parts = new FileNameParts
            {
                Title = Path.GetFileNameWithoutExtension(targetPath),
                Extension = Path.GetExtension(targetPath)
            },
            SuggestedFileName = Path.GetFileName(targetPath),
            SuggestedPath = targetPath,
            Status = status
        };
    }
}
