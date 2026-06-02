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
    private readonly Label _selectedOriginalLabel = new();
    private readonly ComboBox _candidateCombo = new();
    private readonly TextBox _selectedNameTextBox = new();
    private readonly Button _autoButton = new();
    private readonly Button _originalButton = new();
    private readonly Button _skipButton = new();
    private bool _updatingSelection;
    private bool _selectionInitialized;

    public OperationResult Result { get; private set; } = new();

    private RenameReviewDialog(IEnumerable<string> paths, FileToolsSettings settings, bool applyOnOk)
    {
        _applyOnOk = applyOnOk;
        Text = Localizer.Get("DialogRenameTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 900;
        Height = 500;
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
            dialog.UpdateSelectionEditor();
        }

        if (dialog.ShowDialog(owner) != DialogResult.OK || dialog._rows.Count == 0)
        {
            return false;
        }

        var row = dialog._rows[0];
        step.ManualRenameFileName = row.IsSkipped
            ? row.OriginalName
            : WindowsFileNameSafety.MakeSafeFileName(row.SuggestedName.Trim());
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

        return dialog._rows
            .Where(static row => !row.IsSkipped)
            .Where(static row => !string.Equals(row.OriginalName, row.SuggestedName, StringComparison.Ordinal))
            .ToDictionary(
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
        _grid.MultiSelect = false;
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
        _grid.SelectionChanged += (_, _) => UpdateSelectionEditor();
        _grid.CellEndEdit += (_, _) => UpdateSelectionEditor();
        _grid.DataBindingComplete += (_, _) => SelectFirstRowIfAvailable();

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        content.Controls.Add(_grid, 0, 0);
        content.Controls.Add(BuildEditorPanel(), 1, 0);
        panel.Controls.Add(content);

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

    private Control BuildEditorPanel()
    {
        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 0, 0, 0),
            RowCount = 9,
            ColumnCount = 1
        };
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        editor.Controls.Add(CreateEditorLabel(Localizer.Get("LabelRenameSelectedItem")), 0, 0);
        _selectedOriginalLabel.AutoEllipsis = true;
        _selectedOriginalLabel.Height = 34;
        _selectedOriginalLabel.Dock = DockStyle.Fill;
        editor.Controls.Add(_selectedOriginalLabel, 0, 1);

        editor.Controls.Add(CreateEditorLabel(Localizer.Get("LabelRenameCandidates")), 0, 3);
        _candidateCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _candidateCombo.Dock = DockStyle.Top;
        _candidateCombo.SelectedIndexChanged += (_, _) => ApplySelectedCandidate();
        editor.Controls.Add(_candidateCombo, 0, 4);

        editor.Controls.Add(CreateEditorLabel(Localizer.Get("LabelRenameNewName")), 0, 6);
        _selectedNameTextBox.Dock = DockStyle.Top;
        _selectedNameTextBox.TextChanged += (_, _) => ApplyTypedName();
        editor.Controls.Add(_selectedNameTextBox, 0, 7);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Height = 72,
            Padding = new Padding(0, 10, 0, 0),
            WrapContents = true
        };
        _autoButton.Text = Localizer.Get("ButtonRenameUseAuto");
        _autoButton.Width = 92;
        _autoButton.Height = 28;
        _autoButton.Click += (_, _) => SetSelectedName(GetSelectedRow()?.Preview.SuggestedFileName);
        buttonPanel.Controls.Add(_autoButton);

        _originalButton.Text = Localizer.Get("ButtonRenameKeepOriginal");
        _originalButton.Width = 92;
        _originalButton.Height = 28;
        _originalButton.Click += (_, _) => SetSelectedName(GetSelectedRow()?.OriginalName);
        buttonPanel.Controls.Add(_originalButton);

        _skipButton.Text = Localizer.Get("ButtonRenameSkip");
        _skipButton.Width = 92;
        _skipButton.Height = 28;
        _skipButton.Click += (_, _) => SkipSelectedRow();
        buttonPanel.Controls.Add(_skipButton);
        editor.Controls.Add(buttonPanel, 0, 8);

        return editor;
    }

    private static Label CreateEditorLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold)
        };
    }

    private void LoadRows(IEnumerable<string> paths, FileToolsSettings settings)
    {
        foreach (var preview in RenameOperations.CreatePlan(paths, settings))
        {
            _rows.Add(new RenameRow(preview)
            {
                OriginalName = preview.OriginalFileName,
                SuggestedName = preview.SuggestedFileName,
                Status = GetStatusText(preview.Status),
                Reason = string.Join("; ", preview.Reasons.Take(4))
            });
        }

        SelectFirstRowIfAvailable();
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
            if (row.IsSkipped)
            {
                previews.Add(row.Preview with
                {
                    SuggestedFileName = row.OriginalName,
                    SuggestedPath = row.Preview.OriginalPath,
                    Status = RenamePreviewStatus.Skipped
                });
                continue;
            }

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

            if ((File.Exists(suggestedPath) || Directory.Exists(suggestedPath)) &&
                !string.Equals(suggestedPath, row.Preview.OriginalPath, StringComparison.OrdinalIgnoreCase))
            {
                ShowValidation(Localizer.Get("RenameTargetExistsMessage"));
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

    private RenameRow? GetSelectedRow()
    {
        if (_grid.CurrentRow?.DataBoundItem is RenameRow current)
        {
            return current;
        }

        return _grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].DataBoundItem is RenameRow selected
            ? selected
            : null;
    }

    private void SelectFirstRowIfAvailable()
    {
        if (_selectionInitialized)
        {
            return;
        }

        if (_grid.Rows.Count == 0)
        {
            UpdateSelectionEditor();
            return;
        }

        _selectionInitialized = true;
        _grid.ClearSelection();
        var firstRow = _grid.Rows[0];
        firstRow.Selected = true;

        for (var columnIndex = 0; columnIndex < _grid.Columns.Count; columnIndex++)
        {
            if (!_grid.Columns[columnIndex].Visible)
            {
                continue;
            }

            _grid.CurrentCell = firstRow.Cells[columnIndex];
            break;
        }

        UpdateSelectionEditor();
    }

    private void UpdateSelectionEditor()
    {
        if (_updatingSelection)
        {
            return;
        }

        _updatingSelection = true;
        try
        {
            var row = GetSelectedRow();
            var hasRow = row is not null;
            _selectedOriginalLabel.Text = row?.OriginalName ?? "";
            _selectedNameTextBox.Enabled = hasRow;
            _candidateCombo.Enabled = hasRow;
            _autoButton.Enabled = hasRow;
            _originalButton.Enabled = hasRow;
            _skipButton.Enabled = hasRow;
            _candidateCombo.Items.Clear();

            if (row is null)
            {
                _selectedNameTextBox.Text = "";
                return;
            }

            foreach (var option in CreateCandidateOptions(row.Preview))
            {
                _candidateCombo.Items.Add(option);
            }

            _selectedNameTextBox.Text = row.SuggestedName;
            SelectCandidateByFileName(row.SuggestedName);
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void ApplySelectedCandidate()
    {
        if (_updatingSelection ||
            GetSelectedRow() is null ||
            _candidateCombo.SelectedItem is not RenameCandidateOption option)
        {
            return;
        }

        SetSelectedName(option.FileName);
    }

    private void ApplyTypedName()
    {
        if (_updatingSelection || GetSelectedRow() is not { } row)
        {
            return;
        }

        row.SetSuggestedName(_selectedNameTextBox.Text);
        SelectCandidateByFileName(row.SuggestedName);
        _grid.Refresh();
    }

    private void SetSelectedName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || GetSelectedRow() is not { } row)
        {
            return;
        }

        _updatingSelection = true;
        try
        {
            row.SetSuggestedName(fileName);
            _selectedNameTextBox.Text = row.SuggestedName;
            SelectCandidateByFileName(row.SuggestedName);
            _grid.Refresh();
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void SkipSelectedRow()
    {
        if (GetSelectedRow() is not { } row)
        {
            return;
        }

        row.SetSkipped();
        _grid.Refresh();
    }

    private void SelectCandidateByFileName(string fileName)
    {
        for (var index = 0; index < _candidateCombo.Items.Count; index++)
        {
            if (_candidateCombo.Items[index] is RenameCandidateOption option &&
                string.Equals(option.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                _candidateCombo.SelectedIndex = index;
                return;
            }
        }

        _candidateCombo.SelectedIndex = -1;
    }

    private static IReadOnlyList<RenameCandidateOption> CreateCandidateOptions(RenamePreview preview)
    {
        var options = new List<RenameCandidateOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in preview.Candidates)
        {
            var fileName = NormalizeCandidateFileName(preview, candidate.Value);
            Add(fileName, GetCandidateSourceText(candidate.Reason));
        }

        Add(preview.SuggestedFileName, Localizer.Get("RenameCandidateAuto"));
        Add(preview.OriginalFileName, Localizer.Get("RenameCandidateOriginal"));
        return options;

        void Add(string fileName, string source)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !seen.Add(fileName))
            {
                return;
            }

            options.Add(new RenameCandidateOption(source, fileName));
        }
    }

    private static string GetCandidateSourceText(string reason)
    {
        return reason switch
        {
            "자동 교정 결과" => Localizer.Get("RenameCandidateAuto"),
            "원본 이름" => Localizer.Get("RenameCandidateOriginal"),
            _ => reason
        };
    }

    private static string NormalizeCandidateFileName(RenamePreview preview, string value)
    {
        var fileName = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "";
        }

        var extension = preview.Parts.Extension;
        if (string.IsNullOrWhiteSpace(extension) ||
            string.Equals(Path.GetExtension(fileName), extension, StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        return fileName + extension;
    }

    private static string GetStatusText(RenamePreviewStatus status)
    {
        return status switch
        {
            RenamePreviewStatus.Unchanged => Localizer.Get("RenameStatusUnchanged"),
            RenamePreviewStatus.Ready => Localizer.Get("RenameStatusReady"),
            RenamePreviewStatus.NeedsReview => Localizer.Get("RenameStatusNeedsReview"),
            RenamePreviewStatus.Conflict => Localizer.Get("RenameStatusConflict"),
            RenamePreviewStatus.Skipped => Localizer.Get("RenameStatusSkipped"),
            _ => status.ToString()
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

    private sealed class RenameRow : INotifyPropertyChanged
    {
        private string _suggestedName = "";
        private bool _isSkipped;
        private string _status = "";

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
                IsSkipped = false;
                RefreshStatus();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SuggestedName)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        public bool IsSkipped
        {
            get => _isSkipped;
            private set
            {
                if (_isSkipped == value)
                {
                    return;
                }

                _isSkipped = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSkipped)));
            }
        }

        public string Reason { get; init; } = "";

        public void SetSuggestedName(string fileName)
        {
            SuggestedName = fileName;
        }

        public void SetSkipped()
        {
            IsSkipped = true;
            Status = GetStatusText(RenamePreviewStatus.Skipped);
        }

        private void RefreshStatus()
        {
            if (IsSkipped)
            {
                Status = GetStatusText(RenamePreviewStatus.Skipped);
                return;
            }

            Status = string.Equals(OriginalName, SuggestedName, StringComparison.OrdinalIgnoreCase)
                ? GetStatusText(RenamePreviewStatus.Unchanged)
                : GetStatusText(RenamePreviewStatus.Ready);
        }
    }

    private sealed record RenameCandidateOption(string Source, string FileName)
    {
        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Source) ? FileName : $"{Source}: {FileName}";
        }
    }
}
