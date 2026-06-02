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

    public SettingsForm(FileToolsSettings settings)
    {
        Settings = settings.Clone();
        Text = Localizer.Get("SettingsFormTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 760;
        Height = 620;
        MinimumSize = new Size(680, 500);

        BuildLayout();
        LoadSettings();
    }

    public FileToolsSettings Settings { get; private set; }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            RowCount = 2,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildContextMenuTab());
        tabs.TabPages.Add(BuildRenameTab());
        tabs.TabPages.Add(BuildRelocationTab());
        tabs.TabPages.Add(BuildFolderTab());
        root.Controls.Add(tabs, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var okButton = new Button { Text = "OK", Width = 90 };
        var cancelButton = new Button { Text = Localizer.Get("ButtonCancel"), DialogResult = DialogResult.Cancel, Width = 90 };
        okButton.Click += (_, _) => SaveAndClose();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);
        root.Controls.Add(buttons, 0, 1);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private TabPage BuildContextMenuTab()
    {
        var page = new TabPage(Localizer.Get("TabContextMenu"));
        var panel = CreateStackPanel();
        page.Controls.Add(panel);

        _registerContextMenuCheckBox.Text = Localizer.Get("CheckRegisterContextMenu");
        _registerContextMenuCheckBox.Width = 660;
        _registerContextMenuCheckBox.Height = 28;
        _contextMenuOpenCheckBox.Text = Localizer.Get("ToolOpenApp");
        _contextMenuRenameCheckBox.Text = ToolModeText.GetDisplayName(ToolMode.FileNameCorrection);
        _contextMenuFolderWrapCheckBox.Text = ToolModeText.GetDisplayName(FolderStructureOperation.WrapFiles);
        _contextMenuFolderUnwrapSameNameCheckBox.Text = ToolModeText.GetDisplayName(FolderStructureOperation.UnwrapSameNameSingleFile);
        _contextMenuFolderUnwrapSingleFileCheckBox.Text = ToolModeText.GetDisplayName(FolderStructureOperation.UnwrapSingleFileFolder);
        _contextMenuFolderMoveInnerFilesCheckBox.Text = ToolModeText.GetDisplayName(FolderStructureOperation.MoveInnerFilesUp);
        _contextMenuRelocationCurrentCheckBox.Text = Localizer.Get("ContextCommandAutoRelocationCurrentFolder");
        _contextMenuRelocationChooseTargetCheckBox.Text = Localizer.Get("ContextCommandAutoRelocationChooseTarget");

        panel.Controls.Add(_registerContextMenuCheckBox);
        panel.Controls.Add(CreateComboRow(Localizer.Get("LabelContextMenuLayout"), _contextMenuLayoutCombo));
        panel.Controls.Add(CreateGroup(Localizer.Get("GroupContextMenuTasks"),
            _contextMenuRenameCheckBox));
        panel.Controls.Add(CreateGroup(Localizer.Get("GroupFolderStructure"),
            _contextMenuFolderWrapCheckBox,
            _contextMenuFolderUnwrapSameNameCheckBox,
            _contextMenuFolderUnwrapSingleFileCheckBox,
            _contextMenuFolderMoveInnerFilesCheckBox));
        panel.Controls.Add(CreateGroup(Localizer.Get("GroupAutoRelocationContextMenu"),
            _contextMenuRelocationCurrentCheckBox,
            _contextMenuRelocationChooseTargetCheckBox));
        panel.Controls.Add(CreateGroup(Localizer.Get("GroupApplicationContextMenu"),
            _contextMenuOpenCheckBox));

        var installPanel = new FlowLayoutPanel
        {
            Height = 42,
            Width = 660,
            WrapContents = false
        };
        var installButton = new Button { Text = Localizer.Get("ButtonInstallContextMenu"), Width = 160, Height = 30 };
        var uninstallButton = new Button { Text = Localizer.Get("ButtonUninstallContextMenu"), Width = 160, Height = 30 };
        installButton.Click += (_, _) => InstallContextMenu();
        uninstallButton.Click += (_, _) => UninstallContextMenu();
        installPanel.Controls.Add(installButton);
        installPanel.Controls.Add(uninstallButton);
        panel.Controls.Add(installPanel);

        return page;
    }

    private TabPage BuildRenameTab()
    {
        var page = new TabPage(Localizer.Get("TabRename"));
        var panel = CreateStackPanel();
        page.Controls.Add(panel);

        _renameDictionaryCheckBox.Text = Localizer.Get("CheckRenameUseDictionary");
        _renameDictionaryCheckBox.Width = 660;
        _renameDictionaryCheckBox.Height = 28;
        panel.Controls.Add(CreateComboRow(Localizer.Get("LabelRenameReviewMode"), _renameReviewModeCombo));
        panel.Controls.Add(_renameDictionaryCheckBox);

        var buttonPanel = new FlowLayoutPanel
        {
            Height = 42,
            Width = 660,
            WrapContents = false
        };
        var dictionaryButton = new Button
        {
            Text = Localizer.Get("ButtonEditRenameDictionary"),
            Width = 200,
            Height = 30
        };
        var phraseButton = new Button
        {
            Text = Localizer.Get("ButtonEditCommonPhrases"),
            Width = 220,
            Height = 30
        };
        dictionaryButton.Click += (_, _) => OpenRenameDictionaryEditor();
        phraseButton.Click += (_, _) => OpenCommonPhraseEditor();
        buttonPanel.Controls.Add(dictionaryButton);
        buttonPanel.Controls.Add(phraseButton);
        panel.Controls.Add(buttonPanel);

        return page;
    }

    private TabPage BuildRelocationTab()
    {
        var page = new TabPage(Localizer.Get("TabAutoRelocation"));
        var panel = CreateStackPanel();
        page.Controls.Add(panel);

        panel.Controls.Add(CreateComboRow(Localizer.Get("LabelDefaultTemplate"), _defaultTemplateCombo));
        var templateButton = new Button
        {
            Text = Localizer.Get("ButtonEditTemplates"),
            Width = 180,
            Height = 30
        };
        templateButton.Click += (_, _) => OpenTemplateEditor();
        panel.Controls.Add(templateButton);

        return page;
    }

    private TabPage BuildFolderTab()
    {
        var page = new TabPage(Localizer.Get("TabFolderStructure"));
        var panel = CreateStackPanel();
        page.Controls.Add(panel);

        panel.Controls.Add(CreateComboRow(Localizer.Get("LabelDefaultFolderOperation"), _defaultFolderOperationCombo));
        panel.Controls.Add(CreateComboRow(Localizer.Get("LabelFolderUnwrapMismatch"), _folderMismatchCombo));
        var label = new Label
        {
            Text = Localizer.Get("FolderOptionsFutureMessage"),
            Width = 660,
            Height = 48,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label);

        return page;
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

        _renameReviewModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _renameReviewModeCombo.DataSource = Enum.GetValues<RenameReviewMode>()
            .Select(mode => new ComboOption<RenameReviewMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray();
        SelectComboValue(_renameReviewModeCombo, Settings.RenameReviewMode);

        _contextMenuLayoutCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _contextMenuLayoutCombo.DataSource = Enum.GetValues<ContextMenuLayout>()
            .Select(layout => new ComboOption<ContextMenuLayout>(ToolModeText.GetDisplayName(layout), layout))
            .ToArray();
        SelectComboValue(_contextMenuLayoutCombo, Settings.ContextMenuLayout);

        _defaultFolderOperationCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _defaultFolderOperationCombo.DataSource = Enum.GetValues<FolderStructureOperation>()
            .Select(operation => new ComboOption<FolderStructureOperation>(ToolModeText.GetDisplayName(operation), operation))
            .ToArray();
        SelectComboValue(_defaultFolderOperationCombo, Settings.FolderStructureOperation);

        _folderMismatchCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _folderMismatchCombo.DataSource = Enum.GetValues<FolderUnwrapNameMismatchMode>()
            .Select(mode => new ComboOption<FolderUnwrapNameMismatchMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray();
        SelectComboValue(_folderMismatchCombo, Settings.FolderUnwrapNameMismatchMode);

        RefreshTemplateCombo(Settings.AutoRelocationTemplateId);
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
        using var dialog = new AutoRelocationTemplateEditorDialog();
        dialog.ShowDialog(this);
        RefreshTemplateCombo(Settings.AutoRelocationTemplateId);
    }

    private void RefreshTemplateCombo(string? selectedTemplateId)
    {
        _defaultTemplateCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _defaultTemplateCombo.DataSource = AutoRelocationTemplateStore.LoadTemplates()
            .Select(template => new ComboOption<string>(
                $"{template.Document.DisplayName} ({template.Document.Id})",
                template.Document.Id))
            .ToArray();
        SelectComboValue(_defaultTemplateCombo, string.IsNullOrWhiteSpace(selectedTemplateId)
            ? AutoRelocationTemplateDefaults.DefaultTemplateId
            : selectedTemplateId);
    }

    private static FlowLayoutPanel CreateStackPanel()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
    }

    private static Control CreateComboRow(string labelText, ComboBox combo)
    {
        var panel = new Panel { Width = 660, Height = 42 };
        panel.Controls.Add(new Label
        {
            Text = labelText,
            Left = 0,
            Top = 8,
            Width = 200,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        });
        combo.Left = 210;
        combo.Top = 6;
        combo.Width = 390;
        panel.Controls.Add(combo);
        return panel;
    }

    private static GroupBox CreateGroup(string text, params Control[] controls)
    {
        var group = new GroupBox
        {
            Text = text,
            Width = 660,
            Height = 36 + controls.Length * 28,
            Padding = new Padding(12)
        };

        var top = 24;
        foreach (var control in controls)
        {
            control.Left = 12;
            control.Top = top;
            control.Width = 560;
            control.Height = 24;
            group.Controls.Add(control);
            top += 28;
        }

        return group;
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
}
