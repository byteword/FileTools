using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _registerContextMenuCheckBox = new();
    private readonly CheckBox _contextMenuOpenCheckBox = new();
    private readonly CheckBox _contextMenuRenameCheckBox = new();
    private readonly CheckBox _contextMenuFolderCheckBox = new();
    private readonly CheckBox _contextMenuRelocationCheckBox = new();
    private readonly ComboBox _contextMenuLayoutCombo = new();
    private readonly ComboBox _defaultFolderOperationCombo = new();
    private readonly ComboBox _defaultTemplateCombo = new();
    private readonly CheckBox _renameReviewCheckBox = new();
    private readonly CheckBox _renameDictionaryCheckBox = new();

    public SettingsForm(FileToolsSettings settings)
    {
        Settings = settings.Clone();
        Text = Localizer.Get("SettingsFormTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 720;
        Height = 520;
        MinimumSize = new Size(640, 440);

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
        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90 };
        var cancelButton = new Button { Text = Localizer.Get("ButtonCancel"), DialogResult = DialogResult.Cancel, Width = 90 };
        okButton.Click += (_, _) => SaveSettingsFromUi();
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
        _registerContextMenuCheckBox.Width = 620;
        _registerContextMenuCheckBox.Height = 28;
        _contextMenuOpenCheckBox.Text = Localizer.Get("ToolOpenApp");
        _contextMenuRenameCheckBox.Text = ToolModeText.GetDisplayName(ToolMode.FileNameCorrection);
        _contextMenuFolderCheckBox.Text = ToolModeText.GetDisplayName(ToolMode.FolderStructure);
        _contextMenuRelocationCheckBox.Text = ToolModeText.GetDisplayName(ToolMode.AutoRelocation);

        panel.Controls.Add(_registerContextMenuCheckBox);
        panel.Controls.Add(CreateComboRow(Localizer.Get("LabelContextMenuLayout"), _contextMenuLayoutCombo));
        panel.Controls.Add(CreateGroup(Localizer.Get("GroupContextMenuTasks"),
            _contextMenuOpenCheckBox,
            _contextMenuRenameCheckBox,
            _contextMenuFolderCheckBox,
            _contextMenuRelocationCheckBox));

        var installPanel = new FlowLayoutPanel
        {
            Height = 42,
            Width = 620,
            WrapContents = false
        };
        var installButton = new Button { Text = Localizer.Get("ButtonInstallContextMenu"), Width = 150, Height = 30 };
        var uninstallButton = new Button { Text = Localizer.Get("ButtonUninstallContextMenu"), Width = 150, Height = 30 };
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

        _renameReviewCheckBox.Text = Localizer.Get("CheckRenameReviewBeforeApply");
        _renameReviewCheckBox.Width = 620;
        _renameReviewCheckBox.Height = 28;
        _renameDictionaryCheckBox.Text = Localizer.Get("CheckRenameUseDictionary");
        _renameDictionaryCheckBox.Width = 620;
        _renameDictionaryCheckBox.Height = 28;
        panel.Controls.Add(_renameReviewCheckBox);
        panel.Controls.Add(_renameDictionaryCheckBox);

        var dictionaryButton = new Button
        {
            Text = Localizer.Get("ButtonEditRenameDictionary"),
            Width = 180,
            Height = 30
        };
        dictionaryButton.Click += (_, _) => MessageBox.Show(
            Localizer.Get("FutureRenameDictionaryMessage"),
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        panel.Controls.Add(dictionaryButton);

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
        templateButton.Click += (_, _) => MessageBox.Show(
            Localizer.Get("FutureTemplateEditorMessage"),
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        panel.Controls.Add(templateButton);

        return page;
    }

    private TabPage BuildFolderTab()
    {
        var page = new TabPage(Localizer.Get("TabFolderStructure"));
        var panel = CreateStackPanel();
        page.Controls.Add(panel);

        panel.Controls.Add(CreateComboRow(Localizer.Get("LabelDefaultFolderOperation"), _defaultFolderOperationCombo));
        var label = new Label
        {
            Text = Localizer.Get("FolderOptionsFutureMessage"),
            Width = 620,
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
        _contextMenuFolderCheckBox.Checked = Settings.ContextMenuFolderStructure;
        _contextMenuRelocationCheckBox.Checked = Settings.ContextMenuAutoRelocation;
        _renameReviewCheckBox.Checked = Settings.RenameReviewBeforeApply;
        _renameDictionaryCheckBox.Checked = Settings.RenameUseDictionary;

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

        _defaultTemplateCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _defaultTemplateCombo.DataSource = AutoRelocationTemplateStore.LoadTemplates()
            .Select(template => new ComboOption<string>(
                $"{template.Document.DisplayName} ({template.Document.Id})",
                template.Document.Id))
            .ToArray();
        SelectComboValue(_defaultTemplateCombo, Settings.AutoRelocationTemplateId);
    }

    private void SaveSettingsFromUi()
    {
        Settings.RegisterContextMenu = _registerContextMenuCheckBox.Checked;
        Settings.ContextMenuOpenApp = _contextMenuOpenCheckBox.Checked;
        Settings.ContextMenuFileNameCorrection = _contextMenuRenameCheckBox.Checked;
        Settings.ContextMenuFolderStructure = _contextMenuFolderCheckBox.Checked;
        Settings.ContextMenuAutoRelocation = _contextMenuRelocationCheckBox.Checked;
        Settings.RenameReviewBeforeApply = _renameReviewCheckBox.Checked;
        Settings.RenameUseDictionary = _renameDictionaryCheckBox.Checked;

        if (_contextMenuLayoutCombo.SelectedItem is ComboOption<ContextMenuLayout> layout)
        {
            Settings.ContextMenuLayout = layout.Value;
        }

        if (_defaultFolderOperationCombo.SelectedItem is ComboOption<FolderStructureOperation> operation)
        {
            Settings.FolderStructureOperation = operation.Value;
        }

        if (_defaultTemplateCombo.SelectedItem is ComboOption<string> template)
        {
            Settings.AutoRelocationTemplateId = template.Value;
        }
    }

    private void InstallContextMenu()
    {
        try
        {
            SaveSettingsFromUi();
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

    private static void UninstallContextMenu()
    {
        ContextMenuRegistrar.Uninstall();
        MessageBox.Show(
            Localizer.Get("ContextMenuRemoved"),
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
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
        var panel = new Panel { Width = 620, Height = 42 };
        panel.Controls.Add(new Label
        {
            Text = labelText,
            Left = 0,
            Top = 8,
            Width = 180,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        });
        combo.Left = 190;
        combo.Top = 6;
        combo.Width = 360;
        panel.Controls.Add(combo);
        return panel;
    }

    private static GroupBox CreateGroup(string text, params Control[] controls)
    {
        var group = new GroupBox
        {
            Text = text,
            Width = 620,
            Height = 150,
            Padding = new Padding(12)
        };

        var top = 24;
        foreach (var control in controls)
        {
            control.Left = 12;
            control.Top = top;
            control.Width = 420;
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
    }
}
