using System.Drawing;
using System.Windows.Forms;

#nullable enable

namespace FileTools;

internal sealed partial class SettingsForm
{
    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        root.Controls.Add(BuildStatusPanel(), 0, 0);
        root.Controls.Add(BuildScrollableSettingsPanel(), 0, 1);
        root.Controls.Add(BuildButtonPanel(), 0, 2);
    }

    private Control BuildStatusPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(14, 10, 14, 10)
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(203, 213, 225));
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        _statusTitleLabel.AutoSize = false;
        _statusTitleLabel.Left = 14;
        _statusTitleLabel.Top = 9;
        _statusTitleLabel.Height = 24;
        _statusTitleLabel.Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold);
        _statusTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusTitleLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(_statusTitleLabel);

        _statusDetailLabel.AutoSize = false;
        _statusDetailLabel.Left = 14;
        _statusDetailLabel.Top = 35;
        _statusDetailLabel.Height = 22;
        _statusDetailLabel.ForeColor = Color.FromArgb(31, 41, 55);
        _statusDetailLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusDetailLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(_statusDetailLabel);

        _statusHintLabel.AutoSize = false;
        _statusHintLabel.Left = 14;
        _statusHintLabel.Top = 60;
        _statusHintLabel.Height = 20;
        _statusHintLabel.ForeColor = Color.FromArgb(100, 116, 139);
        _statusHintLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusHintLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(_statusHintLabel);

        panel.Resize += (_, _) =>
        {
            var width = Math.Max(120, panel.ClientSize.Width - 28);
            _statusTitleLabel.Width = width;
            _statusDetailLabel.Width = width;
            _statusHintLabel.Width = width;
        };

        return panel;
    }

    private Control BuildScrollableSettingsPanel()
    {
        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(244, 246, 248),
            Padding = new Padding(0, 8, 0, 8)
        };

        _settingsStack.FlowDirection = FlowDirection.TopDown;
        _settingsStack.WrapContents = false;
        _settingsStack.AutoSize = true;
        _settingsStack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _settingsStack.Dock = DockStyle.Top;
        _settingsStack.Padding = new Padding(0);
        scrollHost.Controls.Add(_settingsStack);

        _contextMenuGroup = CreateContextMenuGroup();
        _renameGroup = CreateRenameGroup();
        _folderGroup = CreateFolderGroup();
        _fileCompareGroup = CreateFileCompareGroup();
        _archiveMergeGroup = CreateArchiveMergeGroup();
        _relocationGroup = CreateRelocationGroup();
        _settingsStack.Controls.Add(_contextMenuGroup);
        _settingsStack.Controls.Add(_renameGroup);
        _settingsStack.Controls.Add(_folderGroup);
        _settingsStack.Controls.Add(_fileCompareGroup);
        _settingsStack.Controls.Add(_archiveMergeGroup);
        _settingsStack.Controls.Add(_relocationGroup);

        scrollHost.Resize += (_, _) => ResizeGroups(scrollHost);
        _settingsStack.SizeChanged += (_, _) => ResizeGroups(scrollHost);

        return scrollHost;
    }

    private Control BuildButtonPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(0, 8, 0, 0)
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
        };
        var okButton = new Button { Text = "OK", Width = 94, Height = 30, Margin = new Padding(8, 0, 0, 0) };
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 94,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        okButton.Click += (_, _) => SaveAndClose();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);
        panel.Controls.Add(buttons, 0, 0);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        return panel;
    }

    private CollapsibleSettingsGroup CreateContextMenuGroup()
    {
        _registerContextMenuCheckBox.Text = Localizer.Get("CheckRegisterContextMenu");
        ConfigureCheckBox(_registerContextMenuCheckBox);
        _contextMenuOpenCheckBox.Text = Localizer.Get("ToolOpenApp");
        ConfigureCheckBox(_contextMenuOpenCheckBox);
        _contextMenuRenameCheckBox.Text = ToolModeText.GetDisplayName(ToolMode.FileNameCorrection);
        ConfigureCheckBox(_contextMenuRenameCheckBox);
        _contextMenuFolderWrapCheckBox.Text = ToolModeText.GetDisplayName(FolderStructureOperation.WrapFiles);
        ConfigureCheckBox(_contextMenuFolderWrapCheckBox);
        _contextMenuFolderUnwrapSameNameCheckBox.Text = ToolModeText.GetDisplayName(FolderStructureOperation.UnwrapSameNameSingleFile);
        ConfigureCheckBox(_contextMenuFolderUnwrapSameNameCheckBox);
        _contextMenuFolderUnwrapSingleFileCheckBox.Text = ToolModeText.GetDisplayName(FolderStructureOperation.UnwrapSingleFileFolder);
        ConfigureCheckBox(_contextMenuFolderUnwrapSingleFileCheckBox);
        _contextMenuFolderMoveInnerFilesCheckBox.Text = ToolModeText.GetDisplayName(FolderStructureOperation.MoveInnerFilesUp);
        ConfigureCheckBox(_contextMenuFolderMoveInnerFilesCheckBox);
        _contextMenuRelocationCurrentCheckBox.Text = Localizer.Get("ContextCommandAutoRelocationCurrentFolder");
        ConfigureCheckBox(_contextMenuRelocationCurrentCheckBox);
        _contextMenuRelocationChooseTargetCheckBox.Text = Localizer.Get("ContextCommandAutoRelocationChooseTarget");
        ConfigureCheckBox(_contextMenuRelocationChooseTargetCheckBox);
        _contextMenuArchiveMergeGroupByArchiveNameCheckBox.Text = Localizer.Get("ContextCommandArchiveMergeGroupByArchiveName");
        ConfigureCheckBox(_contextMenuArchiveMergeGroupByArchiveNameCheckBox);
        _contextMenuArchiveMergePreserveInternalPathsCheckBox.Text = Localizer.Get("ContextCommandArchiveMergePreserveInternalPaths");
        ConfigureCheckBox(_contextMenuArchiveMergePreserveInternalPathsCheckBox);

        var group = new CollapsibleSettingsGroup(
            Localizer.Get("GroupContextMenu"),
            Color.FromArgb(37, 99, 235),
            GetContextMenuSummary,
            expanded: true);
        group.AddBodyControl(_registerContextMenuCheckBox);
        group.AddBodyControl(CreateHelperText(Localizer.Get("SettingsContextMenuRegistrationHelp")));
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("LabelContextMenuLayout"),
            _contextMenuLayoutCombo,
            Localizer.Get("SettingsContextMenuLayoutHelp")));
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("GroupContextMenuTasks")));
        group.AddBodyControl(_contextMenuRenameCheckBox);
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("GroupFolderStructure")));
        group.AddBodyControl(_contextMenuFolderWrapCheckBox);
        group.AddBodyControl(_contextMenuFolderUnwrapSameNameCheckBox);
        group.AddBodyControl(_contextMenuFolderUnwrapSingleFileCheckBox);
        group.AddBodyControl(_contextMenuFolderMoveInnerFilesCheckBox);
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("GroupAutoRelocationContextMenu")));
        group.AddBodyControl(_contextMenuRelocationCurrentCheckBox);
        group.AddBodyControl(_contextMenuRelocationChooseTargetCheckBox);
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("GroupArchiveMergeContextMenu")));
        group.AddBodyControl(_contextMenuArchiveMergeGroupByArchiveNameCheckBox);
        group.AddBodyControl(_contextMenuArchiveMergePreserveInternalPathsCheckBox);
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("GroupApplicationContextMenu")));
        group.AddBodyControl(_contextMenuOpenCheckBox);
        group.AddBodyControl(CreateContextMenuButtons());
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("GroupWindows11NativeContextMenu")));
        group.AddBodyControl(CreateHelperText(Localizer.Get("SettingsWindows11NativeContextMenuHelp")));
        group.AddBodyControl(CreateWindows11NativeContextMenuButtons());
        RegisterGroup(group);
        return group;
    }

    private CollapsibleSettingsGroup CreateArchiveMergeGroup()
    {
        _archiveMergeDeleteOriginalsCheckBox.Text = Localizer.Get("ArchiveMergeCheckDeleteOriginals");
        ConfigureCheckBox(_archiveMergeDeleteOriginalsCheckBox);

        var group = new CollapsibleSettingsGroup(
            Localizer.Get("TabArchiveMerge"),
            Color.FromArgb(20, 116, 148),
            GetArchiveMergeSummary,
            expanded: true);
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("ArchiveMergeLabelLayout"),
            _archiveMergeLayoutCombo,
            Localizer.Get("ArchiveMergeLayoutHelp")));
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("ArchiveMergeLabelCollision"),
            _archiveMergeCollisionCombo,
            Localizer.Get("ArchiveMergeCollisionHelp")));
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("ArchiveMergeLabelDuplicate"),
            _archiveMergeDuplicateCombo,
            Localizer.Get("ArchiveMergeDuplicateHelp")));
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("ArchiveMergeLabelFailure"),
            _archiveMergeFailureCombo,
            Localizer.Get("ArchiveMergeFailureHelp")));
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("ArchiveMergeLabelOutputName"),
            _archiveMergeOutputNameCombo,
            Localizer.Get("ArchiveMergeOutputNameHelp")));
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("ArchiveMergeLabelCompression"),
            _archiveMergeCompressionCombo,
            Localizer.Get("ArchiveMergeCompressionHelp")));
        group.AddBodyControl(_archiveMergeDeleteOriginalsCheckBox);
        RegisterGroup(group);
        return group;
    }

    private CollapsibleSettingsGroup CreateFileCompareGroup()
    {
        _fileCompareNameCheckBox.Text = Localizer.Get("FileCompareCheckFileName");
        ConfigureCheckBox(_fileCompareNameCheckBox);
        _fileCompareCreatedTimeCheckBox.Text = Localizer.Get("FileCompareCheckCreatedTime");
        ConfigureCheckBox(_fileCompareCreatedTimeCheckBox);
        _fileCompareModifiedTimeCheckBox.Text = Localizer.Get("FileCompareCheckModifiedTime");
        ConfigureCheckBox(_fileCompareModifiedTimeCheckBox);
        _fileCompareSizeCheckBox.Text = Localizer.Get("FileCompareCheckFileSize");
        ConfigureCheckBox(_fileCompareSizeCheckBox);
        _fileCompareContentCheckBox.Text = Localizer.Get("FileCompareCheckContent");
        ConfigureCheckBox(_fileCompareContentCheckBox);
        _fileCompareExtractArchivesCheckBox.Text = Localizer.Get("FileCompareCheckExtractArchives");
        ConfigureCheckBox(_fileCompareExtractArchivesCheckBox);
        _fileCompareArchiveSameRelativePathOnlyCheckBox.Text = Localizer.Get("FileCompareCheckArchiveSameRelativePathOnly");
        ConfigureCheckBox(_fileCompareArchiveSameRelativePathOnlyCheckBox);
        _fileCompareEarlyExitCheckBox.Text = Localizer.Get("FileCompareCheckEarlyExit");
        ConfigureCheckBox(_fileCompareEarlyExitCheckBox);
        _fileCompareHashCacheCheckBox.Text = Localizer.Get("FileCompareCheckHashCache");
        ConfigureCheckBox(_fileCompareHashCacheCheckBox);

        var group = new CollapsibleSettingsGroup(
            Localizer.Get("TabFileCompare"),
            Color.FromArgb(59, 130, 246),
            GetFileCompareSummary,
            expanded: true);
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("FileCompareGroupFileName")));
        group.AddBodyControl(_fileCompareNameCheckBox);
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("FileCompareLabelNameMode"),
            _fileCompareNameModeCombo,
            Localizer.Get("FileCompareNameModeHelp")));
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("FileCompareLabelCommonNameThresholdMode"),
            _fileCompareCommonNameThresholdModeCombo,
            Localizer.Get("FileCompareCommonNameThresholdModeHelp")));
        group.AddBodyControl(CreateTextRow(
            Localizer.Get("FileCompareLabelCommonNameThreshold"),
            _fileCompareCommonNameThresholdBox,
            Localizer.Get("FileCompareCommonNameThresholdHelp")));
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("FileCompareGroupMetadata")));
        group.AddBodyControl(_fileCompareCreatedTimeCheckBox);
        group.AddBodyControl(_fileCompareModifiedTimeCheckBox);
        group.AddBodyControl(_fileCompareSizeCheckBox);
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("FileCompareGroupContent")));
        group.AddBodyControl(_fileCompareContentCheckBox);
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("FileCompareLabelContentMode"),
            _fileCompareContentModeCombo,
            Localizer.Get("FileCompareContentModeHelp")));
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("FileCompareLabelRangeMode"),
            _fileCompareRangeModeCombo,
            Localizer.Get("FileCompareRangeModeHelp")));
        group.AddBodyControl(CreateTextRow(
            Localizer.Get("FileCompareLabelRangeStart"),
            _fileCompareRangeOffsetBox,
            Localizer.Get("FileCompareRangeStartHelp")));
        group.AddBodyControl(CreateTextComboRow(
            Localizer.Get("FileCompareLabelRangeLength"),
            _fileCompareRangeBytesBox,
            _fileCompareRangeUnitCombo,
            Localizer.Get("FileCompareRangeBytesHelp")));
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("FileCompareGroupArchiveExtraction")));
        group.AddBodyControl(_fileCompareExtractArchivesCheckBox);
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("FileCompareLabelArchiveOrder"),
            _fileCompareArchiveOrderCombo,
            Localizer.Get("FileCompareArchiveOrderHelp")));
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("FileCompareLabelArchiveLimitMode"),
            _fileCompareArchiveLimitModeCombo,
            Localizer.Get("FileCompareArchiveLimitModeHelp")));
        group.AddBodyControl(CreateTextRow(
            Localizer.Get("FileCompareLabelArchiveLimitCount"),
            _fileCompareArchiveLimitCountBox,
            Localizer.Get("FileCompareArchiveLimitCountHelp")));
        group.AddBodyControl(_fileCompareArchiveSameRelativePathOnlyCheckBox);
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("FileCompareGroupOther")));
        group.AddBodyControl(_fileCompareEarlyExitCheckBox);
        group.AddBodyControl(_fileCompareHashCacheCheckBox);
        group.AddBodyControl(CreateTextRow(
            Localizer.Get("FileCompareLabelPrefilterPercent"),
            _fileComparePrefilterPercentBox,
            Localizer.Get("FileComparePrefilterHelp")));
        RegisterGroup(group);
        return group;
    }

    private CollapsibleSettingsGroup CreateRenameGroup()
    {
        _renameDictionaryCheckBox.Text = Localizer.Get("CheckRenameUseDictionary");
        ConfigureCheckBox(_renameDictionaryCheckBox);

        var group = new CollapsibleSettingsGroup(
            Localizer.Get("TabRename"),
            Color.FromArgb(5, 150, 105),
            GetRenameSummary,
            expanded: true);
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("LabelRenameReviewMode"),
            _renameReviewModeCombo,
            Localizer.Get("SettingsRenameReviewHelp")));
        group.AddBodyControl(_renameDictionaryCheckBox);
        group.AddBodyControl(CreateRenameButtons());
        RegisterGroup(group);
        return group;
    }

    private CollapsibleSettingsGroup CreateFolderGroup()
    {
        var group = new CollapsibleSettingsGroup(
            Localizer.Get("TabFolderStructure"),
            Color.FromArgb(217, 119, 6),
            GetFolderSummary,
            expanded: true);
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("LabelDefaultFolderOperation"),
            _defaultFolderOperationCombo,
            Localizer.Get("SettingsFolderOperationHelp")));
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("LabelFolderUnwrapMismatch"),
            _folderMismatchCombo,
            Localizer.Get("SettingsFolderMismatchExample")));
        group.AddBodyControl(CreateFolderNameTemplateButton());
        RegisterGroup(group);
        return group;
    }

    private CollapsibleSettingsGroup CreateRelocationGroup()
    {
        var group = new CollapsibleSettingsGroup(
            Localizer.Get("TabAutoRelocation"),
            Color.FromArgb(124, 58, 237),
            GetRelocationSummary,
            expanded: true);
        group.AddBodyControl(CreateComboRow(
            Localizer.Get("LabelDefaultTemplate"),
            _defaultTemplateCombo,
            Localizer.Get("SettingsAutoRelocationHelp")));
        group.AddBodyControl(CreateTemplateButton());
        RegisterGroup(group);
        return group;
    }

    private Control CreateContextMenuButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Height = 40,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0)
        };
        var installButton = new Button { Text = Localizer.Get("ButtonInstallContextMenu"), Width = 168, Height = 30 };
        var uninstallButton = new Button { Text = Localizer.Get("ButtonUninstallContextMenu"), Width = 168, Height = 30 };
        installButton.Click += (_, _) => InstallContextMenu();
        uninstallButton.Click += (_, _) => UninstallContextMenu();
        panel.Controls.Add(installButton);
        panel.Controls.Add(uninstallButton);
        return panel;
    }

    private Control CreateRenameButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Height = 40,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0)
        };
        var ruleButton = new Button
        {
            Text = Localizer.Get("ButtonEditRenameRules"),
            Width = 190,
            Height = 30
        };
        ruleButton.Click += (_, _) => OpenRenameRuleEditor();
        panel.Controls.Add(ruleButton);
        return panel;
    }

    private Control CreateWindows11NativeContextMenuButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Height = 40,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0)
        };
        var registerButton = new Button { Text = Localizer.Get("ButtonRegisterWindows11ContextMenu"), Width = 220, Height = 30 };
        var unregisterButton = new Button { Text = Localizer.Get("ButtonUnregisterWindows11ContextMenu"), Width = 220, Height = 30 };
        registerButton.Click += (_, _) => RegisterWindows11NativeContextMenu();
        unregisterButton.Click += (_, _) => UnregisterWindows11NativeContextMenu();
        panel.Controls.Add(registerButton);
        panel.Controls.Add(unregisterButton);
        return panel;
    }

    private Control CreateTemplateButton()
    {
        var panel = new FlowLayoutPanel
        {
            Height = 40,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0)
        };
        var templateButton = new Button
        {
            Text = Localizer.Get("ButtonEditTemplates"),
            Width = 180,
            Height = 30
        };
        var classificationButton = new Button
        {
            Text = Localizer.Get("ButtonEditFileKindClassification"),
            Width = 220,
            Height = 30
        };
        templateButton.Click += (_, _) => OpenTemplateEditor();
        classificationButton.Click += (_, _) => OpenFileKindClassificationEditor();
        panel.Controls.Add(templateButton);
        panel.Controls.Add(classificationButton);
        return panel;
    }

    private Control CreateFolderNameTemplateButton()
    {
        var panel = new FlowLayoutPanel
        {
            Height = 40,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0)
        };
        var button = new Button
        {
            Text = Localizer.Get("ButtonEditNameTemplates"),
            Width = 190,
            Height = 30
        };
        button.Click += (_, _) => OpenNameTemplateSettings();
        panel.Controls.Add(button);
        return panel;
    }

    private void RegisterGroup(CollapsibleSettingsGroup group)
    {
        _groups.Add(group);
        group.ExpandedChanged += (_, _) => UpdateUiState();
    }

    private void ResizeGroups(Panel scrollHost)
    {
        var width = Math.Max(420, scrollHost.ClientSize.Width - scrollHost.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
        _settingsStack.Width = width;
        foreach (var group in _groups)
        {
            group.Width = width;
            group.RefreshLayoutSize();
        }
    }

    private static Panel CreateComboRow(string labelText, ComboBox combo, string? helpText = null)
    {
        var panel = new Panel
        {
            Height = string.IsNullOrWhiteSpace(helpText) ? 38 : 64,
            Margin = new Padding(0, 0, 0, 8)
        };
        var label = new Label
        {
            Text = labelText,
            Left = 0,
            Top = 3,
            Width = 190,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        combo.Left = 204;
        combo.Top = 1;
        combo.Height = 26;
        combo.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(label);
        panel.Controls.Add(combo);

        Label? helpLabel = null;
        if (!string.IsNullOrWhiteSpace(helpText))
        {
            helpLabel = CreateHelperText(helpText);
            helpLabel.Left = 204;
            helpLabel.Top = 31;
            helpLabel.Height = 32;
            helpLabel.Margin = new Padding(0);
            panel.Controls.Add(helpLabel);
        }

        void ResizeRow()
        {
            var labelWidth = Math.Clamp(panel.ClientSize.Width / 3, 150, 220);
            label.Width = labelWidth;
            combo.Left = labelWidth + 14;
            combo.Width = Math.Max(180, panel.ClientSize.Width - combo.Left);
            if (helpLabel is not null)
            {
                helpLabel.Left = combo.Left;
                helpLabel.Width = combo.Width;
            }
        }

        panel.Resize += (_, _) => ResizeRow();
        ResizeRow();
        return panel;
    }

    private static Panel CreateTextRow(string labelText, TextBox textBox, string? helpText = null)
    {
        var panel = new Panel
        {
            Height = string.IsNullOrWhiteSpace(helpText) ? 38 : 64,
            Margin = new Padding(0, 0, 0, 8)
        };
        var label = new Label
        {
            Text = labelText,
            Left = 0,
            Top = 3,
            Width = 190,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        textBox.Left = 204;
        textBox.Top = 1;
        textBox.Height = 26;
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(label);
        panel.Controls.Add(textBox);

        Label? helpLabel = null;
        if (!string.IsNullOrWhiteSpace(helpText))
        {
            helpLabel = CreateHelperText(helpText);
            helpLabel.Left = 204;
            helpLabel.Top = 31;
            helpLabel.Height = 32;
            helpLabel.Margin = new Padding(0);
            panel.Controls.Add(helpLabel);
        }

        void ResizeRow()
        {
            var labelWidth = Math.Clamp(panel.ClientSize.Width / 3, 150, 220);
            label.Width = labelWidth;
            textBox.Left = labelWidth + 14;
            textBox.Width = Math.Max(180, panel.ClientSize.Width - textBox.Left);
            if (helpLabel is not null)
            {
                helpLabel.Left = textBox.Left;
                helpLabel.Width = textBox.Width;
            }
        }

        panel.Resize += (_, _) => ResizeRow();
        ResizeRow();
        return panel;
    }

    private static Panel CreateTextComboRow(string labelText, TextBox textBox, ComboBox combo, string? helpText = null)
    {
        var panel = new Panel
        {
            Height = string.IsNullOrWhiteSpace(helpText) ? 38 : 64,
            Margin = new Padding(0, 0, 0, 8)
        };
        var label = new Label
        {
            Text = labelText,
            Left = 0,
            Top = 3,
            Width = 190,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        textBox.Left = 204;
        textBox.Top = 1;
        textBox.Height = 26;
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        combo.Left = 350;
        combo.Top = 1;
        combo.Height = 26;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        panel.Controls.Add(label);
        panel.Controls.Add(textBox);
        panel.Controls.Add(combo);

        Label? helpLabel = null;
        if (!string.IsNullOrWhiteSpace(helpText))
        {
            helpLabel = CreateHelperText(helpText);
            helpLabel.Left = 204;
            helpLabel.Top = 31;
            helpLabel.Height = 32;
            helpLabel.Margin = new Padding(0);
            panel.Controls.Add(helpLabel);
        }

        void ResizeRow()
        {
            var labelWidth = Math.Clamp(panel.ClientSize.Width / 3, 150, 220);
            label.Width = labelWidth;
            textBox.Left = labelWidth + 14;
            textBox.Width = 132;
            combo.Left = textBox.Right + 8;
            combo.Width = 92;
            if (helpLabel is not null)
            {
                helpLabel.Left = textBox.Left;
                helpLabel.Width = Math.Max(180, panel.ClientSize.Width - helpLabel.Left);
            }
        }

        panel.Resize += (_, _) => ResizeRow();
        ResizeRow();
        return panel;
    }

    private static Label CreateSectionLabel(string text)
    {
        return new Label
        {
            Text = text,
            Height = 30,
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 65, 81),
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 8, 0, 2)
        };
    }

    private static Label CreateHelperText(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Height = 36,
            ForeColor = Color.FromArgb(100, 116, 139),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 8)
        };
    }

    private static void ConfigureCheckBox(CheckBox checkBox)
    {
        checkBox.AutoSize = false;
        checkBox.Height = 28;
        checkBox.TextAlign = ContentAlignment.MiddleLeft;
        checkBox.Margin = new Padding(0, 0, 0, 2);
    }

    private sealed class CollapsibleSettingsGroup : Panel
    {
        private const int HeaderHeight = 40;
        private readonly Button _headerButton = new();
        private readonly Panel _bodyPanel = new();
        private readonly FlowLayoutPanel _bodyStack = new();
        private readonly Color _accentColor;
        private readonly Func<string> _summaryProvider;
        private bool _expanded;

        public CollapsibleSettingsGroup(string title, Color accentColor, Func<string> summaryProvider, bool expanded)
        {
            Title = title;
            _accentColor = accentColor;
            _summaryProvider = summaryProvider;
            _expanded = expanded;
            BackColor = Color.White;
            Margin = new Padding(0, 0, 0, 10);
            DoubleBuffered = true;

            _bodyStack.FlowDirection = FlowDirection.TopDown;
            _bodyStack.WrapContents = false;
            _bodyStack.AutoSize = true;
            _bodyStack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _bodyStack.Dock = DockStyle.Top;
            _bodyStack.Padding = new Padding(0);
            _bodyPanel.Dock = DockStyle.Top;
            _bodyPanel.Padding = new Padding(14, 12, 14, 12);
            _bodyPanel.BackColor = Color.White;
            _bodyPanel.Controls.Add(_bodyStack);
            Controls.Add(_bodyPanel);

            _headerButton.Dock = DockStyle.Top;
            _headerButton.Height = HeaderHeight;
            _headerButton.FlatStyle = FlatStyle.Flat;
            _headerButton.FlatAppearance.BorderSize = 0;
            _headerButton.TextAlign = ContentAlignment.MiddleLeft;
            _headerButton.UseVisualStyleBackColor = false;
            _headerButton.AutoEllipsis = true;
            _headerButton.Click += (_, _) => ToggleExpanded();
            Controls.Add(_headerButton);

            Resize += (_, _) => RefreshLayoutSize();
            RefreshSummary();
            RefreshLayoutSize();
        }

        public event EventHandler? ExpandedChanged;

        public string Title { get; }

        public void AddBodyControl(Control control)
        {
            _bodyStack.Controls.Add(control);
            RefreshLayoutSize();
        }

        public void RefreshSummary()
        {
            var marker = _expanded ? "v" : ">";
            var summary = _summaryProvider();
            _headerButton.Text = string.IsNullOrWhiteSpace(summary)
                ? $"{marker}  {Title}"
                : $"{marker}  {Title}    {summary}";
            _headerButton.BackColor = _expanded
                ? Blend(_accentColor, Color.White, 0.84)
                : Color.White;
            _headerButton.ForeColor = Color.FromArgb(31, 41, 55);
            Invalidate();
        }

        public void RefreshLayoutSize()
        {
            var bodyWidth = Math.Max(260, ClientSize.Width - _bodyPanel.Padding.Horizontal - 2);
            _bodyStack.Width = bodyWidth;
            foreach (Control control in _bodyStack.Controls)
            {
                control.Width = Math.Max(120, bodyWidth - control.Margin.Horizontal);
            }

            _bodyPanel.Visible = _expanded;
            var bodyHeight = 0;
            if (_expanded)
            {
                bodyHeight = _bodyStack.GetPreferredSize(new Size(bodyWidth, 0)).Height + _bodyPanel.Padding.Vertical;
                _bodyPanel.Height = bodyHeight;
            }

            Height = HeaderHeight + bodyHeight + 2;
            RefreshSummary();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(_accentColor, _expanded ? 2 : 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private void ToggleExpanded()
        {
            _expanded = !_expanded;
            RefreshLayoutSize();
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }

        private static Color Blend(Color color, Color backColor, double backAmount)
        {
            var colorAmount = 1 - backAmount;
            return Color.FromArgb(
                (int)((color.R * colorAmount) + (backColor.R * backAmount)),
                (int)((color.G * colorAmount) + (backColor.G * backAmount)),
                (int)((color.B * colorAmount) + (backColor.B * backAmount)));
        }
    }
}
