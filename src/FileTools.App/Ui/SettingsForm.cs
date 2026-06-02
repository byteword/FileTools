using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _registerContextMenuCheckBox = new();
    private readonly CheckBox _contextMenuOpenCheckBox = new();
    private readonly CheckBox _contextMenuRenameCheckBox = new();
    private readonly CheckBox _contextMenuFolderWrapCheckBox = new();
    private readonly CheckBox _contextMenuFolderUnwrapSameNameCheckBox = new();
    private readonly CheckBox _contextMenuFolderUnwrapSingleFileCheckBox = new();
    private readonly CheckBox _contextMenuFolderMoveInnerFilesCheckBox = new();
    private readonly CheckBox _contextMenuRelocationCurrentCheckBox = new();
    private readonly CheckBox _contextMenuRelocationChooseTargetCheckBox = new();
    private readonly ComboBox _contextMenuLayoutCombo = new();
    private readonly ComboBox _defaultFolderOperationCombo = new();
    private readonly ComboBox _folderMismatchCombo = new();
    private readonly ComboBox _defaultTemplateCombo = new();
    private readonly ComboBox _renameReviewModeCombo = new();
    private readonly CheckBox _renameDictionaryCheckBox = new();
    private readonly Label _statusTitleLabel = new();
    private readonly Label _statusDetailLabel = new();
    private readonly Label _statusHintLabel = new();
    private readonly FlowLayoutPanel _settingsStack = new();
    private readonly List<CollapsibleSettingsGroup> _groups = [];
    private CollapsibleSettingsGroup? _contextMenuGroup;
    private CollapsibleSettingsGroup? _renameGroup;
    private CollapsibleSettingsGroup? _folderGroup;
    private CollapsibleSettingsGroup? _relocationGroup;

    public SettingsForm(FileToolsSettings settings)
    {
        Settings = settings.Clone();
        Text = Localizer.Get("SettingsFormTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 720);
        MinimumSize = new Size(820, 640);

        BuildLayout();
        LoadSettings();
        WireUiStateUpdates();
        UpdateUiState();
    }

    public FileToolsSettings Settings { get; private set; }

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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
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
        _relocationGroup = CreateRelocationGroup();
        _settingsStack.Controls.Add(_contextMenuGroup);
        _settingsStack.Controls.Add(_renameGroup);
        _settingsStack.Controls.Add(_folderGroup);
        _settingsStack.Controls.Add(_relocationGroup);

        scrollHost.Resize += (_, _) => ResizeGroups(scrollHost);
        _settingsStack.SizeChanged += (_, _) => ResizeGroups(scrollHost);

        return scrollHost;
    }

    private Control BuildButtonPanel()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        var okButton = new Button { Text = "OK", Width = 94, Height = 30 };
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 94,
            Height = 30
        };
        okButton.Click += (_, _) => SaveAndClose();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        return buttons;
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
        group.AddBodyControl(CreateSectionLabel(Localizer.Get("GroupApplicationContextMenu")));
        group.AddBodyControl(_contextMenuOpenCheckBox);
        group.AddBodyControl(CreateContextMenuButtons());
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
        var dictionaryButton = new Button
        {
            Text = Localizer.Get("ButtonEditRenameDictionary"),
            Width = 210,
            Height = 30
        };
        var phraseButton = new Button
        {
            Text = Localizer.Get("ButtonEditCommonPhrases"),
            Width = 230,
            Height = 30
        };
        dictionaryButton.Click += (_, _) => OpenRenameDictionaryEditor();
        phraseButton.Click += (_, _) => OpenCommonPhraseEditor();
        panel.Controls.Add(dictionaryButton);
        panel.Controls.Add(phraseButton);
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
        templateButton.Click += (_, _) => OpenTemplateEditor();
        panel.Controls.Add(templateButton);
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

    private void LoadSettings()
    {
        _registerContextMenuCheckBox.Checked = Settings.RegisterContextMenu;
        _contextMenuOpenCheckBox.Checked = Settings.ContextMenuOpenApp;
        _contextMenuRenameCheckBox.Checked = Settings.ContextMenuFileNameCorrection;
        _contextMenuFolderWrapCheckBox.Checked = Settings.ContextMenuFolderWrapFiles;
        _contextMenuFolderUnwrapSameNameCheckBox.Checked = Settings.ContextMenuFolderUnwrapSameNameSingleFile;
        _contextMenuFolderUnwrapSingleFileCheckBox.Checked = Settings.ContextMenuFolderUnwrapSingleFile;
        _contextMenuFolderMoveInnerFilesCheckBox.Checked = Settings.ContextMenuFolderMoveInnerFilesUp;
        _contextMenuRelocationCurrentCheckBox.Checked = Settings.ContextMenuAutoRelocationCurrentFolder;
        _contextMenuRelocationChooseTargetCheckBox.Checked = Settings.ContextMenuAutoRelocationChooseTarget;
        _renameDictionaryCheckBox.Checked = Settings.RenameUseDictionary;

        ConfigureCombo(_renameReviewModeCombo, Enum.GetValues<RenameReviewMode>()
            .Select(mode => new ComboOption<RenameReviewMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray());
        SelectComboValue(_renameReviewModeCombo, Settings.RenameReviewMode);

        ConfigureCombo(_contextMenuLayoutCombo, Enum.GetValues<ContextMenuLayout>()
            .Select(layout => new ComboOption<ContextMenuLayout>(ToolModeText.GetDisplayName(layout), layout))
            .ToArray());
        SelectComboValue(_contextMenuLayoutCombo, Settings.ContextMenuLayout);

        ConfigureCombo(_defaultFolderOperationCombo, Enum.GetValues<FolderStructureOperation>()
            .Select(operation => new ComboOption<FolderStructureOperation>(ToolModeText.GetDisplayName(operation), operation))
            .ToArray());
        SelectComboValue(_defaultFolderOperationCombo, Settings.FolderStructureOperation);

        ConfigureCombo(_folderMismatchCombo, Enum.GetValues<FolderUnwrapNameMismatchMode>()
            .Select(mode => new ComboOption<FolderUnwrapNameMismatchMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray());
        SelectComboValue(_folderMismatchCombo, Settings.FolderUnwrapNameMismatchMode);

        RefreshTemplateCombo(Settings.AutoRelocationTemplateId);
    }

    private static void ConfigureCombo<T>(ComboBox combo, ComboOption<T>[] options)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DataSource = options;
    }

    private void WireUiStateUpdates()
    {
        foreach (var checkBox in new[]
        {
            _registerContextMenuCheckBox,
            _contextMenuOpenCheckBox,
            _contextMenuRenameCheckBox,
            _contextMenuFolderWrapCheckBox,
            _contextMenuFolderUnwrapSameNameCheckBox,
            _contextMenuFolderUnwrapSingleFileCheckBox,
            _contextMenuFolderMoveInnerFilesCheckBox,
            _contextMenuRelocationCurrentCheckBox,
            _contextMenuRelocationChooseTargetCheckBox,
            _renameDictionaryCheckBox
        })
        {
            checkBox.CheckedChanged += (_, _) => UpdateUiState();
        }

        foreach (var combo in new[]
        {
            _renameReviewModeCombo,
            _contextMenuLayoutCombo,
            _defaultFolderOperationCombo,
            _folderMismatchCombo,
            _defaultTemplateCombo
        })
        {
            combo.SelectedIndexChanged += (_, _) => UpdateUiState();
        }
    }

    private void UpdateUiState()
    {
        var contextStatus = _registerContextMenuCheckBox.Checked
            ? Localizer.Get("SettingsContextMenuRegistered")
            : Localizer.Get("SettingsContextMenuNotRegistered");
        var commandCount = GetEnabledContextMenuCommandCount();
        _statusTitleLabel.Text = Localizer.Get("SettingsStatusTitle");
        _statusDetailLabel.Text = _registerContextMenuCheckBox.Checked
            ? Localizer.Format("SettingsStatusRegisteredFormat", commandCount)
            : Localizer.Format("SettingsStatusNotRegisteredFormat", commandCount);
        _statusHintLabel.Text = Localizer.Format("SettingsStatusApplyHintFormat", contextStatus);

        foreach (var group in _groups)
        {
            group.RefreshSummary();
        }
    }

    private string GetContextMenuSummary()
    {
        var commandCount = GetEnabledContextMenuCommandCount();
        return _registerContextMenuCheckBox.Checked
            ? Localizer.Format("SettingsSummaryRegisteredEnabledCountFormat", commandCount)
            : Localizer.Format("SettingsSummaryNotRegisteredEnabledCountFormat", commandCount);
    }

    private string GetRenameSummary()
    {
        var dictionaryState = _renameDictionaryCheckBox.Checked
            ? Localizer.Get("SettingsSummaryDictionaryOn")
            : Localizer.Get("SettingsSummaryDictionaryOff");
        return Localizer.Format("SettingsSummaryPairFormat", GetComboText(_renameReviewModeCombo), dictionaryState);
    }

    private string GetFolderSummary()
    {
        return Localizer.Format(
            "SettingsSummaryPairFormat",
            GetComboText(_defaultFolderOperationCombo),
            GetComboText(_folderMismatchCombo));
    }

    private string GetRelocationSummary()
    {
        return TrimSummary(GetComboText(_defaultTemplateCombo), 64);
    }

    private int GetEnabledContextMenuCommandCount()
    {
        return new[]
        {
            _contextMenuOpenCheckBox,
            _contextMenuRenameCheckBox,
            _contextMenuFolderWrapCheckBox,
            _contextMenuFolderUnwrapSameNameCheckBox,
            _contextMenuFolderUnwrapSingleFileCheckBox,
            _contextMenuFolderMoveInnerFilesCheckBox,
            _contextMenuRelocationCurrentCheckBox,
            _contextMenuRelocationChooseTargetCheckBox
        }.Count(static checkBox => checkBox.Checked);
    }

    private void SaveAndClose()
    {
        try
        {
            SaveSettingsFromUi();
            SettingsStore.Save(Settings);
            SyncContextMenuRegistration();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSettingsFromUi()
    {
        Settings.RegisterContextMenu = _registerContextMenuCheckBox.Checked;
        Settings.ContextMenuOpenApp = _contextMenuOpenCheckBox.Checked;
        Settings.ContextMenuFileNameCorrection = _contextMenuRenameCheckBox.Checked;
        Settings.ContextMenuFolderStructure = true;
        Settings.ContextMenuFolderWrapFiles = _contextMenuFolderWrapCheckBox.Checked;
        Settings.ContextMenuFolderUnwrapSameNameSingleFile = _contextMenuFolderUnwrapSameNameCheckBox.Checked;
        Settings.ContextMenuFolderUnwrapSingleFile = _contextMenuFolderUnwrapSingleFileCheckBox.Checked;
        Settings.ContextMenuFolderMoveInnerFilesUp = _contextMenuFolderMoveInnerFilesCheckBox.Checked;
        Settings.ContextMenuAutoRelocation = true;
        Settings.ContextMenuAutoRelocationCurrentFolder = _contextMenuRelocationCurrentCheckBox.Checked;
        Settings.ContextMenuAutoRelocationChooseTarget = _contextMenuRelocationChooseTargetCheckBox.Checked;
        Settings.RenameUseDictionary = _renameDictionaryCheckBox.Checked;

        if (_renameReviewModeCombo.SelectedItem is ComboOption<RenameReviewMode> renameReviewMode)
        {
            Settings.RenameReviewMode = renameReviewMode.Value;
        }

        if (_contextMenuLayoutCombo.SelectedItem is ComboOption<ContextMenuLayout> layout)
        {
            Settings.ContextMenuLayout = layout.Value;
        }

        if (_defaultFolderOperationCombo.SelectedItem is ComboOption<FolderStructureOperation> operation)
        {
            Settings.FolderStructureOperation = operation.Value;
        }

        if (_folderMismatchCombo.SelectedItem is ComboOption<FolderUnwrapNameMismatchMode> mismatchMode)
        {
            Settings.FolderUnwrapNameMismatchMode = mismatchMode.Value;
        }

        if (_defaultTemplateCombo.SelectedItem is ComboOption<string> template)
        {
            Settings.AutoRelocationTemplateId = template.Value;
        }
    }

    private void SyncContextMenuRegistration()
    {
        if (Settings.RegisterContextMenu)
        {
            ContextMenuRegistrar.Install(Environment.ProcessPath ?? "", Settings);
            return;
        }

        ContextMenuRegistrar.Uninstall();
    }

    private void InstallContextMenu()
    {
        try
        {
            SaveSettingsFromUi();
            Settings.RegisterContextMenu = true;
            _registerContextMenuCheckBox.Checked = true;
            SettingsStore.Save(Settings);
            var installedPath = ContextMenuRegistrar.Install(Environment.ProcessPath ?? "", Settings);
            UpdateUiState();
            MessageBox.Show(
                Localizer.Format("ContextMenuInstalledFormat", installedPath),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UninstallContextMenu()
    {
        ContextMenuRegistrar.Uninstall();
        Settings.RegisterContextMenu = false;
        _registerContextMenuCheckBox.Checked = false;
        SettingsStore.Save(Settings);
        UpdateUiState();
        MessageBox.Show(
            Localizer.Get("ContextMenuRemoved"),
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OpenRenameDictionaryEditor()
    {
        var document = RenameDictionaryStore.Load();
        using var dialog = new RenameDictionaryEditorDialog(document.Replacements);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            document.Replacements = dialog.Entries.ToList();
            RenameDictionaryStore.Save(document);
        }
    }

    private void OpenCommonPhraseEditor()
    {
        var document = RenameDictionaryStore.Load();
        using var dialog = new StringListEditorDialog(
            Localizer.Get("DialogCommonPhrasesTitle"),
            Localizer.Get("LabelCommonPhrase"),
            document.CommonPhrases);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            document.CommonPhrases = dialog.Items.ToList();
            RenameDictionaryStore.Save(document);
        }
    }

    private void OpenTemplateEditor()
    {
        var selectedTemplateId = _defaultTemplateCombo.SelectedItem is ComboOption<string> selectedTemplate
            ? selectedTemplate.Value
            : Settings.AutoRelocationTemplateId;
        using var dialog = new AutoRelocationTemplateEditorDialog();
        dialog.ShowDialog(this);
        RefreshTemplateCombo(selectedTemplateId);
        UpdateUiState();
    }

    private void RefreshTemplateCombo(string? selectedTemplateId)
    {
        ConfigureCombo(_defaultTemplateCombo, AutoRelocationTemplateStore.LoadTemplates()
            .Select(template => new ComboOption<string>(
                $"{template.Document.DisplayName} ({template.Document.Id})",
                template.Document.Id))
            .ToArray());
        SelectComboValue(_defaultTemplateCombo, string.IsNullOrWhiteSpace(selectedTemplateId)
            ? AutoRelocationTemplateDefaults.DefaultTemplateId
            : selectedTemplateId);
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

    private static string GetComboText(ComboBox combo)
    {
        return combo.SelectedItem?.ToString() ?? "";
    }

    private static string TrimSummary(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..Math.Max(0, maxLength - 3)] + "...";
    }

    private static void SelectComboValue<T>(ComboBox combo, T value)
        where T : notnull
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboOption<T> option && EqualityComparer<T>.Default.Equals(option.Value, value))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
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
