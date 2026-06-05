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

        var targetFolder = temp.GetPath("Series 0");
        Assert.Equal(targetFolder, result.TargetFolderPath);
        Assert.Equal(2, result.OperationResult.AppliedCount);
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.Equal("one", File.ReadAllText(Path.Combine(targetFolder, "Series 01.txt")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(targetFolder, "Series 02.txt")));
    }

    [Fact]
    public void FolderMergeOperations_PreviewTargetFolderPathAutoNumbersExistingTarget()
    {
        using var temp = TempDirectory.Create();
        var first = temp.GetPath("Series 01.txt");
        var second = temp.GetPath("Series 02.txt");
        Directory.CreateDirectory(temp.GetPath("Series 0"));
        File.WriteAllText(first, "one");
        File.WriteAllText(second, "two");

        var target = FolderMergeOperations.PreviewTargetFolderPath([first, second], new FileToolsSettings());

        Assert.Equal(temp.GetPath("Series 0 (2)"), target);
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
