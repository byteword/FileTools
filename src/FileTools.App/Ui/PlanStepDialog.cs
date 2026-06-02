using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class PlanStepDialog : Form
{
    private readonly WorkPlanStep _step;
    private readonly ComboBox _folderOperationCombo = new();
    private readonly ComboBox _folderMismatchCombo = new();
    private readonly ComboBox _templateCombo = new();
    private readonly TextBox _manualRootBox = new();
    private readonly Label _descriptionLabel = new();
    private readonly Button _browseButton = new();
    private readonly ToolTip _toolTip = new();

    public PlanStepDialog(WorkPlanStep step, FileToolsSettings settings)
    {
        _step = step;
        Text = step.Kind switch
        {
            WorkPlanStepKind.FileNameCorrection => Localizer.Get("DialogRenameTitle"),
            WorkPlanStepKind.FolderWrap => Localizer.Get("DialogFolderWrapTitle"),
            WorkPlanStepKind.FolderUnwrap => Localizer.Get("DialogFolderUnwrapTitle"),
            WorkPlanStepKind.AutoRelocation => Localizer.Get("DialogAutoRelocationTitle"),
            _ => FileToolsEnvironment.AppName
        };
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(640, 320);
        MinimumSize = new Size(560, 260);
        MinimizeBox = false;
        MaximizeBox = false;

        BuildLayout(settings);
        LoadValues(settings);
    }

    private void BuildLayout(FileToolsSettings settings)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        _descriptionLabel.Dock = DockStyle.Fill;
        _descriptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(_descriptionLabel, 0, 0);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0
        };
        root.Controls.Add(fields, 0, 1);

        _folderOperationCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _folderOperationCombo.DataSource = new[]
        {
            new ComboOption<FolderStructureOperation>(
                ToolModeText.GetDisplayName(FolderStructureOperation.UnwrapSameNameSingleFile),
                FolderStructureOperation.UnwrapSameNameSingleFile),
            new ComboOption<FolderStructureOperation>(
                ToolModeText.GetDisplayName(FolderStructureOperation.UnwrapSingleFileFolder),
                FolderStructureOperation.UnwrapSingleFileFolder),
            new ComboOption<FolderStructureOperation>(
                ToolModeText.GetDisplayName(FolderStructureOperation.MoveInnerFilesUp),
                FolderStructureOperation.MoveInnerFilesUp)
        };

        _folderMismatchCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _folderMismatchCombo.DataSource = Enum.GetValues<FolderUnwrapNameMismatchMode>()
            .Select(mode => new ComboOption<FolderUnwrapNameMismatchMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray();

        _templateCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateCombo.DataSource = AutoRelocationTemplateStore.LoadTemplates()
            .Select(template => new ComboOption<string>(
                $"{template.Document.DisplayName} ({template.Document.Id})",
                template.Document.Id))
            .ToArray();
        _templateCombo.SelectedIndexChanged += (_, _) => UpdateToolTips();

        _manualRootBox.TextChanged += (_, _) => UpdateToolTips();
        _browseButton.Text = Localizer.Get("ButtonBrowse");
        _browseButton.Width = 94;
        _browseButton.Click += (_, _) => BrowseManualRoot();

        if (_step.Kind == WorkPlanStepKind.FolderUnwrap)
        {
            AddFieldRow(fields, Localizer.Get("LabelFolderOperation"), _folderOperationCombo);
            AddFieldRow(fields, Localizer.Get("LabelFolderUnwrapMismatch"), _folderMismatchCombo);
        }
        else if (_step.Kind == WorkPlanStepKind.AutoRelocation)
        {
            AddFieldRow(fields, Localizer.Get("LabelTemplate"), _templateCombo);
            AddBrowseRow(fields, Localizer.Get("LabelManualTargetRoot"), _manualRootBox, _browseButton);
        }

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        root.Controls.Add(buttons, 0, 2);

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Width = 90,
            Height = 28
        };
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 90,
            Height = 28
        };
        okButton.Click += (_, _) => SaveValues();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        _descriptionLabel.Text = _step.Kind switch
        {
            WorkPlanStepKind.FileNameCorrection => Localizer.Get("RenameStepDescription"),
            WorkPlanStepKind.FolderWrap => Localizer.Get("FolderWrapStepDescription"),
            WorkPlanStepKind.FolderUnwrap => Localizer.Get("FolderUnwrapStepDescription"),
            WorkPlanStepKind.AutoRelocation => Localizer.Get("AutoRelocationStepDescription"),
            _ => ""
        };
    }

    private static void AddFieldRow(TableLayoutPanel parent, string labelText, Control editor)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(CreateLabel(labelText), 0, 0);
        editor.Dock = DockStyle.Fill;
        row.Controls.Add(editor, 1, 0);
        AddRow(parent, row);
    }

    private static void AddBrowseRow(TableLayoutPanel parent, string labelText, TextBox textBox, Button button)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
        row.Controls.Add(CreateLabel(labelText), 0, 0);
        textBox.Dock = DockStyle.Fill;
        button.Dock = DockStyle.Fill;
        row.Controls.Add(textBox, 1, 0);
        row.Controls.Add(button, 2, 0);
        AddRow(parent, row);
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static void AddRow(TableLayoutPanel parent, Control row)
    {
        var index = parent.RowCount++;
        parent.RowStyles.Add(new RowStyle(SizeType.Absolute, row.Height + row.Margin.Vertical));
        parent.Controls.Add(row, 0, index);
    }

    private void LoadValues(FileToolsSettings settings)
    {
        SelectComboValue(_folderOperationCombo, _step.FolderOperation);
        SelectComboValue(_folderMismatchCombo, _step.FolderUnwrapNameMismatchMode);
        SelectComboValue(_templateCombo, string.IsNullOrWhiteSpace(_step.AutoRelocationTemplateId)
            ? settings.AutoRelocationTemplateId
            : _step.AutoRelocationTemplateId);
        _manualRootBox.Text = _step.ManualTargetRootPath ?? "";
        UpdateToolTips();
    }

    private void SaveValues()
    {
        if (_folderOperationCombo.SelectedItem is ComboOption<FolderStructureOperation> folderOperation)
        {
            _step.FolderOperation = folderOperation.Value;
        }

        if (_folderMismatchCombo.SelectedItem is ComboOption<FolderUnwrapNameMismatchMode> mismatchMode)
        {
            _step.FolderUnwrapNameMismatchMode = mismatchMode.Value;
        }

        if (_templateCombo.SelectedItem is ComboOption<string> template)
        {
            _step.AutoRelocationTemplateId = template.Value;
        }

        _step.ManualTargetRootPath = string.IsNullOrWhiteSpace(_manualRootBox.Text)
            ? null
            : _manualRootBox.Text.Trim();
    }

    private void BrowseManualRoot()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = Localizer.Get("ManualTargetRootDialogDescription"),
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _manualRootBox.Text = dialog.SelectedPath;
        }
    }

    private void UpdateToolTips()
    {
        _toolTip.SetToolTip(_templateCombo, _templateCombo.SelectedItem?.ToString() ?? "");
        _toolTip.SetToolTip(_manualRootBox, _manualRootBox.Text);
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
