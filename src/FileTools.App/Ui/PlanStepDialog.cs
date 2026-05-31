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
    private readonly Label _folderOperationLabel = new();
    private readonly Label _folderMismatchLabel = new();
    private readonly Label _templateLabel = new();
    private readonly Label _manualRootLabel = new();
    private readonly Button _browseButton = new();

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
        Width = 520;
        Height = 300;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        BuildLayout(settings);
        LoadValues(settings);
    }

    private void BuildLayout(FileToolsSettings settings)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };
        Controls.Add(panel);

        _descriptionLabel.Left = 16;
        _descriptionLabel.Top = 16;
        _descriptionLabel.Width = 470;
        _descriptionLabel.Height = 44;
        _descriptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(_descriptionLabel);

        ConfigureLabel(_folderOperationLabel, Localizer.Get("LabelFolderOperation"), 16, 70);
        panel.Controls.Add(_folderOperationLabel);
        _folderOperationCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _folderOperationCombo.Left = 150;
        _folderOperationCombo.Top = 68;
        _folderOperationCombo.Width = 340;
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
        panel.Controls.Add(_folderOperationCombo);

        ConfigureLabel(_folderMismatchLabel, Localizer.Get("LabelFolderUnwrapMismatch"), 16, 106);
        panel.Controls.Add(_folderMismatchLabel);
        _folderMismatchCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _folderMismatchCombo.Left = 150;
        _folderMismatchCombo.Top = 104;
        _folderMismatchCombo.Width = 340;
        _folderMismatchCombo.DataSource = Enum.GetValues<FolderUnwrapNameMismatchMode>()
            .Select(mode => new ComboOption<FolderUnwrapNameMismatchMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray();
        panel.Controls.Add(_folderMismatchCombo);

        ConfigureLabel(_templateLabel, Localizer.Get("LabelTemplate"), 16, 70);
        panel.Controls.Add(_templateLabel);
        _templateCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateCombo.Left = 150;
        _templateCombo.Top = 68;
        _templateCombo.Width = 340;
        _templateCombo.DataSource = AutoRelocationTemplateStore.LoadTemplates()
            .Select(template => new ComboOption<string>(
                $"{template.Document.DisplayName} ({template.Document.Id})",
                template.Document.Id))
            .ToArray();
        panel.Controls.Add(_templateCombo);

        ConfigureLabel(_manualRootLabel, Localizer.Get("LabelManualTargetRoot"), 16, 106);
        panel.Controls.Add(_manualRootLabel);
        _manualRootBox.Left = 150;
        _manualRootBox.Top = 104;
        _manualRootBox.Width = 260;
        _browseButton.Text = Localizer.Get("ButtonBrowse");
        _browseButton.Left = 408;
        _browseButton.Top = 102;
        _browseButton.Width = 80;
        _browseButton.Height = 26;
        _browseButton.Click += (_, _) => BrowseManualRoot();
        panel.Controls.Add(_manualRootBox);
        panel.Controls.Add(_browseButton);

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Left = 314,
            Top = 224,
            Width = 86
        };
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Left = 404,
            Top = 224,
            Width = 86
        };
        panel.Controls.Add(okButton);
        panel.Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        okButton.Click += (_, _) => SaveValues();

        var isFolderUnwrap = _step.Kind == WorkPlanStepKind.FolderUnwrap;
        var isAutoRelocation = _step.Kind == WorkPlanStepKind.AutoRelocation;
        _folderOperationLabel.Visible = isFolderUnwrap;
        _folderOperationCombo.Visible = isFolderUnwrap;
        _folderMismatchLabel.Visible = isFolderUnwrap;
        _folderMismatchCombo.Visible = isFolderUnwrap;
        _templateLabel.Visible = isAutoRelocation;
        _templateCombo.Visible = isAutoRelocation;
        _manualRootLabel.Visible = isAutoRelocation;
        _manualRootBox.Visible = isAutoRelocation;
        _browseButton.Visible = isAutoRelocation;
        _descriptionLabel.Text = _step.Kind switch
        {
            WorkPlanStepKind.FileNameCorrection => Localizer.Get("RenameStepDescription"),
            WorkPlanStepKind.FolderWrap => Localizer.Get("FolderWrapStepDescription"),
            WorkPlanStepKind.FolderUnwrap => Localizer.Get("FolderUnwrapStepDescription"),
            WorkPlanStepKind.AutoRelocation => Localizer.Get("AutoRelocationStepDescription"),
            _ => ""
        };
    }

    private static void ConfigureLabel(Label label, string text, int left, int top)
    {
        label.Text = text;
        label.Left = left;
        label.Top = top;
        label.Width = 126;
        label.Height = 24;
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private void LoadValues(FileToolsSettings settings)
    {
        SelectComboValue(_folderOperationCombo, _step.FolderOperation);
        SelectComboValue(_folderMismatchCombo, _step.FolderUnwrapNameMismatchMode);
        SelectComboValue(_templateCombo, string.IsNullOrWhiteSpace(_step.AutoRelocationTemplateId)
            ? settings.AutoRelocationTemplateId
            : _step.AutoRelocationTemplateId);
        _manualRootBox.Text = _step.ManualTargetRootPath ?? "";
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
