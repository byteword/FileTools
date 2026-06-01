using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class RenameReviewDialog : Form
{
    private readonly BindingList<RenameRow> _rows = [];
    private readonly bool _applyOnOk;
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();
    private readonly DataGridView _grid = new();

    public OperationResult Result { get; private set; } = new();

    private RenameReviewDialog(IEnumerable<string> paths, FileToolsSettings settings, bool applyOnOk)
    {
        _applyOnOk = applyOnOk;
        Text = Localizer.Get("DialogRenameTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 900;
        Height = 460;
        MinimizeBox = false;
        MaximizeBox = false;

        BuildLayout(applyOnOk);
        LoadRows(paths, settings);
    }

    public static OperationResult ShowAndApply(IEnumerable<string> paths, FileToolsSettings settings)
    {
        using var dialog = new RenameReviewDialog(paths, settings, applyOnOk: true);
        return dialog.ShowDialog() == DialogResult.OK ? dialog.Result : new OperationResult();
    }

    public static bool EditPlanStep(IWin32Window owner, string path, WorkPlanStep step, FileToolsSettings settings)
    {
        using var dialog = new RenameReviewDialog([path], settings, applyOnOk: false);
        if (!string.IsNullOrWhiteSpace(step.ManualRenameFileName) && dialog._rows.Count > 0)
        {
            dialog._rows[0].SuggestedName = step.ManualRenameFileName;
        }

        if (dialog.ShowDialog(owner) != DialogResult.OK || dialog._rows.Count == 0)
        {
            return false;
        }

        step.ManualRenameFileName = WindowsFileNameSafety.MakeSafeFileName(dialog._rows[0].SuggestedName.Trim());
        return true;
    }

    public static IReadOnlyDictionary<string, string>? EditPlanSteps(
        IWin32Window owner,
        IEnumerable<string> paths,
        FileToolsSettings settings)
    {
        using var dialog = new RenameReviewDialog(paths, settings, applyOnOk: false);
        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return null;
        }

        return dialog._rows.ToDictionary(
            static row => row.Preview.OriginalPath,
            static row => WindowsFileNameSafety.MakeSafeFileName(row.SuggestedName.Trim()),
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    private void BuildLayout(bool applyOnOk)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };
        Controls.Add(panel);

        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.Dock = DockStyle.Fill;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.OriginalName),
            HeaderText = Localizer.Get("ColumnOriginalName"),
            ReadOnly = true,
            Width = 230
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.SuggestedName),
            HeaderText = Localizer.Get("ColumnSuggestedName"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.Status),
            HeaderText = Localizer.Get("ColumnRenameStatus"),
            ReadOnly = true,
            Width = 100
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.Reason),
            HeaderText = Localizer.Get("ColumnRenameReason"),
            ReadOnly = true,
            Width = 240
        });
        _grid.DataSource = _rows;
        panel.Controls.Add(_grid);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 42,
            Padding = new Padding(0, 10, 0, 0),
            WrapContents = false
        };
        panel.Controls.Add(buttons);

        _okButton.Text = applyOnOk ? Localizer.Get("ButtonApply") : "OK";
        _okButton.Width = 86;
        _okButton.Height = 28;
        _okButton.Click += (_, _) => Confirm();
        buttons.Controls.Add(_okButton);

        _cancelButton.Text = Localizer.Get("ButtonCancel");
        _cancelButton.Width = 86;
        _cancelButton.Height = 28;
        _cancelButton.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(_cancelButton);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private void LoadRows(IEnumerable<string> paths, FileToolsSettings settings)
    {
        foreach (var preview in RenameOperations.CreatePlan(paths, settings))
        {
            _rows.Add(new RenameRow(preview)
            {
                OriginalName = preview.OriginalFileName,
                SuggestedName = preview.SuggestedFileName,
                Status = preview.Status.ToString(),
                Reason = string.Join("; ", preview.Reasons.Take(4))
            });
        }
    }

    private void Confirm()
    {
        _grid.EndEdit();
        var previews = CreateEditedPreviews();
        if (previews is null)
        {
            return;
        }

        if (_applyOnOk)
        {
            Result = RenameOperations.Apply(previews);
            if (Result.HasErrors)
            {
                MessageBox.Show(
                    Result.ToUserMessage(Localizer.Get("DialogRenameTitle")),
                    FileToolsEnvironment.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private IReadOnlyList<RenamePreview>? CreateEditedPreviews()
    {
        var previews = new List<RenamePreview>();
        var targets = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

        foreach (var row in _rows)
        {
            var suggestedName = (row.SuggestedName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(suggestedName) ||
                suggestedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                ShowValidation(Localizer.Get("RenameInvalidNameMessage"));
                return null;
            }

            var safeName = WindowsFileNameSafety.MakeSafeFileName(suggestedName);
            var directory = Path.GetDirectoryName(row.Preview.OriginalPath) ?? "";
            var suggestedPath = Path.Combine(directory, safeName);
            if (!targets.Add(suggestedPath))
            {
                ShowValidation(Localizer.Get("RenameDuplicateNameMessage"));
                return null;
            }

            previews.Add(row.Preview with
            {
                SuggestedFileName = safeName,
                SuggestedPath = suggestedPath,
                Status = string.Equals(row.Preview.OriginalFileName, safeName, StringComparison.Ordinal)
                    ? RenamePreviewStatus.Unchanged
                    : RenamePreviewStatus.Ready
            });
        }

        return previews;
    }

    private static void ShowValidation(string message)
    {
        MessageBox.Show(
            message,
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private sealed class RenameRow : INotifyPropertyChanged
    {
        private string _suggestedName = "";

        public RenameRow(RenamePreview preview)
        {
            Preview = preview;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public RenamePreview Preview { get; }

        public string OriginalName { get; init; } = "";

        public string SuggestedName
        {
            get => _suggestedName;
            set
            {
                if (_suggestedName == value)
                {
                    return;
                }

                _suggestedName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SuggestedName)));
            }
        }

        public string Status { get; init; } = "";

        public string Reason { get; init; } = "";
    }
}
