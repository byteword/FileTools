using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class PlanStepDialog : Form
{
    private readonly WorkPlanStep _step;

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
