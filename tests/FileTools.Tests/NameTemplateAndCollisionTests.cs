using FileTools;

namespace FileTools.Tests;

public sealed class NameTemplateAndCollisionTests
{
    [Fact]
    public void NameTemplateResolver_ResolvesSelectionAndNumberTokens()
    {
        var context = new NameTemplateContext
        {
            CommonStem = "Series",
            SelectedCount = 3,
            TargetExtension = ".zip"
        };

        var result = NameTemplateResolver.Default.Evaluate(
            "{CommonStem}-{SelectedCount:000}{TargetExtension}",
            context);

        Assert.Equal(NameTemplateEvaluationStatus.Ready, result.Status);
        Assert.Equal("Series-003.zip", result.Value);
    }

    [Fact]
    public void NameTemplateResolver_ReturnsInvalidTemplateForUnclosedToken()
    {
        var result = NameTemplateResolver.Default.Evaluate("{FileStem", new NameTemplateContext());

        Assert.Equal(NameTemplateEvaluationStatus.InvalidTemplate, result.Status);
    }

    [Fact]
    public void NameCollisionResolver_AutoNumbersExistingFile()
    {
        using var temp = TempDirectory.Create();
        var existing = temp.GetPath("Report.txt");
        File.WriteAllText(existing, "existing");

        var result = NameCollisionResolver.Resolve(
            temp.Root,
            "Report.txt",
            new NameCollisionOptions
            {
                Policy = NameCollisionPolicy.AutoNumber,
                TargetKind = NameCollisionTargetKind.File
            });

        Assert.True(result.IsReady);
        Assert.True(result.HadCollision);
        Assert.Equal("Report (2).txt", result.TargetName);
        Assert.Equal(temp.GetPath("Report (2).txt"), result.TargetPath);
    }

    [Fact]
    public void FolderStructureNameTemplates_ResolveUnwrapMismatchWithFolderPrefix()
    {
        var fileName = FolderStructureNameTemplates.ResolveUnwrappedFileName(
            "FolderName",
            "child.txt",
            FolderUnwrapNameMismatchMode.PrefixFolderName);

        Assert.Equal("FolderName-child.txt", fileName);
    }
}
