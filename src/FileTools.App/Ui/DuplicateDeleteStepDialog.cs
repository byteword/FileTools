using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FileTools;

internal sealed class DuplicateDeleteStepDialog : Form
{
    private readonly List<RowState> _rows;
    private readonly Label _summaryLabel = new();
    private readonly DataGridView _deleteGrid = new();
    private readonly DataGridView _keepGrid = new();
    private readonly Button _moveToDeleteButton = new();
    private readonly Button _moveToKeepButton = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();

    public DuplicateDeleteStepDialog(IEnumerable<DuplicateDeleteStepCandidate> candidates)
    {
        _rows = candidates
            .Select(static candidate => RowState.Create(candidate))
            .OrderBy(static row => row.FileName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static row => row.Path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Text = Localizer.Get("DuplicateDeleteDialogTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1040, 620);
        MinimumSize = new Size(880, 500);
        MinimizeBox = false;
        MaximizeBox = false;

        BuildLayout();
        ConfigureGrid(_deleteGrid);
        ConfigureGrid(_keepGrid);
        RefreshGrids();
        UpdateSummary();
        UpdateCommandState();
    }

    public IReadOnlyList<string> DeletePaths => _rows
        .Where(static row => row.Delete)
        .Select(static row => row.Path)
        .ToArray();

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        var helpLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("DuplicateDeleteDialogHelp"),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        header.Controls.Add(helpLabel, 0, 0);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _summaryLabel.AutoEllipsis = true;
        _summaryLabel.ForeColor = Color.FromArgb(55, 65, 81);
        header.Controls.Add(_summaryLabel, 0, 1);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(CreateSelectionLayout(), 0, 1);
        root.Controls.Add(CreateButtonPanel(), 0, 2);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private Control CreateSelectionLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        layout.Controls.Add(CreateGridGroup(
            Localizer.Get("DuplicateDeleteDialogDeleteTargets"),
            _deleteGrid,
            Color.FromArgb(153, 27, 27)), 0, 0);
        layout.Controls.Add(CreateMoveButtons(), 1, 0);
        layout.Controls.Add(CreateGridGroup(
            Localizer.Get("DuplicateDeleteDialogKeepTargets"),
            _keepGrid,
            Color.FromArgb(22, 101, 52)), 2, 0);
        return layout;
    }

    private Control CreateGridGroup(string title, DataGridView grid, Color titleColor)
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = titleColor,
            Padding = new Padding(10)
        };
        group.Controls.Add(grid);
        return group;
    }

    private Control CreateMoveButtons()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10, 120, 10, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        _moveToDeleteButton.Text = Localizer.Get("DuplicateDeleteDialogMoveToDelete");
        _moveToDeleteButton.Dock = DockStyle.Fill;
        _moveToDeleteButton.Click += (_, _) => MoveSelected(_keepGrid, delete: true);
        panel.Controls.Add(_moveToDeleteButton, 0, 0);

        _moveToKeepButton.Text = Localizer.Get("DuplicateDeleteDialogMoveToKeep");
        _moveToKeepButton.Dock = DockStyle.Fill;
        _moveToKeepButton.Click += (_, _) => MoveSelected(_deleteGrid, delete: false);
        panel.Controls.Add(_moveToKeepButton, 0, 2);

        var noteLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("DuplicateDeleteDialogMoveHint"),
            TextAlign = ContentAlignment.BottomCenter,
            ForeColor = Color.FromArgb(93, 99, 108)
        };
        panel.Controls.Add(noteLabel, 0, 4);
        return panel;
    }

    private Control CreateButtonPanel()
    {
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        _okButton.Text = Localizer.Get("ButtonOK");
        _okButton.Width = 96;
        _okButton.Height = 30;
        _okButton.DialogResult = DialogResult.OK;
        _cancelButton.Text = Localizer.Get("ButtonCancel");
        _cancelButton.Width = 96;
        _cancelButton.Height = 30;
        _cancelButton.DialogResult = DialogResult.Cancel;
        buttonPanel.Controls.Add(_okButton);
        buttonPanel.Controls.Add(_cancelButton);
        return buttonPanel;
    }

    private void ConfigureGrid(DataGridView grid)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoGenerateColumns = false;
        grid.BackgroundColor = SystemColors.Window;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.Dock = DockStyle.Fill;
        grid.ForeColor = SystemColors.ControlText;
        grid.MultiSelect = true;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ShowCellToolTips = true;
        grid.RowTemplate.Height = 26;
        grid.SelectionChanged += (_, _) => UpdateCommandState();
        grid.CellDoubleClick += (_, args) =>
        {
            if (args.RowIndex < 0)
            {
                return;
            }

            if (ReferenceEquals(grid, _deleteGrid))
            {
                MoveSelected(_deleteGrid, delete: false);
            }
            else
            {
                MoveSelected(_keepGrid, delete: true);
            }
        };

        grid.Columns.Add(CreateTextColumn(
            "FileName",
            Localizer.Get("DuplicateDeleteColumnFileName"),
            fillWeight: 38));
        grid.Columns.Add(CreateTextColumn(
            "Size",
            Localizer.Get("DuplicateDeleteColumnSize"),
            width: 96));
        grid.Columns.Add(CreateTextColumn(
            "Modified",
            Localizer.Get("DuplicateDeleteColumnModified"),
            width: 140));
        grid.Columns.Add(CreateTextColumn(
            "Path",
            Localizer.Get("DuplicateDeleteColumnPath"),
            fillWeight: 62));
    }

    private void RefreshGrids()
    {
        LoadGrid(_deleteGrid, delete: true, Color.FromArgb(254, 242, 242));
        LoadGrid(_keepGrid, delete: false, Color.FromArgb(240, 253, 244));
    }

    private void LoadGrid(DataGridView grid, bool delete, Color rowColor)
    {
        grid.Rows.Clear();
        foreach (var row in _rows.Where(row => row.Delete == delete))
        {
            var rowIndex = grid.Rows.Add(
                row.FileName,
                row.SizeText,
                row.ModifiedText,
                row.Path);
            var gridRow = grid.Rows[rowIndex];
            gridRow.Tag = row;
            gridRow.DefaultCellStyle.BackColor = rowColor;
            foreach (var cell in gridRow.Cells.Cast<DataGridViewCell>())
            {
                cell.ToolTipText = row.Path;
            }
        }
    }

    private void MoveSelected(DataGridView sourceGrid, bool delete)
    {
        var selectedRows = sourceGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(static row => row.Tag)
            .OfType<RowState>()
            .ToArray();
        if (selectedRows.Length == 0)
        {
            return;
        }

        foreach (var row in selectedRows)
        {
            row.Delete = delete;
        }

        RefreshGrids();
        UpdateSummary();
        UpdateCommandState();
    }

    private void UpdateSummary()
    {
        var deleteCount = _rows.Count(static row => row.Delete);
        _summaryLabel.Text = Localizer.Format(
            "DuplicateDeleteDialogSummaryFormat",
            deleteCount,
            _rows.Count - deleteCount,
            _rows.Count);
    }

    private void UpdateCommandState()
    {
        _moveToDeleteButton.Enabled = _keepGrid.SelectedRows.Count > 0;
        _moveToKeepButton.Enabled = _deleteGrid.SelectedRows.Count > 0;
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(
        string name,
        string header,
        int? width = null,
        float fillWeight = 0)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            AutoSizeMode = width is null ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
            FillWeight = fillWeight <= 0 ? 20 : fillWeight,
            Width = width ?? 120,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
    }

    private sealed class RowState
    {
        private RowState(
            string path,
            bool delete,
            string fileName,
            string sizeText,
            string modifiedText)
        {
            Path = path;
            Delete = delete;
            FileName = fileName;
            SizeText = sizeText;
            ModifiedText = modifiedText;
        }

        public string Path { get; }

        public bool Delete { get; set; }

        public string FileName { get; }

        public string SizeText { get; }

        public string ModifiedText { get; }

        public static RowState Create(DuplicateDeleteStepCandidate candidate)
        {
            var info = new FileInfo(candidate.Path);
            var fileName = System.IO.Path.GetFileName(candidate.Path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = candidate.Path;
            }

            return new RowState(
                candidate.Path,
                candidate.Delete,
                fileName,
                info.Exists ? FormatFileSize(info.Length) : "",
                info.Exists ? info.LastWriteTime.ToString("g", CultureInfo.CurrentCulture) : "");
        }

        private static string FormatFileSize(long bytes)
        {
            return Localizer.Format("DuplicateDeleteFileSizeFormat", bytes);
        }
    }
}
