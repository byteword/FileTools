using System.Globalization;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class FileCompareResultDialog : Form
{
    private const string FilterAll = "__all";

    private readonly FileCompareReport _report;
    private readonly Label _summaryLabel = new();
    private readonly ComboBox _statusFilterCombo = new();
    private readonly DataGridView _pairGrid = new();
    private readonly DataGridView _criteriaGrid = new();
    private readonly Button _closeButton = new();

    public FileCompareResultDialog(FileCompareReport report)
    {
        _report = report;
        Text = Localizer.Get("FileCompareResultTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1120, 760);
        MinimumSize = new Size(920, 620);

        BuildLayout();
        ConfigureGrids();
        LoadFilterOptions();
        ApplySummary();
        RefreshPairGrid();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        root.Controls.Add(header, 0, 0);

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

        root.Controls.Add(_pairGrid, 0, 1);
        root.Controls.Add(_criteriaGrid, 0, 2);

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
        root.Controls.Add(buttonPanel, 0, 3);

        AcceptButton = _closeButton;
        CancelButton = _closeButton;
    }

    private void ConfigureGrids()
    {
        ConfigureBaseGrid(_pairGrid);
        _pairGrid.Columns.Add(CreateTextColumn("Status", Localizer.Get("FileCompareColumnStatus"), width: 110));
        _pairGrid.Columns.Add(CreateTextColumn("Ratio", Localizer.Get("FileCompareColumnMatchRatio"), width: 96));
        _pairGrid.Columns.Add(CreateTextColumn("Left", Localizer.Get("FileCompareColumnLeft"), fillWeight: 34));
        _pairGrid.Columns.Add(CreateTextColumn("Right", Localizer.Get("FileCompareColumnRight"), fillWeight: 34));
        _pairGrid.Columns.Add(CreateTextColumn("Reason", Localizer.Get("FileCompareColumnReason"), fillWeight: 28));
        _pairGrid.SelectionChanged += (_, _) => RefreshCriteriaGrid();

        ConfigureBaseGrid(_criteriaGrid);
        _criteriaGrid.Columns.Add(CreateTextColumn("Name", Localizer.Get("FileCompareColumnCriterion"), width: 170));
        _criteriaGrid.Columns.Add(CreateTextColumn("Status", Localizer.Get("FileCompareColumnStatus"), width: 110));
        _criteriaGrid.Columns.Add(CreateTextColumn("Ratio", Localizer.Get("FileCompareColumnMatchRatio"), width: 96));
        _criteriaGrid.Columns.Add(CreateTextColumn("Detail", Localizer.Get("FileCompareColumnDetail"), fillWeight: 100));
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
    }

    private void RefreshCriteriaGrid()
    {
        _criteriaGrid.Rows.Clear();
        var pair = _pairGrid.CurrentRow?.Tag as FileComparePairResult;
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
