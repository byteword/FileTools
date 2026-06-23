using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class FolderWrapOptionsDialog : Form
{
    private const string SourceColumnName = "Source";
    private const string TargetFolderColumnName = "TargetFolder";
    private const string TargetPathColumnName = "TargetPath";
    private const string StatusColumnName = "Status";

    private readonly IReadOnlyList<string> _sourcePaths;
    private readonly FileToolsSettings _settings;
    private readonly DataGridView _grid = new();
    private readonly Button _okButton = new();

    public FolderWrapOptionsDialog(IReadOnlyList<string> sourcePaths, FileToolsSettings settings)
    {
        _sourcePaths = sourcePaths.Select(Path.GetFullPath).ToArray();
        _settings = settings;

        Text = Localizer.Get("FolderWrapOptionsDialogTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(860, 520);
        MinimumSize = new Size(720, 420);
        ShowInTaskbar = false;

        BuildLayout();
        LoadRows();
        RefreshRows();
    }

    public IReadOnlyDictionary<string, string> ResultFolderNames { get; private set; } =
        new Dictionary<string, string>();

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FolderWrapDialogHeader"),
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = SourceColumnName,
            HeaderText = Localizer.Get("ColumnTargetName"),
            ReadOnly = true,
            Width = 180,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = TargetFolderColumnName,
            HeaderText = Localizer.Get("FolderWrapTargetFolderName"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 34,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = TargetPathColumnName,
            HeaderText = Localizer.Get("FolderWrapTargetPath"),
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 46,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = StatusColumnName,
            HeaderText = Localizer.Get("ColumnPlanPreview"),
            ReadOnly = true,
            Width = 150,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.CellEndEdit += (_, _) => RefreshRows();
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        root.Controls.Add(_grid, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 92,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        _okButton.Text = Localizer.Get("ButtonOK");
        _okButton.Width = 92;
        _okButton.Height = 30;
        _okButton.Click += (_, _) => SaveAndClose();
        var advancedButton = new Button
        {
            Text = Localizer.Get("ButtonAdvanced"),
            Width = 92,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        advancedButton.Click += (_, _) => OpenAdvancedNameEditor();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(advancedButton);
        root.Controls.Add(buttons, 0, 2);
        AcceptButton = _okButton;
        CancelButton = cancelButton;
    }

    private void LoadRows()
    {
        foreach (var sourcePath in _sourcePaths)
        {
            var preview = FolderWrapOperations.CreatePreview(sourcePath, _settings);
            var rowIndex = _grid.Rows.Add();
            var row = _grid.Rows[rowIndex];
            row.Tag = sourcePath;
            row.Cells[SourceColumnName].Value = Path.GetFileName(sourcePath);
            row.Cells[TargetFolderColumnName].Value = preview.TargetFolderName;
            row.Cells[TargetPathColumnName].Value = preview.TargetFolderPath ?? "";
            row.Cells[StatusColumnName].Value = "";
        }
    }

    private void RefreshRows()
    {
        var allReady = _grid.Rows.Count > 0;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var sourcePath = GetSourcePath(row);
            var targetName = Convert.ToString(row.Cells[TargetFolderColumnName].Value) ?? "";
            var preview = FolderWrapOperations.CreatePreview(sourcePath, _settings, targetName);
            row.Cells[TargetFolderColumnName].Value = preview.TargetFolderName;
            row.Cells[TargetPathColumnName].Value = preview.TargetFolderPath ?? "";
            row.Cells[StatusColumnName].Value = preview.IsReady
                ? Localizer.Get("ArchiveMergeEntryPreviewStatusReady")
                : preview.FailureReason ?? Localizer.Get("PlanPreviewUnavailable");
            row.DefaultCellStyle.BackColor = preview.IsReady
                ? Color.FromArgb(240, 253, 244)
                : Color.FromArgb(254, 242, 242);
            allReady &= preview.IsReady;
        }

        _okButton.Enabled = allReady;
    }

    private void OpenAdvancedNameEditor()
    {
        RefreshRows();
        var row = _grid.CurrentRow ?? _grid.Rows.Cast<DataGridViewRow>().FirstOrDefault();
        if (row is null)
        {
            return;
        }

        var sourcePath = GetSourcePath(row);
        var automaticName = FolderWrapOperations.CreatePreview(sourcePath, _settings).TargetFolderName;
        var edited = AdvancedNameEditDialog.EditName(
            this,
            Localizer.Get("AdvancedNameDialogTitle"),
            Localizer.Get("AdvancedNameFolderWrapHeader"),
            new NameEditRequest(
                OriginalName: Path.GetFileName(sourcePath),
                SuggestedName: Convert.ToString(row.Cells[TargetFolderColumnName].Value) ?? "",
                AutomaticName: automaticName,
                Recommendations: BuildRecommendations(sourcePath, automaticName)));
        if (edited is null)
        {
            return;
        }

        row.Cells[TargetFolderColumnName].Value = edited;
        RefreshRows();
    }

    private static IReadOnlyList<string> BuildRecommendations(string sourcePath, string automaticName)
    {
        return
        [
            Path.GetFileNameWithoutExtension(sourcePath),
            automaticName
        ];
    }

    private void SaveAndClose()
    {
        RefreshRows();
        if (!_okButton.Enabled)
        {
            return;
        }

        ResultFolderNames = _grid.Rows
            .Cast<DataGridViewRow>()
            .ToDictionary(
                GetSourcePath,
                row => Convert.ToString(row.Cells[TargetFolderColumnName].Value) ?? "",
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string GetSourcePath(DataGridViewRow row)
    {
        return row.Tag as string ?? "";
    }
}
