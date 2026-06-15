using FileTools;
using System.Reflection;
using System.Windows.Forms;

namespace FileTools.Tests;

public sealed class SettingsAndConfigurationTests
{
    [Fact]
    public void FileToolsSettings_CloneDeepCopiesExtensionRules()
    {
        var settings = new FileToolsSettings
        {
            FileCompareOptions = new FileCompareOptions
            {
                RangeBytes = 4096
            },
            RenameCorrectionPlugins = new RenameCorrectionPluginOptions
            {
                Enabled = true,
                Language = "en-US",
                Plugins =
                [
                    new RenameCorrectionPluginConfiguration
                    {
                        PluginId = "filetools.symspell",
                        Enabled = true,
                        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["dictionaryPath"] = "dictionary.txt"
                        }
                    }
                ]
            },
            RenamePatternLearningEnabled = false,
            RenamePatternFeedbackLimit = 1234,
            ContextMenuFileCompare = false,
            ContextMenuFolderMergeSelectedTargets = false,
            ActionToolbarSize = ActionToolbarSize.Large,
            FileKindExtensionRules =
            [
                new FileKindExtensionRule
                {
                    Kind = "Text",
                    Extensions = [".txt"]
                }
            ]
        };

        var clone = settings.Clone();
        clone.FileKindExtensionRules[0].Extensions.Add(".md");
        clone.FileCompareOptions.RangeBytes = 8192;
        clone.RenameCorrectionPlugins.Plugins[0].Settings["dictionaryPath"] = "changed.txt";

        Assert.Equal([".txt"], settings.FileKindExtensionRules[0].Extensions);
        Assert.Equal([".txt", ".md"], clone.FileKindExtensionRules[0].Extensions);
        Assert.Equal(4096, settings.FileCompareOptions.RangeBytes);
        Assert.Equal(8192, clone.FileCompareOptions.RangeBytes);
        Assert.Equal("dictionary.txt", settings.RenameCorrectionPlugins.Plugins[0].Settings["dictionaryPath"]);
        Assert.Equal("changed.txt", clone.RenameCorrectionPlugins.Plugins[0].Settings["dictionaryPath"]);
        Assert.False(clone.RenamePatternLearningEnabled);
        Assert.Equal(1234, clone.RenamePatternFeedbackLimit);
        Assert.False(clone.ContextMenuFileCompare);
        Assert.False(clone.ContextMenuFolderMergeSelectedTargets);
        Assert.Equal(ActionToolbarSize.Large, clone.ActionToolbarSize);
    }

    [Theory]
    [InlineData((int)ActionToolbarSize.Small, 1)]
    [InlineData((int)ActionToolbarSize.Medium, 2)]
    [InlineData((int)ActionToolbarSize.Large, 4)]
    public void MainForm_GetActionToolbarScaleMapsSettingsToExpectedScale(int size, int expectedScale)
    {
        Assert.Equal(expectedScale, MainForm.GetActionToolbarScale((ActionToolbarSize)size));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(80)]
    public void UiIconFactory_GetIconCreatesNativeSizedToolbarImages(int imageSize)
    {
        var icon = UiIconFactory.GetIcon(UiIconKind.ArchiveMerge, imageSize);

        Assert.Equal(imageSize, icon.Width);
        Assert.Equal(imageSize, icon.Height);
    }

    [Fact]
    public void ApplicationIconProvider_LoadsEmbeddedApplicationIcon()
    {
        using var icon = ApplicationIconProvider.GetApplicationIcon();

        Assert.NotNull(icon);
        Assert.True(icon.Width > 0);
        Assert.True(icon.Height > 0);
    }

    [Fact]
    public void MainForm_ConstructsWithApplicationIcon()
    {
        using var form = new MainForm();

        Assert.NotNull(form.Icon);
    }

    [Fact]
    public void MainForm_FileMenuIncludesExitCommand()
    {
        using var form = new MainForm();

        var fileMenu = GetPrivateField<ToolStripMenuItem>(form, "_fileMenuItem");
        var exitMenu = GetPrivateField<ToolStripMenuItem>(form, "_exitMenuItem");

        Assert.Contains(exitMenu, fileMenu.DropDownItems.Cast<ToolStripItem>());
        Assert.Equal(Localizer.Get("MenuExit"), exitMenu.Text);
        Assert.NotNull(exitMenu.Image);
    }

    [Fact]
    public void DialogButtonPanelFactory_CreateRightAlignedUsesStableBottomButtonColumns()
    {
        using var cancelButton = new Button
        {
            Width = 96,
            Height = 30
        };
        using var hideButton = new Button
        {
            Width = 96,
            Height = 30
        };

        using var panel = DialogButtonPanelFactory.CreateRightAligned(cancelButton, hideButton);

        Assert.Equal(3, panel.ColumnCount);
        Assert.Equal(SizeType.Percent, panel.ColumnStyles[0].SizeType);
        Assert.Equal(100, panel.ColumnStyles[0].Width);
        Assert.Equal(96, panel.ColumnStyles[1].Width);
        Assert.Equal(104, panel.ColumnStyles[2].Width);
        Assert.Equal(AnchorStyles.Top | AnchorStyles.Right, cancelButton.Anchor);
        Assert.Equal(AnchorStyles.Top | AnchorStyles.Right, hideButton.Anchor);
        Assert.Equal(new Padding(8, 0, 0, 0), hideButton.Margin);
    }

    [Fact]
    public void MainForm_FinishFileCompareExecutionClearsExecutionUiState()
    {
        using var form = new MainForm();
        var state = new FileCompareProgressState(2);
        var cancellationField = GetPrivateFieldInfo(form, "_executionCancellation");
        cancellationField.SetValue(form, state.Cancellation);

        form.FinishFileCompareExecution(state, hideProgressDialog: false);

        Assert.Null(cancellationField.GetValue(form));
        Assert.Equal(Localizer.Get("ButtonRun"), GetPrivateField<Button>(form, "_runStopButton").Text);
        Assert.Equal(ProgressBarStyle.Blocks, GetPrivateField<ProgressBar>(form, "_planProgressBar").Style);
        Assert.Equal(0, GetPrivateField<ProgressBar>(form, "_planProgressBar").MarqueeAnimationSpeed);
    }

    [Fact]
    public void IsAnyContextMenuFolderOperationEnabled_ConsidersFolderMergeSelectedTargets()
    {
        var settings = new FileToolsSettings
        {
            ContextMenuFolderWrapFiles = false,
            ContextMenuFolderUnwrapSameNameSingleFile = false,
            ContextMenuFolderUnwrapSingleFile = false,
            ContextMenuFolderMoveInnerFilesUp = false,
            ContextMenuFolderMergeSelectedTargets = false
        };
        Assert.False(settings.IsAnyContextMenuFolderOperationEnabled);

        settings.ContextMenuFolderMergeSelectedTargets = true;
        Assert.True(settings.IsAnyContextMenuFolderOperationEnabled);
    }

    private static T GetPrivateField<T>(object instance, string name)
        where T : class
    {
        return Assert.IsType<T>(GetPrivateFieldInfo(instance, name).GetValue(instance));
    }

    private static FieldInfo GetPrivateFieldInfo(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field;
    }

    [Fact]
    public void FileNamePatternFeedbackStore_CreateOptionsClampsSettingsLimit()
    {
        var options = FileNamePatternFeedbackStore.CreateOptions(new FileToolsSettings
        {
            RenamePatternLearningEnabled = false,
            RenamePatternFeedbackLimit = 1
        });

        Assert.False(options.Enabled);
        Assert.Equal(FileNamePatternFeedbackStore.MinimumFeedbackLimit, options.FeedbackLimit);
    }

    [Fact]
    public void RenameRuleStore_NormalizeRulesKeepsRequiredRuleEnabledAndAddsUserRule()
    {
        var rules = RenameRuleStore.NormalizeRules(
            [
                new RenameCorrectionRule
                {
                    Id = RenameRuleIds.WindowsSafeFileName,
                    Enabled = false,
                    Mode = RenameCorrectionRuleMode.Review
                },
                new RenameCorrectionRule
                {
                    Id = "user.trim-prefix",
                    DisplayName = " Trim prefix ",
                    Kind = RenameCorrectionRuleKind.PrefixTrim,
                    Stage = RenameCorrectionRuleStage.UserRewrite,
                    Source = "raw-",
                    Replacement = "",
                    Enabled = true
                }
            ]);

        var required = rules.Single(rule => rule.Id == RenameRuleIds.WindowsSafeFileName);
        var userRule = rules.Single(rule => rule.Id == "user.trim-prefix");
        Assert.True(required.Enabled);
        Assert.Equal(RenameCorrectionRuleMode.Automatic, required.Mode);
        Assert.Equal("Trim prefix", userRule.DisplayName);
        Assert.Equal("raw-", userRule.Source);
    }

    [Fact]
    public void ContextMenuCommandLine_TryParseCommandAcceptsFileCompare()
    {
        var parsed = ContextMenuCommandLine.TryParseCommand("FileCompare", out var command);

        Assert.True(parsed);
        Assert.Equal(ContextMenuCommand.FileCompare, command);
    }

    [Fact]
    public void ContextMenuCommandLine_TryParseCommandAcceptsFolderMergeSelectedTargets()
    {
        var parsed = ContextMenuCommandLine.TryParseCommand("FolderMergeSelectedTargets", out var command);

        Assert.True(parsed);
        Assert.Equal(ContextMenuCommand.FolderMergeSelectedTargets, command);
    }

    [Fact]
    public void ContextMenuCommandLine_TryParseCommandAcceptsFolderUnwrapPrefixFolderName()
    {
        var parsed = ContextMenuCommandLine.TryParseCommand("FolderUnwrapPrefixFolderName", out var command);

        Assert.True(parsed);
        Assert.Equal(ContextMenuCommand.FolderUnwrapPrefixFolderName, command);
    }

    [Fact]
    public void ContextMenuCommandLine_CreateRegistryCommandBuildsFileCompareContextLaunch()
    {
        var commandLine = ContextMenuCommandLine.CreateRegistryCommand(
            @"C:\Tools\FileTools.exe",
            ContextMenuCommand.FileCompare);

        Assert.Equal(@"""C:\Tools\FileTools.exe"" /context FileCompare ""%1""", commandLine);
    }

    [Fact]
    public void ContextMenuCommandLine_CreateRegistryCommandBuildsFolderMergeContextLaunch()
    {
        var commandLine = ContextMenuCommandLine.CreateRegistryCommand(
            @"C:\Tools\FileTools.exe",
            ContextMenuCommand.FolderMergeSelectedTargets);

        Assert.Equal(@"""C:\Tools\FileTools.exe"" /context FolderMergeSelectedTargets ""%1""", commandLine);
    }

    [Fact]
    public void ContextMenuCommandLine_CreateRegistryCommandBuildsFolderUnwrapPrefixContextLaunch()
    {
        var commandLine = ContextMenuCommandLine.CreateRegistryCommand(
            @"C:\Tools\FileTools.exe",
            ContextMenuCommand.FolderUnwrapPrefixFolderName);

        Assert.Equal(@"""C:\Tools\FileTools.exe"" /context FolderUnwrapPrefixFolderName ""%1""", commandLine);
    }

    [Fact]
    public void ArchiveMergeProgressDialog_CanConstructBeforeLayoutCompletes()
    {
        using var dialog = new ArchiveMergeProgressDialog(new ArchiveMergeOptions
        {
            SourcePaths = [@"C:\Temp\a.zip", @"C:\Temp\b.zip"],
            OutputPath = @"C:\Temp\merged.zip"
        });

        Assert.Equal(Localizer.Get("ArchiveMergeProgressDialogTitle"), dialog.Text);
    }

    [Fact]
    public void FileCompareText_ConvertsRangeUnitsWithCeiling()
    {
        var kib = FileCompareText.ConvertBytesToRangeValue(1536, FileCompareRangeUnit.KiB);
        var bytes = FileCompareText.ConvertRangeValueToBytes(kib, FileCompareRangeUnit.KiB);

        Assert.Equal(2, kib);
        Assert.Equal(2048, bytes);
    }
}
