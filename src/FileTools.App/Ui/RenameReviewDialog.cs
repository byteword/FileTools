using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class RenameReviewDialog : Form
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly BindingList<RenameRow> _rows = [];
    private readonly bool _applyOnOk;
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();
    private readonly DataGridView _grid = new();
    private readonly Label _summaryLabel = new();

    private bool _updatingRows;

    public OperationResult Result { get; private set; } = new();

    private RenameReviewDialog(IEnumerable<RenamePreview> previews, bool applyOnOk)
    {
        _applyOnOk = applyOnOk;
        Text = Localizer.Get("DialogRenameTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 940;
        Height = 500;
        MinimumSize = new Size(760, 360);
        MinimizeBox = false;

        BuildLayout(applyOnOk);
        LoadRows(previews);
        ValidateRows();
    }

    public static OperationResult ShowAndApply(IEnumerable<string> paths, FileToolsSettings settings)
    {
        var previews = RenameOperations.CreatePlan(paths, settings);
        if (settings.RenameReviewMode == RenameReviewMode.IssuesOnly &&
            !previews.Any(static preview => preview.Status is RenamePreviewStatus.NeedsReview or RenamePreviewStatus.Conflict))
        {
            return RenameOperations.Apply(previews);
        }

        using var dialog = new RenameReviewDialog(previews, applyOnOk: true);
        return dialog.ShowDialog() == DialogResult.OK ? dialog.Result : new OperationResult();
    }

    public static bool EditPlanStep(IWin32Window owner, string path, WorkPlanStep step, FileToolsSettings settings)
    {
        using var dialog = new RenameReviewDialog(RenameOperations.CreatePlan([path], settings), applyOnOk: false);
        if (!string.IsNullOrWhiteSpace(step.ManualRenameFileName) && dialog._rows.Count > 0)
        {
            dialog._rows[0].SuggestedName = step.ManualRenameFileName;
            dialog._rows[0].UserEdited = true;
            dialog.NormalizeEditedRow(dialog._rows[0]);
            dialog.ValidateRows();
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
        using var dialog = new RenameReviewDialog(RenameOperations.CreatePlan(paths, settings), applyOnOk: false);
        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return null;
        }

        return dialog._rows.ToDictionary(
            static row => row.Preview.OriginalPath,
            static row => WindowsFileNameSafety.MakeSafeFileName(row.SuggestedName.Trim()),
            PathComparer);
    }

    private void BuildLayout(bool applyOnOk)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(panel);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
        panel.Controls.Add(header, 0, 0);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.Font = new Font(Font, FontStyle.Bold);
        _summaryLabel.TextAlign = ContentAlignment.MiddleRight;
        header.Controls.Add(_summaryLabel, 1, 0);

        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.Dock = DockStyle.Fill;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.CellEndEdit += (_, args) => HandleCellEndEdit(args);
        _grid.CellToolTipTextNeeded += (_, args) => SetCellToolTip(args);
        _grid.DataBindingComplete += (_, _) => ApplyRowStyles();
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.OriginalName),
            HeaderText = Localizer.Get("ColumnOriginalName"),
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 46
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.Arrow),
            HeaderText = Localizer.Get("ColumnRenameArrow"),
            ReadOnly = true,
            Width = 34,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter
            }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.SuggestedName),
            HeaderText = Localizer.Get("ColumnSuggestedName"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 54
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.Status),
            HeaderText = Localizer.Get("ColumnRenameStatus"),
            ReadOnly = true,
            Width = 118
        });
        _grid.DataSource = _rows;
        panel.Controls.Add(_grid, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0),
            WrapContents = false
        };
        panel.Controls.Add(buttons, 0, 2);

        _okButton.Text = applyOnOk ? Localizer.Get("ButtonApply") : "OK";
        _okButton.Width = 96;
        _okButton.Height = 28;
        _okButton.Click += (_, _) => Confirm();
        buttons.Controls.Add(_okButton);

        _cancelButton.Text = Localizer.Get("ButtonCancel");
        _cancelButton.Width = 96;
        _cancelButton.Height = 28;
        _cancelButton.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(_cancelButton);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private void LoadRows(IEnumerable<RenamePreview> previews)
    {
        foreach (var preview in previews.OrderBy(GetInitialSortPriority))
        {
            _rows.Add(new RenameRow(preview)
            {
                OriginalName = preview.OriginalFileName,
                SuggestedName = preview.SuggestedFileName
            });
        }
    }

    private void HandleCellEndEdit(DataGridViewCellEventArgs args)
    {
        if (args.RowIndex < 0 || args.ColumnIndex < 0 ||
            _grid.Columns[args.ColumnIndex].DataPropertyName != nameof(RenameRow.SuggestedName) ||
            _grid.Rows[args.RowIndex].DataBoundItem is not RenameRow row)
        {
            return;
        }

        row.UserEdited = true;
        NormalizeEditedRow(row);
        ValidateRows();
    }

    private void NormalizeEditedRow(RenameRow row)
    {
        var suggestedName = row.SuggestedName.Trim();
        if (string.IsNullOrWhiteSpace(suggestedName))
        {
            row.SuggestedName = suggestedName;
            return;
        }

        var safeName = WindowsFileNameSafety.MakeSafeFileName(suggestedName);
        if (!string.Equals(suggestedName, safeName, StringComparison.Ordinal))
        {
            row.SuggestedName = safeName;
        }
    }

    private void ValidateRows()
    {
        if (_updatingRows)
        {
            return;
        }

        _updatingRows = true;
        try
        {
            var targetGroups = new Dictionary<string, List<RenameRow>>(PathComparer);
            foreach (var row in _rows)
            {
                row.BlockingError = false;
                row.ValidationMessage = "";
                row.TargetPath = "";

                var suggestedName = row.SuggestedName.Trim();
                if (string.IsNullOrWhiteSpace(suggestedName) ||
                    suggestedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    row.State = RenameRowState.Invalid;
                    row.BlockingError = true;
                    row.Status = Localizer.Get("RenameStatusInvalid");
                    row.ValidationMessage = Localizer.Get("RenameInvalidNameMessage");
                    continue;
                }

                row.State = RenameRowState.Ready;
                var safeName = WindowsFileNameSafety.MakeSafeFileName(suggestedName);
                var directory = Path.GetDirectoryName(row.Preview.OriginalPath) ?? "";
                row.TargetPath = Path.Combine(directory, safeName);
                if (!targetGroups.TryGetValue(row.TargetPath, out var group))
                {
                    group = [];
                    targetGroups.Add(row.TargetPath, group);
                }

                group.Add(row);
            }

            foreach (var row in _rows.Where(static row => row.State != RenameRowState.Invalid))
            {
                var hasDuplicateTarget = targetGroups.TryGetValue(row.TargetPath, out var group) && group.Count > 1;
                var targetExists = !PathComparer.Equals(row.Preview.OriginalPath, row.TargetPath) &&
                    (File.Exists(row.TargetPath) || Directory.Exists(row.TargetPath));

                if (hasDuplicateTarget || targetExists)
                {
                    row.State = RenameRowState.Conflict;
                    row.BlockingError = true;
                    row.Status = Localizer.Get("RenameStatusConflict");
                    row.ValidationMessage = hasDuplicateTarget
                        ? Localizer.Get("RenameDuplicateNameMessage")
                        : Localizer.Format("PlanPreviewTargetExistsFormat", row.TargetPath);
                    continue;
                }

                if (row.Preview.Status == RenamePreviewStatus.Conflict)
                {
                    row.State = row.UserEdited ? RenameRowState.Resolved : RenameRowState.Conflict;
                    row.Status = row.UserEdited
                        ? Localizer.Get("RenameStatusResolved")
                        : Localizer.Get("RenameStatusConflict");
                    continue;
                }

                if (row.Preview.Status == RenamePreviewStatus.NeedsReview && !row.UserEdited)
                {
                    row.State = RenameRowState.NeedsReview;
                    row.Status = Localizer.Get("RenameStatusNeedsReview");
                    continue;
                }

                if (PathComparer.Equals(row.Preview.OriginalPath, row.TargetPath))
                {
                    row.State = RenameRowState.Unchanged;
                    row.Status = Localizer.Get("RenameStatusUnchanged");
                    continue;
                }

                row.State = RenameRowState.Ready;
                row.Status = Localizer.Get("RenameStatusReady");
            }
        }
        finally
        {
            _updatingRows = false;
        }

        ApplyRowStyles();
        UpdateSummary();
        UpdateCommandState();
    }

    private void ApplyRowStyles()
    {
        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is not RenameRow row)
            {
                continue;
            }

            var style = gridRow.DefaultCellStyle;
            style.SelectionForeColor = SystemColors.HighlightText;
            switch (row.State)
            {
                case RenameRowState.Invalid:
                    style.BackColor = Color.FromArgb(255, 232, 232);
                    style.ForeColor = Color.FromArgb(128, 23, 23);
                    break;
                case RenameRowState.Conflict:
                    style.BackColor = row.BlockingError
                        ? Color.FromArgb(255, 232, 232)
                        : Color.FromArgb(255, 243, 205);
                    style.ForeColor = row.BlockingError
                        ? Color.FromArgb(128, 23, 23)
                        : Color.FromArgb(112, 73, 0);
                    break;
                case RenameRowState.NeedsReview:
                    style.BackColor = Color.FromArgb(255, 249, 219);
                    style.ForeColor = Color.FromArgb(86, 65, 0);
                    break;
                case RenameRowState.Resolved:
                    style.BackColor = Color.FromArgb(226, 246, 232);
                    style.ForeColor = Color.FromArgb(18, 92, 54);
                    break;
                case RenameRowState.Unchanged:
                    style.BackColor = Color.FromArgb(245, 246, 248);
                    style.ForeColor = Color.FromArgb(93, 99, 108);
                    break;
                default:
                    style.BackColor = Color.White;
                    style.ForeColor = SystemColors.ControlText;
                    break;
            }
        }
    }

    private void UpdateSummary()
    {
        var totalCount = _rows.Count;
        var changeCount = _rows.Count(static row =>
            row.State != RenameRowState.Invalid &&
            !PathComparer.Equals(row.Preview.OriginalPath, row.TargetPath));
        var reviewCount = _rows.Count(static row => row.State == RenameRowState.NeedsReview);
        var conflictCount = _rows.Count(static row => row.State == RenameRowState.Conflict);
        var resolvedCount = _rows.Count(static row => row.State == RenameRowState.Resolved);
        _summaryLabel.Text = Localizer.Format(
            "RenameSummaryFormat",
            totalCount,
            changeCount,
            reviewCount,
            conflictCount,
            resolvedCount);
    }

    private void UpdateCommandState()
    {
        _okButton.Enabled = !_rows.Any(static row => row.BlockingError);
    }

    private void SetCellToolTip(DataGridViewCellToolTipTextNeededEventArgs args)
    {
        if (args.RowIndex < 0 || args.ColumnIndex < 0 ||
            _grid.Rows[args.RowIndex].DataBoundItem is not RenameRow row)
        {
            return;
        }

        var columnName = _grid.Columns[args.ColumnIndex].DataPropertyName;
        args.ToolTipText = columnName switch
        {
            nameof(RenameRow.OriginalName) => row.Preview.OriginalPath,
            nameof(RenameRow.SuggestedName) => string.IsNullOrWhiteSpace(row.TargetPath)
                ? row.SuggestedName
                : row.TargetPath,
            nameof(RenameRow.Status) => string.IsNullOrWhiteSpace(row.ValidationMessage)
                ? row.Status
                : row.ValidationMessage,
            _ => ""
        };
    }

    private void Confirm()
    {
        _grid.EndEdit();
        foreach (var row in _rows)
        {
            NormalizeEditedRow(row);
        }

        ValidateRows();
        if (_rows.Any(static row => row.BlockingError))
        {
            ShowValidation(_rows.First(static row => row.BlockingError).ValidationMessage);
            return;
        }

        var previews = CreateEditedPreviews();
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

    private IReadOnlyList<RenamePreview> CreateEditedPreviews()
    {
        var previews = new List<RenamePreview>();
        foreach (var row in _rows)
        {
            var safeName = WindowsFileNameSafety.MakeSafeFileName(row.SuggestedName.Trim());
            var status = row.State switch
            {
                RenameRowState.Unchanged => RenamePreviewStatus.Unchanged,
                RenameRowState.NeedsReview => RenamePreviewStatus.NeedsReview,
                RenameRowState.Conflict => RenamePreviewStatus.Conflict,
                _ => RenamePreviewStatus.Ready
            };

            previews.Add(row.Preview with
            {
                SuggestedFileName = safeName,
                SuggestedPath = row.TargetPath,
                Status = status
            });
        }

        return previews;
    }

    private static int GetInitialSortPriority(RenamePreview preview)
    {
        return preview.Status switch
        {
            RenamePreviewStatus.Conflict => 0,
            RenamePreviewStatus.NeedsReview => 1,
            RenamePreviewStatus.Ready => 2,
            RenamePreviewStatus.Unchanged => 3,
            _ => 4
        };
    }

    private static void ShowValidation(string message)
    {
        MessageBox.Show(
            message,
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private enum RenameRowState
    {
        Ready,
        Unchanged,
        NeedsReview,
        Conflict,
        Resolved,
        Invalid
    }

    private sealed class RenameRow : INotifyPropertyChanged
    {
        private string _suggestedName = "";
        private string _status = "";

        public RenameRow(RenamePreview preview)
        {
            Preview = preview;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public RenamePreview Preview { get; }

        public string OriginalName { get; init; } = "";

        public string Arrow => ">";

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
                OnPropertyChanged(nameof(SuggestedName));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value)
                {
                    return;
                }

                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public RenameRowState State { get; set; } = RenameRowState.Ready;

        public bool BlockingError { get; set; }

        public bool UserEdited { get; set; }

        public string TargetPath { get; set; } = "";

        public string ValidationMessage { get; set; } = "";

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
