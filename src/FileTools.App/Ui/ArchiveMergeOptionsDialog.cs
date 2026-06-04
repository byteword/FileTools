using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class ArchiveMergeOptionsDialog : Form
{
    private const string SourceNameColumnName = "SourceName";
    private const string SourceLocationColumnName = "SourceLocation";

    private readonly TextBox _outputPathBox = new();
    private readonly DataGridView _sourceGrid = new();
    private readonly ComboBox _layoutCombo = new();
    private readonly ComboBox _collisionCombo = new();
    private readonly ComboBox _duplicateCombo = new();
    private readonly ComboBox _failureCombo = new();
    private readonly ComboBox _compressionCombo = new();
    private readonly CheckBox _deleteOriginalsCheckBox = new();

    public ArchiveMergeOptionsDialog(ArchiveMergeOptions options)
    {
        Options = options.Clone();
        Text = Localizer.Get("ArchiveMergeOptionsDialogTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(860, 560);
        MinimumSize = new Size(740, 460);

        BuildLayout();
        LoadOptions();
    }

    public ArchiveMergeOptions Options { get; private set; }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("ArchiveMergeOptionsHeader"),
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        root.Controls.Add(CreateOutputPanel(), 0, 1);
        root.Controls.Add(CreatePolicyPanel(), 0, 2);
        root.Controls.Add(CreateSourcePanel(), 0, 3);
        root.Controls.Add(CreateButtonPanel(), 0, 4);
    }

    private Control CreateOutputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Margin = new Padding(0, 4, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        panel.Controls.Add(new Label
        {
            Text = Localizer.Get("ArchiveMergeLabelOutputPath"),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _outputPathBox.Dock = DockStyle.Fill;
        _outputPathBox.Margin = new Padding(0, 2, 8, 0);
        panel.Controls.Add(_outputPathBox, 1, 0);

        var browseButton = new Button
        {
            Text = Localizer.Get("ButtonBrowse"),
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 2)
        };
        browseButton.Click += (_, _) => BrowseOutputPath();
        panel.Controls.Add(browseButton, 2, 0);

        var help = new Label
        {
            Text = Localizer.Get("ArchiveMergeOutputHelp"),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.SetColumnSpan(help, 2);
        panel.Controls.Add(help, 1, 1);
        return panel;
    }

    private Control CreatePolicyPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            Margin = new Padding(0, 0, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        AddComboRow(panel, 0, Localizer.Get("ArchiveMergeLabelLayout"), _layoutCombo);
        AddComboRow(panel, 1, Localizer.Get("ArchiveMergeLabelCollision"), _collisionCombo);
        AddComboRow(panel, 2, Localizer.Get("ArchiveMergeLabelDuplicate"), _duplicateCombo);
        AddComboRow(panel, 3, Localizer.Get("ArchiveMergeLabelFailure"), _failureCombo);
        AddComboRow(panel, 4, Localizer.Get("ArchiveMergeLabelCompression"), _compressionCombo);

        _deleteOriginalsCheckBox.Text = Localizer.Get("ArchiveMergeCheckDeleteOriginals");
        _deleteOriginalsCheckBox.Dock = DockStyle.Fill;
        _deleteOriginalsCheckBox.TextAlign = ContentAlignment.MiddleLeft;
        panel.SetColumnSpan(_deleteOriginalsCheckBox, 2);
        panel.Controls.Add(_deleteOriginalsCheckBox, 2, 2);

        return panel;
    }

    private Control CreateSourcePanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("ArchiveMergeSourcesGroup"),
            Padding = new Padding(8)
        };
        _sourceGrid.Dock = DockStyle.Fill;
        _sourceGrid.AllowUserToAddRows = false;
        _sourceGrid.AllowUserToDeleteRows = false;
        _sourceGrid.AllowUserToResizeRows = false;
        _sourceGrid.BackgroundColor = SystemColors.Window;
        _sourceGrid.BorderStyle = BorderStyle.FixedSingle;
        _sourceGrid.ReadOnly = true;
        _sourceGrid.RowHeadersVisible = false;
        _sourceGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _sourceGrid.ShowCellToolTips = true;
        _sourceGrid.AutoGenerateColumns = false;
        _sourceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = SourceNameColumnName,
            HeaderText = Localizer.Get("ColumnTargetName"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 260,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _sourceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = SourceLocationColumnName,
            HeaderText = Localizer.Get("ColumnTargetLocation"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        group.Controls.Add(_sourceGrid);
        return group;
    }

    private Control CreateButtonPanel()
    {
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
        okButton.Click += (_, _) => SaveAndClose();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return buttons;
    }

    private void LoadOptions()
    {
        _outputPathBox.Text = Options.OutputPath;
        ConfigureCombo(_layoutCombo, Enum.GetValues<ArchiveMergeLayout>()
            .Select(value => new ComboOption<ArchiveMergeLayout>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        ConfigureCombo(_collisionCombo, Enum.GetValues<ArchiveMergeCollisionPolicy>()
            .Select(value => new ComboOption<ArchiveMergeCollisionPolicy>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        ConfigureCombo(_duplicateCombo, Enum.GetValues<ArchiveMergeDuplicatePolicy>()
            .Select(value => new ComboOption<ArchiveMergeDuplicatePolicy>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        ConfigureCombo(_failureCombo, Enum.GetValues<ArchiveMergeFailurePolicy>()
            .Select(value => new ComboOption<ArchiveMergeFailurePolicy>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        ConfigureCombo(_compressionCombo, Enum.GetValues<ArchiveMergeCompressionLevel>()
            .Select(value => new ComboOption<ArchiveMergeCompressionLevel>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_layoutCombo, Options.Layout);
        SelectComboValue(_collisionCombo, Options.CollisionPolicy);
        SelectComboValue(_duplicateCombo, Options.DuplicatePolicy);
        SelectComboValue(_failureCombo, Options.FailurePolicy);
        SelectComboValue(_compressionCombo, Options.CompressionLevel);
        _deleteOriginalsCheckBox.Checked = Options.DeleteOriginals;

        foreach (var sourcePath in Options.SourcePaths)
        {
            var rowIndex = _sourceGrid.Rows.Add();
            var row = _sourceGrid.Rows[rowIndex];
            row.Cells[SourceNameColumnName].Value = Path.GetFileName(sourcePath);
            row.Cells[SourceLocationColumnName].Value = Path.GetDirectoryName(sourcePath) ?? "";
            foreach (var cell in row.Cells.Cast<DataGridViewCell>())
            {
                cell.ToolTipText = sourcePath;
            }
        }
    }

    private void BrowseOutputPath()
    {
        using var dialog = new SaveFileDialog
        {
            Title = Localizer.Get("ArchiveMergeOutputDialogTitle"),
            Filter = Localizer.Get("ArchiveMergeOutputDialogFilter"),
            FileName = Path.GetFileName(_outputPathBox.Text),
            InitialDirectory = Path.GetDirectoryName(_outputPathBox.Text)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputPathBox.Text = dialog.FileName;
        }
    }

    private void SaveAndClose()
    {
        try
        {
            var outputPath = _outputPathBox.Text.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                MessageBox.Show(Localizer.Get("ArchiveMergeOutputPathRequired"), FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Options.OutputPath = Path.GetFullPath(outputPath);
            if (_layoutCombo.SelectedItem is ComboOption<ArchiveMergeLayout> layout)
            {
                Options.Layout = layout.Value;
            }

            if (_collisionCombo.SelectedItem is ComboOption<ArchiveMergeCollisionPolicy> collision)
            {
                Options.CollisionPolicy = collision.Value;
            }

            if (_duplicateCombo.SelectedItem is ComboOption<ArchiveMergeDuplicatePolicy> duplicate)
            {
                Options.DuplicatePolicy = duplicate.Value;
            }

            if (_failureCombo.SelectedItem is ComboOption<ArchiveMergeFailurePolicy> failure)
            {
                Options.FailurePolicy = failure.Value;
            }

            if (_compressionCombo.SelectedItem is ComboOption<ArchiveMergeCompressionLevel> compression)
            {
                Options.CompressionLevel = compression.Value;
            }

            Options.DeleteOriginals = _deleteOriginalsCheckBox.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void AddComboRow(TableLayoutPanel panel, int flatIndex, string labelText, ComboBox combo)
    {
        var row = flatIndex / 2;
        var labelColumn = flatIndex % 2 == 0 ? 0 : 2;
        var comboColumn = labelColumn + 1;
        panel.Controls.Add(new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, labelColumn, row);
        combo.Dock = DockStyle.Fill;
        combo.Margin = new Padding(0, 6, 12, 4);
        panel.Controls.Add(combo, comboColumn, row);
    }

    private static void ConfigureCombo<T>(ComboBox combo, ComboOption<T>[] options)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DataSource = options;
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

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }
}

