using System.Diagnostics;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class FileCompareResultDialog : Form
{
    private const string FilterAll = "__all";

    private readonly FileCompareReport _report;
    private readonly Action<IReadOnlyList<string>>? _addTargetsAction;
    private readonly IReadOnlyList<FileCompareDuplicateGroup> _duplicateGroups;
    private readonly Label _summaryLabel = new();
    private readonly ComboBox _statusFilterCombo = new();
    private readonly DataGridView _pairGrid = new();
    private readonly DataGridView _criteriaGrid = new();
    private readonly DataGridView _duplicateGrid = new();
    private readonly Label _duplicateSummaryLabel = new();
    private readonly Label _actionStatusLabel = new();
    private readonly Button _copyDuplicateCandidatesButton = new();
    private readonly Button _addDuplicateCandidatesButton = new();
    private readonly Button _copyPairPathsButton = new();
    private readonly Button _addPairPathsButton = new();
    private readonly Button _openPairFoldersButton = new();
    private readonly Button _closeButton = new();

    public FileCompareResultDialog(
        FileCompareReport report,
        Action<IReadOnlyList<string>>? addTargetsAction = null)
    {
        _report = report;
        _addTargetsAction = addTargetsAction;
        _duplicateGroups = FileCompareResultActions.BuildDuplicateGroups(report);
        Text = Localizer.Get("FileCompareResultTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1280, 800);
        MinimumSize = new Size(1040, 660);

        BuildLayout();
        ConfigureGrids();
        LoadFilterOptions();
        ApplySummary();
        RefreshPairGrid();
        RefreshDuplicateGrid();
        UpdateActionState();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(CreateHeader(), 0, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel2,
            Orientation = Orientation.Vertical,
            SplitterWidth = 8,
            Panel2MinSize = 310
        };
        split.Panel1.Controls.Add(CreateResultGridLayout());
        split.Panel2.Controls.Add(CreateActionPanel());
        root.Controls.Add(split, 0, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        _closeButton.Text = Localizer.Get("ButtonClose");
        _closeButton.Width = 96;
        _closeButton.Height = 30;
        _closeButton.DialogResult = DialogResult.OK;
        buttonPanel.Controls.Add(_closeButton);
        root.Controls.Add(buttonPanel, 0, 2);

        AcceptButton = _closeButton;
        CancelButton = _closeButton;
    }

    private Control CreateHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _summaryLabel.AutoEllipsis = true;
        header.Controls.Add(_summaryLabel, 0, 0);

        var filterLabel = new Label
        {
            Text = Localizer.Get("FileCompareResultFilter"),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 8, 0)
        };
        header.Controls.Add(filterLabel, 1, 0);

        _statusFilterCombo.Dock = DockStyle.Fill;
        _statusFilterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _statusFilterCombo.SelectedIndexChanged += (_, _) => RefreshPairGrid();
        header.Controls.Add(_statusFilterCombo, 2, 0);
        return header;
    }

    private Control CreateResultGridLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        layout.Controls.Add(_pairGrid, 0, 0);
        layout.Controls.Add(_criteriaGrid, 0, 1);
        return layout;
    }

    private Control CreateActionPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            Padding = new Padding(8, 0, 0, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var duplicateTitleLabel = new Label
        {
            Text = Localizer.Get("FileCompareResultDuplicateGroups"),
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(duplicateTitleLabel, 0, 0);

        _duplicateSummaryLabel.Dock = DockStyle.Fill;
        _duplicateSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _duplicateSummaryLabel.AutoEllipsis = true;
        panel.Controls.Add(_duplicateSummaryLabel, 0, 1);
        panel.Controls.Add(_duplicateGrid, 0, 2);

        ConfigureActionButton(
            _copyDuplicateCandidatesButton,
            Localizer.Get("FileCompareActionCopyDeleteCandidates"),
            (_, _) => CopyPaths(GetSelectedDuplicateDeleteCandidates()));
        panel.Controls.Add(_copyDuplicateCandidatesButton, 0, 3);

        ConfigureActionButton(
            _addDuplicateCandidatesButton,
            Localizer.Get("FileCompareActionAddDeleteCandidatesToTargets"),
            (_, _) => AddPathsToTargets(GetSelectedDuplicateDeleteCandidates()));
        panel.Controls.Add(_addDuplicateCandidatesButton, 0, 4);

        var pairTitleLabel = new Label
        {
            Text = Localizer.Get("FileCompareResultSelectedPair"),
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(pairTitleLabel, 0, 5);

        ConfigureActionButton(
            _copyPairPathsButton,
            Localizer.Get("FileCompareActionCopySelectedPairPaths"),
            (_, _) => CopyPaths(FileCompareResultActions.GetPairPaths(GetCurrentPair())));
        panel.Controls.Add(_copyPairPathsButton, 0, 6);

        ConfigureActionButton(
            _addPairPathsButton,
            Localizer.Get("FileCompareActionAddSelectedPairToTargets"),
            (_, _) => AddPathsToTargets(FileCompareResultActions.GetPairPaths(GetCurrentPair())));
        panel.Controls.Add(_addPairPathsButton, 0, 7);

        ConfigureActionButton(
            _openPairFoldersButton,
            Localizer.Get("FileCompareActionOpenSelectedPairFolders"),
            (_, _) => OpenFolders(FileCompareResultActions.GetPairPaths(GetCurrentPair())));
        panel.Controls.Add(_openPairFoldersButton, 0, 8);

        _actionStatusLabel.Dock = DockStyle.Fill;
        _actionStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _actionStatusLabel.AutoEllipsis = true;
        panel.Controls.Add(_actionStatusLabel, 0, 9);
        return panel;
    }

    private static void ConfigureActionButton(Button button, string text, EventHandler clickHandler)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Height = 30;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Click += clickHandler;
    }

    private void ConfigureGrids()
    {
        ConfigureBaseGrid(_pairGrid);
        _pairGrid.Columns.Add(CreateTextColumn("Status", Localizer.Get("FileCompareColumnStatus"), width: 110));
        _pairGrid.Columns.Add(CreateTextColumn("Ratio", Localizer.Get("FileCompareColumnMatchRatio"), width: 96));
        _pairGrid.Columns.Add(CreateTextColumn("Left", Localizer.Get("FileCompareColumnLeft"), fillWeight: 34));
        _pairGrid.Columns.Add(CreateTextColumn("Right", Localizer.Get("FileCompareColumnRight"), fillWeight: 34));
        _pairGrid.Columns.Add(CreateTextColumn("Reason", Localizer.Get("FileCompareColumnReason"), fillWeight: 28));
        _pairGrid.SelectionChanged += (_, _) =>
        {
            RefreshCriteriaGrid();
            UpdateActionState();
        };

        ConfigureBaseGrid(_criteriaGrid);
        _criteriaGrid.Columns.Add(CreateTextColumn("Name", Localizer.Get("FileCompareColumnCriterion"), width: 170));
        _criteriaGrid.Columns.Add(CreateTextColumn("Status", Localizer.Get("FileCompareColumnStatus"), width: 110));
        _criteriaGrid.Columns.Add(CreateTextColumn("Ratio", Localizer.Get("FileCompareColumnMatchRatio"), width: 96));
        _criteriaGrid.Columns.Add(CreateTextColumn("Detail", Localizer.Get("FileCompareColumnDetail"), fillWeight: 100));

        ConfigureBaseGrid(_duplicateGrid);
        _duplicateGrid.Columns.Add(CreateTextColumn("Group", Localizer.Get("FileCompareDuplicateColumnGroup"), width: 64));
        _duplicateGrid.Columns.Add(CreateTextColumn("Count", Localizer.Get("FileCompareDuplicateColumnFiles"), width: 64));
        _duplicateGrid.Columns.Add(CreateTextColumn("Keep", Localizer.Get("FileCompareDuplicateColumnKeep"), fillWeight: 45));
        _duplicateGrid.Columns.Add(CreateTextColumn("Delete", Localizer.Get("FileCompareDuplicateColumnDeleteCandidates"), fillWeight: 55));
        _duplicateGrid.SelectionChanged += (_, _) => UpdateActionState();
    }

    private static void ConfigureBaseGrid(DataGridView grid)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoGenerateColumns = false;
        grid.BackgroundColor = SystemColors.Window;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.Dock = DockStyle.Fill;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ShowCellToolTips = true;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.RowTemplate.Height = 26;
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string name, string header, int? width = null, float fillWeight = 0)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            AutoSizeMode = width is null ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
            FillWeight = fillWeight <= 0 ? 20 : fillWeight,
            Width = width ?? 120,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
    }

    private void LoadFilterOptions()
    {
        _statusFilterCombo.DataSource = new[]
        {
            new ComboOption<string>(Localizer.Get("FileCompareFilterAll"), FilterAll),
            new ComboOption<string>(FileCompareText.GetDisplayName(FileCompareStatus.Same), FileCompareStatus.Same.ToString()),
            new ComboOption<string>(FileCompareText.GetDisplayName(FileCompareStatus.Different), FileCompareStatus.Different.ToString()),
            new ComboOption<string>(FileCompareText.GetDisplayName(FileCompareStatus.PartialMatch), FileCompareStatus.PartialMatch.ToString()),
            new ComboOption<string>(FileCompareText.GetDisplayName(FileCompareStatus.Failed), FileCompareStatus.Failed.ToString())
        };
    }

    private void ApplySummary()
    {
        var same = _report.Pairs.Count(static pair => pair.Status == FileCompareStatus.Same);
        var different = _report.Pairs.Count(static pair => pair.Status == FileCompareStatus.Different);
        var partial = _report.Pairs.Count(static pair => pair.Status == FileCompareStatus.PartialMatch);
        var failed = _report.Pairs.Count(static pair => pair.Status == FileCompareStatus.Failed);
        _summaryLabel.Text = Localizer.Format(
            "FileCompareResultSummaryFormat",
            _report.Targets.Count,
            _report.Pairs.Count,
            same,
            different,
            partial,
            failed,
            _report.HashCacheHits,
            _report.HashCacheMisses);
    }

    private void RefreshPairGrid()
    {
        var selected = _statusFilterCombo.SelectedItem is ComboOption<string> option
            ? option.Value
            : FilterAll;
        var pairs = selected == FilterAll
            ? _report.Pairs
            : _report.Pairs.Where(pair => pair.Status.ToString() == selected).ToArray();

        _pairGrid.Rows.Clear();
        foreach (var pair in pairs)
        {
            var rowIndex = _pairGrid.Rows.Add(
                FileCompareText.GetDisplayName(pair.Status),
                FormatRatio(pair.MatchRatio),
                pair.Left.Path,
                pair.Right.Path,
                pair.Reason);
            var row = _pairGrid.Rows[rowIndex];
            row.Tag = pair;
            row.Cells["Left"].ToolTipText = pair.Left.Path;
            row.Cells["Right"].ToolTipText = pair.Right.Path;
            row.Cells["Reason"].ToolTipText = pair.Reason;
            row.DefaultCellStyle.BackColor = GetStatusBackColor(pair.Status);
        }

        if (_pairGrid.Rows.Count > 0)
        {
            _pairGrid.Rows[0].Selected = true;
            _pairGrid.CurrentCell = _pairGrid.Rows[0].Cells[0];
        }

        RefreshCriteriaGrid();
        UpdateActionState();
    }

    private void RefreshCriteriaGrid()
    {
        _criteriaGrid.Rows.Clear();
        var pair = GetCurrentPair();
        if (pair is null)
        {
            return;
        }

        foreach (var criterion in pair.Criteria)
        {
            var rowIndex = _criteriaGrid.Rows.Add(
                criterion.Name,
                FileCompareText.GetDisplayName(criterion.Status),
                FormatRatio(criterion.MatchRatio),
                criterion.Detail);
            var row = _criteriaGrid.Rows[rowIndex];
            row.Cells["Detail"].ToolTipText = criterion.Detail;
            row.DefaultCellStyle.BackColor = GetStatusBackColor(criterion.Status);
        }
    }

    private void RefreshDuplicateGrid()
    {
        _duplicateGrid.Rows.Clear();
        foreach (var group in _duplicateGroups)
        {
            var rowIndex = _duplicateGrid.Rows.Add(
                group.Number.ToString(CultureInfo.CurrentCulture),
                group.Paths.Count.ToString(CultureInfo.CurrentCulture),
                group.KeepPath,
                string.Join("; ", group.DeleteCandidates));
            var row = _duplicateGrid.Rows[rowIndex];
            row.Tag = group;
            row.Cells["Keep"].ToolTipText = group.KeepPath;
            row.Cells["Delete"].ToolTipText = string.Join(Environment.NewLine, group.DeleteCandidates);
        }

        if (_duplicateGrid.Rows.Count > 0)
        {
            _duplicateGrid.Rows[0].Selected = true;
            _duplicateGrid.CurrentCell = _duplicateGrid.Rows[0].Cells[0];
        }

        var deleteCandidateCount = FileCompareResultActions.GetDeleteCandidates(_duplicateGroups).Count;
        _duplicateSummaryLabel.Text = deleteCandidateCount > 0
            ? Localizer.Format("FileCompareDuplicateSummaryFormat", _duplicateGroups.Count, deleteCandidateCount)
            : Localizer.Get("FileCompareNoDuplicateGroups");
    }

    private void UpdateActionState()
    {
        var hasPair = GetCurrentPair() is not null;
        var hasDuplicateCandidates = GetSelectedDuplicateDeleteCandidates().Count > 0;
        _copyPairPathsButton.Enabled = hasPair;
        _addPairPathsButton.Enabled = hasPair && _addTargetsAction is not null;
        _openPairFoldersButton.Enabled = hasPair;
        _copyDuplicateCandidatesButton.Enabled = hasDuplicateCandidates;
        _addDuplicateCandidatesButton.Enabled = hasDuplicateCandidates && _addTargetsAction is not null;
    }

    private FileComparePairResult? GetCurrentPair()
    {
        return _pairGrid.CurrentRow?.Tag as FileComparePairResult;
    }

    private IReadOnlyList<string> GetSelectedDuplicateDeleteCandidates()
    {
        var groups = _duplicateGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(static row => row.Tag)
            .OfType<FileCompareDuplicateGroup>()
            .ToArray();
        if (groups.Length == 0)
        {
            groups = _duplicateGroups.ToArray();
        }

        return FileCompareResultActions.GetDeleteCandidates(groups);
    }

    private void CopyPaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            ShowActionStatus(Localizer.Get("FileCompareActionNoPaths"));
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, paths));
        ShowActionStatus(Localizer.Format("FileCompareActionCopiedFormat", paths.Count));
    }

    private void AddPathsToTargets(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            ShowActionStatus(Localizer.Get("FileCompareActionNoPaths"));
            return;
        }

        _addTargetsAction?.Invoke(paths);
        ShowActionStatus(Localizer.Format("FileCompareActionAddedTargetsFormat", paths.Count));
    }

    private void OpenFolders(IReadOnlyList<string> paths)
    {
        var folders = paths
            .Select(GetFolderPath)
            .Where(static path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Select(static path => path!)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
        foreach (var folder in folders)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", folder)
            {
                UseShellExecute = true
            });
        }

        ShowActionStatus(Localizer.Format("FileCompareActionOpenedFoldersFormat", folders.Length));
    }

    private void ShowActionStatus(string message)
    {
        _actionStatusLabel.Text = message;
    }

    private static string? GetFolderPath(string path)
    {
        if (Directory.Exists(path))
        {
            return path;
        }

        return Path.GetDirectoryName(path);
    }

    private static string FormatRatio(double ratio)
    {
        return (ratio * 100).ToString("0.##", CultureInfo.CurrentCulture) + "%";
    }

    private static Color GetStatusBackColor(FileCompareStatus status)
    {
        return status switch
        {
            FileCompareStatus.Same => Color.FromArgb(236, 253, 245),
            FileCompareStatus.PartialMatch => Color.FromArgb(255, 251, 235),
            FileCompareStatus.Failed => Color.FromArgb(254, 242, 242),
            FileCompareStatus.Different => Color.FromArgb(248, 250, 252),
            _ => SystemColors.Window
        };
    }
}
