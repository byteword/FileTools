using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FileTools;

internal sealed class ArchiveEncodingDialog : Form
{
    private const string EncodingColumnName = "Encoding";
    private const string DescriptionColumnName = "Description";
    private const string ScoreColumnName = "Score";
    private const string PreviewColumnName = "Preview";

    private readonly ArchiveEncodingQuestion _question;
    private readonly DataGridView _grid = new();

    public ArchiveEncodingDialog(ArchiveEncodingQuestion question)
    {
        _question = question;
        Text = Localizer.Get("ArchiveEncodingDialogTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(820, 460);
        MinimumSize = new Size(700, 360);

        BuildLayout();
        LoadCandidates();
    }

    public Encoding? SelectedEncoding { get; private set; }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("ArchiveEncodingDialogHeader"),
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(title, 0, 0);

        var fileLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(55, 65, 81),
            Text = _question.ArchivePath,
            TextAlign = ContentAlignment.MiddleLeft
        };
        fileLabel.SetToolTip(_question.ArchivePath);
        root.Controls.Add(fileLabel, 0, 1);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };
        var okButton = new Button { Text = "OK", Width = 92, Height = 30 };
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 92,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        okButton.Click += (_, _) => AcceptSelection();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);
        root.Controls.Add(buttons, 0, 3);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.MultiSelect = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.ShowCellToolTips = true;
        _grid.AutoGenerateColumns = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = EncodingColumnName,
            HeaderText = Localizer.Get("ArchiveEncodingColumnEncoding"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 210,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = DescriptionColumnName,
            HeaderText = Localizer.Get("ArchiveEncodingColumnDescription"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 50,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ScoreColumnName,
            HeaderText = Localizer.Get("ArchiveEncodingColumnScore"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 70,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = PreviewColumnName,
            HeaderText = Localizer.Get("ArchiveEncodingColumnPreview"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 50,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.CellDoubleClick += (_, _) => AcceptSelection();
    }

    private void LoadCandidates()
    {
        foreach (var candidate in _question.Candidates)
        {
            var rowIndex = _grid.Rows.Add();
            var row = _grid.Rows[rowIndex];
            row.Tag = candidate;
            row.Cells[EncodingColumnName].Value = candidate.DisplayName;
            row.Cells[DescriptionColumnName].Value = candidate.Description;
            row.Cells[ScoreColumnName].Value = candidate.Score.ToString();
            row.Cells[PreviewColumnName].Value = string.Join(", ", candidate.PreviewNames.Take(4));
            foreach (var cell in row.Cells.Cast<DataGridViewCell>())
            {
                cell.ToolTipText = CreateCandidateToolTip(candidate);
            }
        }

        if (_grid.Rows.Count > 0)
        {
            _grid.Rows[0].Selected = true;
            _grid.CurrentCell = _grid.Rows[0].Cells[EncodingColumnName];
        }
    }

    private void AcceptSelection()
    {
        var candidate = _grid.CurrentRow?.Tag as ArchiveEncodingCandidateResult;
        if (candidate is null)
        {
            return;
        }

        SelectedEncoding = candidate.Encoding;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string CreateCandidateToolTip(ArchiveEncodingCandidateResult candidate)
    {
        var lines = new List<string>
        {
            candidate.DisplayName,
            candidate.Description,
            Localizer.Format("ArchiveEncodingScoreFormat", candidate.Score)
        };
        lines.AddRange(candidate.PreviewNames.Take(12));
        return string.Join(Environment.NewLine, lines);
    }
}

