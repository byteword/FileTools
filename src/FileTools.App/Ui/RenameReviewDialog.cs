using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class RenameReviewDialog : Form
{
    private const int CommonPhraseCollapsedRowHeight = 72;
    private const int TokenButtonMinWidth = 64;
    private const int TokenButtonMaxWidth = 180;
    private const int TokenButtonHeight = 28;
    private const int TokenButtonRightMargin = 6;
    private const int TokenButtonBottomMargin = 6;

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly BindingList<RenameRow> _rows = [];
    private readonly string[] _commonPhrases;
    private readonly bool _applyOnOk;
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _nextIssueButton = new();
    private readonly Button _skipButton = new();
    private readonly Button _useOriginalButton = new();
    private readonly Button _useAutomaticButton = new();
    private readonly DataGridView _grid = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _validationLabel = new();
    private readonly TextBox _originalNameBox = new();
    private readonly TextBox _suggestedNameBox = new();
    private readonly TextBox _titleBox = new();
    private readonly TextBox _episodeBox = new();
    private readonly TextBox _authorBox = new();
    private readonly TextBox _tagsBox = new();
    private readonly TextBox _extensionBox = new();
    private readonly FlowLayoutPanel _tokenPanel = new();
    private readonly FlowLayoutPanel _commonPhrasePanel = new();
    private readonly ToolTip _toolTip = new();

    private TableLayoutPanel? _selectedEditorPanel;
    private RenameRow? _selectedRow;
    private TextBox? _activeTextBox;
    private bool _commonPhrasesExpanded;
    private bool _updatingRows;
    private bool _updatingEditor;

    public OperationResult Result { get; private set; } = new();

    private RenameReviewDialog(IEnumerable<RenamePreview> previews, bool applyOnOk)
    {
        _applyOnOk = applyOnOk;
        _commonPhrases = LoadCommonPhrases();

        Text = Localizer.Get("DialogRenameTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 1180;
        Height = 680;
        MinimumSize = new Size(960, 540);
        MinimizeBox = false;

        BuildLayout(applyOnOk);
        LoadRows(previews);
        ValidateRows();
        SelectInitialRow();
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
            var row = dialog._rows[0];
            row.SuggestedName = step.ManualRenameFileName;
            row.UserEdited = true;
            dialog.NormalizeEditedRow(row);
            dialog.ValidateRows();
            dialog.SelectRow(row);
        }

        if (dialog.ShowDialog(owner) != DialogResult.OK || dialog._rows.Count == 0)
        {
            return false;
        }

        var editedRow = dialog._rows[0];
        step.ManualRenameFileName = editedRow.IsSkipped
            ? editedRow.OriginalName
            : WindowsFileNameSafety.MakeSafeFileName(editedRow.SuggestedName.Trim());
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

        return dialog._rows
            .Where(static row => !row.IsSkipped)
            .Where(static row => !PathComparer.Equals(row.Preview.OriginalPath, row.TargetPath))
            .ToDictionary(
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
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(panel);

        panel.Controls.Add(BuildHeader(), 0, 0);
        panel.Controls.Add(BuildBody(), 0, 1);
        panel.Controls.Add(BuildButtons(applyOnOk), 0, 2);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("DialogRenameTitle"),
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(titleLabel, 0, 0);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.Font = new Font(Font, FontStyle.Bold);
        _summaryLabel.TextAlign = ContentAlignment.MiddleRight;
        header.Controls.Add(_summaryLabel, 1, 0);
        return header;
    }

    private Control BuildBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Controls.Add(BuildRowList(), 0, 0);
        body.Controls.Add(BuildSelectedEditor(), 1, 0);
        return body;
    }

    private Control BuildRowList()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0, 0, 10, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameEditorItems"),
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label, 0, 0);

        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.CellToolTipTextNeeded += (_, args) => SetCellToolTip(args);
        _grid.DataBindingComplete += (_, _) => ApplyRowStyles();
        _grid.SelectionChanged += (_, _) => SyncEditorFromSelection();
        _grid.CellDoubleClick += (_, _) => _suggestedNameBox.Focus();
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.Status),
            HeaderText = Localizer.Get("ColumnRenameStatus"),
            Width = 92
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.OriginalName),
            HeaderText = Localizer.Get("ColumnOriginalName"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 48
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.SuggestedName),
            HeaderText = Localizer.Get("ColumnSuggestedName"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 52
        });
        _grid.DataSource = _rows;
        panel.Controls.Add(_grid, 0, 1);
        return panel;
    }

    private Control BuildSelectedEditor()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(2, 0, 0, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, CommonPhraseCollapsedRowHeight));
        _selectedEditorPanel = panel;

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameEditorSelectedItem"),
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(BuildNameComparison(), 0, 1);
        panel.Controls.Add(BuildPartsEditor(), 0, 2);
        panel.Controls.Add(BuildTokenPanel(), 0, 3);
        panel.Controls.Add(BuildCommonPhrasePanel(), 0, 4);
        return panel;
    }

    private Control BuildNameComparison()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        panel.Controls.Add(CreateFieldLabel(Localizer.Get("ColumnOriginalName")), 0, 0);
        _originalNameBox.Dock = DockStyle.Fill;
        _originalNameBox.ReadOnly = true;
        panel.Controls.Add(_originalNameBox, 1, 0);

        _useOriginalButton.Dock = DockStyle.Fill;
        _useOriginalButton.Text = Localizer.Get("ButtonUseOriginalName");
        _useOriginalButton.Click += (_, _) => UseOriginalName();
        panel.Controls.Add(_useOriginalButton, 2, 0);

        panel.Controls.Add(CreateFieldLabel(Localizer.Get("ColumnSuggestedName")), 0, 1);
        _suggestedNameBox.Dock = DockStyle.Fill;
        _suggestedNameBox.TextChanged += (_, _) => HandleSuggestedNameChanged();
        _suggestedNameBox.Leave += (_, _) => NormalizeSelectedRow();
        RegisterEditableTextBox(_suggestedNameBox);
        panel.Controls.Add(_suggestedNameBox, 1, 1);

        _useAutomaticButton.Dock = DockStyle.Fill;
        _useAutomaticButton.Text = Localizer.Get("ButtonUseAutomaticName");
        _useAutomaticButton.Click += (_, _) => UseAutomaticName();
        panel.Controls.Add(_useAutomaticButton, 2, 1);

        _validationLabel.Dock = DockStyle.Fill;
        _validationLabel.Padding = new Padding(4, 4, 4, 0);
        _validationLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(_validationLabel, 1, 2);
        panel.SetColumnSpan(_validationLabel, 2);
        return panel;
    }

    private Control BuildPartsEditor()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameEditorExtractedParts"),
            Padding = new Padding(10, 8, 10, 10)
        };

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        panel.Controls.Add(CreateSmallLabel(Localizer.Get("RenameEditorTitle")), 0, 0);
        panel.Controls.Add(CreateSmallLabel(Localizer.Get("RenameEditorEpisode")), 1, 0);
        panel.Controls.Add(CreateSmallLabel(Localizer.Get("RenameEditorAuthor")), 2, 0);
        panel.Controls.Add(CreateSmallLabel(Localizer.Get("RenameEditorExtension")), 3, 0);

        _titleBox.Dock = DockStyle.Fill;
        _episodeBox.Dock = DockStyle.Fill;
        _authorBox.Dock = DockStyle.Fill;
        _extensionBox.Dock = DockStyle.Fill;
        _extensionBox.ReadOnly = true;
        RegisterPartEditor(_titleBox);
        RegisterPartEditor(_episodeBox);
        RegisterPartEditor(_authorBox);
        panel.Controls.Add(_titleBox, 0, 1);
        panel.Controls.Add(_episodeBox, 1, 1);
        panel.Controls.Add(_authorBox, 2, 1);
        panel.Controls.Add(_extensionBox, 3, 1);

        panel.Controls.Add(CreateSmallLabel(Localizer.Get("RenameEditorTags")), 0, 2);
        _tagsBox.Dock = DockStyle.Fill;
        RegisterPartEditor(_tagsBox);
        panel.Controls.Add(_tagsBox, 0, 3);
        panel.SetColumnSpan(_tagsBox, 4);

        group.Controls.Add(panel);
        return group;
    }

    private Control BuildTokenPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameEditorInsertTokens"),
            Padding = new Padding(10, 8, 10, 10)
        };

        _tokenPanel.Dock = DockStyle.Fill;
        _tokenPanel.AutoScroll = true;
        _tokenPanel.WrapContents = true;
        group.Controls.Add(_tokenPanel);
        return group;
    }

    private Control BuildCommonPhrasePanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameEditorCommonPhrases"),
            Padding = new Padding(10, 8, 10, 10)
        };

        _commonPhrasePanel.Dock = DockStyle.Fill;
        _commonPhrasePanel.AutoScroll = false;
        _commonPhrasePanel.WrapContents = false;
        _commonPhrasePanel.Resize += (_, _) => RebuildCommonPhrasePanel();
        group.Controls.Add(_commonPhrasePanel);
        return group;
    }

    private Control BuildButtons(bool applyOnOk)
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 10, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));

        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _nextIssueButton.Text = Localizer.Get("ButtonNextIssue");
        _nextIssueButton.Width = 112;
        _nextIssueButton.Height = 28;
        _nextIssueButton.Click += (_, _) => SelectNextIssue();
        leftButtons.Controls.Add(_nextIssueButton);

        _skipButton.Text = Localizer.Get("ButtonRenameSkip");
        _skipButton.Width = 86;
        _skipButton.Height = 28;
        _skipButton.Click += (_, _) => SkipSelectedRow();
        leftButtons.Controls.Add(_skipButton);
        footer.Controls.Add(leftButtons, 0, 0);

        var rightButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        footer.Controls.Add(rightButtons, 1, 0);

        _okButton.Text = applyOnOk ? Localizer.Get("ButtonApply") : "OK";
        _okButton.Width = 96;
        _okButton.Height = 28;
        _okButton.Click += (_, _) => Confirm();
        rightButtons.Controls.Add(_okButton);

        _cancelButton.Text = Localizer.Get("ButtonCancel");
        _cancelButton.Width = 96;
        _cancelButton.Height = 28;
        _cancelButton.DialogResult = DialogResult.Cancel;
        rightButtons.Controls.Add(_cancelButton);
        return footer;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateSmallLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.BottomLeft
        };
    }

    private void RegisterEditableTextBox(TextBox textBox)
    {
        textBox.Enter += (_, _) => _activeTextBox = textBox;
    }

    private void RegisterPartEditor(TextBox textBox)
    {
        RegisterEditableTextBox(textBox);
        textBox.TextChanged += (_, _) => HandlePartTextChanged();
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

    private void SelectInitialRow()
    {
        var row = _rows.FirstOrDefault(static row => IsIssueState(row.State)) ?? _rows.FirstOrDefault();
        if (row is not null)
        {
            SelectRow(row);
        }
        else
        {
            SyncEditorFromRow(null);
        }
    }

    private void SelectRow(RenameRow row)
    {
        for (var index = 0; index < _grid.Rows.Count; index++)
        {
            if (!ReferenceEquals(_grid.Rows[index].DataBoundItem, row))
            {
                continue;
            }

            _grid.ClearSelection();
            _grid.Rows[index].Selected = true;
            _grid.CurrentCell = _grid.Rows[index].Cells[0];
            SyncEditorFromRow(row);
            return;
        }

        SyncEditorFromRow(row);
    }

    private void SyncEditorFromSelection()
    {
        if (_grid.CurrentRow?.DataBoundItem is RenameRow row)
        {
            SyncEditorFromRow(row);
            return;
        }

        SyncEditorFromRow(null);
    }

    private void SyncEditorFromRow(RenameRow? row)
    {
        _selectedRow = row;
        _updatingEditor = true;
        try
        {
            var hasRow = row is not null;
            _originalNameBox.Enabled = hasRow;
            _suggestedNameBox.Enabled = hasRow;
            _titleBox.Enabled = hasRow;
            _episodeBox.Enabled = hasRow;
            _authorBox.Enabled = hasRow;
            _tagsBox.Enabled = hasRow;
            _extensionBox.Enabled = hasRow;
            _useOriginalButton.Enabled = hasRow;
            _useAutomaticButton.Enabled = hasRow;

            _originalNameBox.Text = row?.OriginalName ?? "";
            _suggestedNameBox.Text = row?.SuggestedName ?? "";
            _titleBox.Text = row?.Draft.Title ?? "";
            _episodeBox.Text = row?.Draft.EpisodeRange ?? "";
            _authorBox.Text = row?.Draft.Author ?? "";
            _tagsBox.Text = row?.Draft.TagsText ?? "";
            _extensionBox.Text = row?.Draft.Extension ?? "";
        }
        finally
        {
            _updatingEditor = false;
        }

        RebuildTokenPanel(row);
        RebuildCommonPhrasePanel();
        UpdateSelectedValidation();
        UpdateCommandState();
    }

    private void HandleSuggestedNameChanged()
    {
        if (_updatingEditor || _selectedRow is null)
        {
            return;
        }

        _selectedRow.UserEdited = true;
        _selectedRow.SuggestedName = _suggestedNameBox.Text;
        ValidateRows();
    }

    private void HandlePartTextChanged()
    {
        if (_updatingEditor || _selectedRow is null)
        {
            return;
        }

        _selectedRow.UserEdited = true;
        _selectedRow.Draft.Title = _titleBox.Text;
        _selectedRow.Draft.EpisodeRange = _episodeBox.Text;
        _selectedRow.Draft.Author = _authorBox.Text;
        _selectedRow.Draft.TagsText = _tagsBox.Text;

        var composed = WindowsFileNameSafety.MakeSafeFileName(_selectedRow.Draft.Compose());
        _selectedRow.SuggestedName = composed;

        _updatingEditor = true;
        try
        {
            _suggestedNameBox.Text = composed;
        }
        finally
        {
            _updatingEditor = false;
        }

        ValidateRows();
    }

    private void UseOriginalName()
    {
        if (_selectedRow is null)
        {
            return;
        }

        SetSelectedSuggestedName(_selectedRow.OriginalName, userEdited: true, resetDraft: false);
    }

    private void UseAutomaticName()
    {
        if (_selectedRow is null)
        {
            return;
        }

        SetSelectedSuggestedName(_selectedRow.AutomaticName, userEdited: true, resetDraft: true);
    }

    private void SkipSelectedRow()
    {
        if (_selectedRow is null)
        {
            return;
        }

        _selectedRow.SetSkipped();
        SyncEditorFromRow(_selectedRow);
        ValidateRows();
    }

    private void SetSelectedSuggestedName(string fileName, bool userEdited, bool resetDraft)
    {
        if (_selectedRow is null)
        {
            return;
        }

        if (resetDraft)
        {
            _selectedRow.ResetDraft();
        }

        _selectedRow.UserEdited = userEdited;
        _selectedRow.SuggestedName = fileName;
        SyncEditorFromRow(_selectedRow);
        ValidateRows();
        _suggestedNameBox.Focus();
        _suggestedNameBox.SelectAll();
    }

    private void InsertToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var target = _activeTextBox;
        if (target is null || target.ReadOnly || !target.Enabled)
        {
            target = _suggestedNameBox;
        }

        var selectionStart = target.SelectionStart;
        target.Text = target.Text.Remove(selectionStart, target.SelectionLength).Insert(selectionStart, value);
        target.SelectionStart = selectionStart + value.Length;
        target.SelectionLength = 0;
        target.Focus();
    }

    private void NormalizeSelectedRow()
    {
        if (_selectedRow is null)
        {
            return;
        }

        NormalizeEditedRow(_selectedRow);
        SyncEditorFromRow(_selectedRow);
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

                if (row.IsSkipped)
                {
                    row.State = RenameRowState.Skipped;
                    row.TargetPath = row.Preview.OriginalPath;
                    row.Status = Localizer.Get("RenameStatusSkipped");
                    continue;
                }

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

            foreach (var row in _rows.Where(static row =>
                row.State != RenameRowState.Invalid &&
                row.State != RenameRowState.Skipped))
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
        UpdateSelectedValidation();
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
                case RenameRowState.Skipped:
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
        _nextIssueButton.Enabled = _rows.Any(static row => IsIssueState(row.State));
        _skipButton.Enabled = _selectedRow is not null && !_selectedRow.IsSkipped;
    }

    private void UpdateSelectedValidation()
    {
        var row = _selectedRow;
        if (row is null)
        {
            _validationLabel.Text = "";
            _validationLabel.BackColor = SystemColors.Control;
            _validationLabel.ForeColor = SystemColors.ControlText;
            return;
        }

        _validationLabel.Text = string.IsNullOrWhiteSpace(row.ValidationMessage)
            ? row.Status
            : row.ValidationMessage;

        switch (row.State)
        {
            case RenameRowState.Invalid:
            case RenameRowState.Conflict when row.BlockingError:
                _validationLabel.BackColor = Color.FromArgb(255, 232, 232);
                _validationLabel.ForeColor = Color.FromArgb(128, 23, 23);
                break;
            case RenameRowState.Conflict:
            case RenameRowState.NeedsReview:
                _validationLabel.BackColor = Color.FromArgb(255, 249, 219);
                _validationLabel.ForeColor = Color.FromArgb(86, 65, 0);
                break;
            case RenameRowState.Resolved:
                _validationLabel.BackColor = Color.FromArgb(226, 246, 232);
                _validationLabel.ForeColor = Color.FromArgb(18, 92, 54);
                break;
            default:
                _validationLabel.BackColor = SystemColors.Control;
                _validationLabel.ForeColor = SystemColors.ControlText;
                break;
        }
    }

    private void SelectNextIssue()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var start = _selectedRow is null ? -1 : _rows.IndexOf(_selectedRow);
        for (var offset = 1; offset <= _rows.Count; offset++)
        {
            var index = (start + offset + _rows.Count) % _rows.Count;
            var row = _rows[index];
            if (!IsIssueState(row.State))
            {
                continue;
            }

            SelectRow(row);
            _suggestedNameBox.Focus();
            return;
        }
    }

    private void RebuildTokenPanel(RenameRow? row)
    {
        _tokenPanel.Controls.Clear();
        if (row is null)
        {
            return;
        }

        foreach (var token in BuildTokens(row).Take(24))
        {
            AddTokenButton(_tokenPanel, token);
        }
    }

    private void RebuildCommonPhrasePanel()
    {
        _commonPhrasePanel.Controls.Clear();
        if (_commonPhrases.Length == 0)
        {
            _commonPhrasePanel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = Localizer.Get("RenameEditorNoCommonPhrases"),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 5, 0, 0)
            });
            return;
        }

        _commonPhrasePanel.AutoScroll = _commonPhrasesExpanded;
        _commonPhrasePanel.WrapContents = _commonPhrasesExpanded;

        var visiblePhrases = _commonPhrases.AsEnumerable();
        var hiddenCount = 0;
        if (!_commonPhrasesExpanded)
        {
            var unreservedCount = CountCommonPhrasesThatFit(0);
            if (unreservedCount < _commonPhrases.Length)
            {
                var toggleReserve = GetTokenButtonWidth(Localizer.Get("ButtonShowMoreCommonPhrases")) + TokenButtonRightMargin;
                var visibleCount = CountCommonPhrasesThatFit(toggleReserve);
                hiddenCount = _commonPhrases.Length - visibleCount;
                visiblePhrases = _commonPhrases.Take(visibleCount);
            }
        }

        foreach (var phrase in visiblePhrases)
        {
            AddTokenButton(_commonPhrasePanel, phrase);
        }

        if (_commonPhrasesExpanded)
        {
            AddCommonPhraseToggleButton(Localizer.Get("ButtonCollapseCommonPhrases"), Localizer.Get("ButtonCollapseCommonPhrasesTooltip"));
        }
        else if (hiddenCount > 0)
        {
            AddCommonPhraseToggleButton(Localizer.Get("ButtonShowMoreCommonPhrases"), Localizer.Get("ButtonShowMoreCommonPhrasesTooltip"));
        }
    }

    private void AddTokenButton(FlowLayoutPanel panel, string value)
    {
        var width = GetTokenButtonWidth(value);
        var button = new Button
        {
            Text = value,
            AutoEllipsis = true,
            Width = width,
            Height = TokenButtonHeight,
            Margin = new Padding(0, 0, TokenButtonRightMargin, TokenButtonBottomMargin)
        };
        _toolTip.SetToolTip(button, value);
        button.Click += (_, _) => InsertToken(value);
        panel.Controls.Add(button);
    }

    private void AddCommonPhraseToggleButton(string text, string toolTip)
    {
        var button = new Button
        {
            Text = text,
            AutoEllipsis = true,
            Width = GetTokenButtonWidth(text),
            Height = TokenButtonHeight,
            Margin = new Padding(0, 0, TokenButtonRightMargin, TokenButtonBottomMargin)
        };
        _toolTip.SetToolTip(button, toolTip);
        button.Click += (_, _) => ToggleCommonPhraseExpansion();
        _commonPhrasePanel.Controls.Add(button);
    }

    private int CountCommonPhrasesThatFit(int reservedWidth)
    {
        var panelWidth = _commonPhrasePanel.ClientSize.Width;
        if (panelWidth <= 0)
        {
            panelWidth = Math.Max(240, ClientSize.Width - 460);
        }

        var availableWidth = Math.Max(0, panelWidth - reservedWidth);
        var usedWidth = 0;
        var count = 0;
        foreach (var phrase in _commonPhrases)
        {
            var itemWidth = GetTokenButtonWidth(phrase) + TokenButtonRightMargin;
            if (count > 0 && usedWidth + itemWidth > availableWidth)
            {
                break;
            }

            if (count == 0 && itemWidth > availableWidth)
            {
                return availableWidth >= TokenButtonMinWidth ? 1 : 0;
            }

            usedWidth += itemWidth;
            count++;
        }

        return count;
    }

    private int GetTokenButtonWidth(string value)
    {
        return Math.Min(TokenButtonMaxWidth, Math.Max(TokenButtonMinWidth, TextRenderer.MeasureText(value, Font).Width + 24));
    }

    private void ToggleCommonPhraseExpansion()
    {
        _commonPhrasesExpanded = !_commonPhrasesExpanded;
        UpdateCommonPhraseRowHeight();
        RebuildCommonPhrasePanel();
    }

    private void UpdateCommonPhraseRowHeight()
    {
        var selectedEditorPanel = _selectedEditorPanel;
        if (selectedEditorPanel is null || selectedEditorPanel.RowStyles.Count < 5)
        {
            return;
        }

        var tokenRow = selectedEditorPanel.RowStyles[3];
        var phraseRow = selectedEditorPanel.RowStyles[4];
        if (_commonPhrasesExpanded)
        {
            tokenRow.SizeType = SizeType.Percent;
            tokenRow.Height = 50;
            phraseRow.SizeType = SizeType.Percent;
            phraseRow.Height = 50;
        }
        else
        {
            tokenRow.SizeType = SizeType.Percent;
            tokenRow.Height = 100;
            phraseRow.SizeType = SizeType.Absolute;
            phraseRow.Height = CommonPhraseCollapsedRowHeight;
        }

        selectedEditorPanel.PerformLayout();
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
        foreach (var row in _rows)
        {
            NormalizeEditedRow(row);
        }

        ValidateRows();
        var blockingRow = _rows.FirstOrDefault(static row => row.BlockingError);
        if (blockingRow is not null)
        {
            SelectRow(blockingRow);
            ShowValidation(blockingRow.ValidationMessage);
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

    private static IEnumerable<string> BuildTokens(RenameRow row)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in GetTokenCandidates(row))
        {
            var normalized = token.Trim();
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                continue;
            }

            yield return normalized;
        }
    }

    private static IEnumerable<string> GetTokenCandidates(RenameRow row)
    {
        var originalStem = Path.GetFileNameWithoutExtension(row.OriginalName);
        yield return originalStem;

        foreach (var token in SplitUsefulTokens(originalStem))
        {
            yield return token;
        }

        yield return row.Preview.Parts.Title;
        if (!string.IsNullOrWhiteSpace(row.Preview.Parts.EpisodeRange))
        {
            yield return row.Preview.Parts.EpisodeRange;
        }

        if (!string.IsNullOrWhiteSpace(row.Preview.Parts.Author))
        {
            yield return row.Preview.Parts.Author;
        }

        foreach (var tag in row.Preview.Parts.Tags)
        {
            yield return tag;
        }

        foreach (var candidate in row.Preview.Candidates)
        {
            yield return candidate.Value;
        }
    }

    private static IEnumerable<string> SplitUsefulTokens(string value)
    {
        var separators = new[] { ' ', '\t', '_', '-', '.', ',', ';', '[', ']', '(', ')', '{', '}', '~' };
        return value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static token => token.Length >= 2);
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

    private static bool IsIssueState(RenameRowState state)
    {
        return state is RenameRowState.Invalid or RenameRowState.Conflict or RenameRowState.NeedsReview;
    }

    private static string[] LoadCommonPhrases()
    {
        try
        {
            return File.Exists(RenameDictionaryStore.DictionaryPath)
                ? RenameDictionaryStore.Load().CommonPhrases
                    .Where(static phrase => !string.IsNullOrWhiteSpace(phrase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileToolsEnvironment.Log("RENAME-COMMON-PHRASES", ex.Message);
            return [];
        }
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
        Skipped,
        Invalid
    }

    private sealed class RenamePartDraft
    {
        public string Title { get; set; } = "";

        public string EpisodeRange { get; set; } = "";

        public string Author { get; set; } = "";

        public string Extension { get; init; } = "";

        public string[] Tags { get; private set; } = [];

        public string TagsText
        {
            get => string.Join(", ", Tags);
            set => Tags = ParseTags(value);
        }

        public static RenamePartDraft From(FileNameParts parts)
        {
            return new RenamePartDraft
            {
                Title = parts.Title,
                EpisodeRange = parts.EpisodeRange ?? "",
                Author = parts.Author ?? "",
                Extension = parts.Extension,
                Tags = parts.Tags.ToArray()
            };
        }

        public string Compose()
        {
            return new FileNameParts
            {
                Title = Title.Trim(),
                EpisodeRange = string.IsNullOrWhiteSpace(EpisodeRange) ? null : EpisodeRange.Trim(),
                Author = string.IsNullOrWhiteSpace(Author) ? null : Author.Trim(),
                Tags = Tags,
                Extension = Extension
            }.Compose();
        }

        private static string[] ParseTags(string value)
        {
            return value
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static tag => tag.Trim('[', ']', '(', ')'))
                .Where(static tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private sealed class RenameRow : INotifyPropertyChanged
    {
        private string _suggestedName = "";
        private bool _isSkipped;
        private string _status = "";

        public RenameRow(RenamePreview preview)
        {
            Preview = preview;
            AutomaticName = preview.SuggestedFileName;
            Draft = RenamePartDraft.From(preview.Parts);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public RenamePreview Preview { get; }

        public RenamePartDraft Draft { get; private set; }

        public string AutomaticName { get; }

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
                OnPropertyChanged(nameof(IsSkipped));
            }
        }

        public string TargetPath { get; set; } = "";

        public string ValidationMessage { get; set; } = "";

        public void ResetDraft()
        {
            Draft = RenamePartDraft.From(Preview.Parts);
        }

        public void SetSkipped()
        {
            IsSkipped = true;
            UserEdited = true;
            _suggestedName = OriginalName;
            OnPropertyChanged(nameof(SuggestedName));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
