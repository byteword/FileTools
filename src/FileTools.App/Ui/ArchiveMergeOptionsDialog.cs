using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class ArchiveMergeOptionsDialog : Form
{
    private const string SourceNameColumnName = "SourceName";
    private const string SourceLocationColumnName = "SourceLocation";
    private const string EntrySourceColumnName = "EntrySource";
    private const string EntryOriginalColumnName = "EntryOriginal";
    private const string EntryTargetColumnName = "EntryTarget";
    private const string EntryStatusColumnName = "EntryStatus";
    private const string EntryReasonColumnName = "EntryReason";

    private readonly TextBox _outputPathBox = new();
    private readonly DataGridView _sourceGrid = new();
    private readonly DataGridView _entryPreviewGrid = new();
    private readonly Label _entryPreviewSummaryLabel = new();
    private readonly ComboBox _layoutCombo = new();
    private readonly ComboBox _collisionCombo = new();
    private readonly ComboBox _duplicateCombo = new();
    private readonly ComboBox _failureCombo = new();
    private readonly ComboBox _compressionCombo = new();
    private readonly CheckBox _deleteOriginalsCheckBox = new();
    private readonly Button _okButton = new();
    private bool _isLoadingOptions;

    public ArchiveMergeOptionsDialog(ArchiveMergeOptions options)
    {
        Options = options.Clone();
        Text = Localizer.Get("ArchiveMergeOptionsDialogTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 720);
        MinimumSize = new Size(820, 580);

        BuildLayout();
        LoadOptions();
        WirePreviewRefreshEvents();
        RefreshEntryPreview();
    }

    public ArchiveMergeOptions Options { get; private set; }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
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
        root.Controls.Add(CreateEntryPreviewPanel(), 0, 4);
        root.Controls.Add(CreateButtonPanel(), 0, 5);
    }

    private Control CreateOutputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Margin = new Padding(0, 4, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
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

        var advancedButton = new Button
        {
            Text = Localizer.Get("ButtonAdvanced"),
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 2)
        };
        advancedButton.Click += (_, _) => OpenAdvancedOutputNameEditor();
        panel.Controls.Add(advancedButton, 3, 0);

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

    private Control CreateEntryPreviewPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("ArchiveMergeEntryPreviewGroup"),
            Padding = new Padding(8)
        };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _entryPreviewSummaryLabel.Dock = DockStyle.Fill;
        _entryPreviewSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _entryPreviewSummaryLabel.ForeColor = Color.FromArgb(71, 85, 105);
        panel.Controls.Add(_entryPreviewSummaryLabel, 0, 0);

        _entryPreviewGrid.Dock = DockStyle.Fill;
        _entryPreviewGrid.AllowUserToAddRows = false;
        _entryPreviewGrid.AllowUserToDeleteRows = false;
        _entryPreviewGrid.AllowUserToResizeRows = false;
        _entryPreviewGrid.BackgroundColor = SystemColors.Window;
        _entryPreviewGrid.BorderStyle = BorderStyle.FixedSingle;
        _entryPreviewGrid.ReadOnly = true;
        _entryPreviewGrid.RowHeadersVisible = false;
        _entryPreviewGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _entryPreviewGrid.ShowCellToolTips = true;
        _entryPreviewGrid.AutoGenerateColumns = false;
        _entryPreviewGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = EntrySourceColumnName,
            HeaderText = Localizer.Get("ArchiveMergeEntryPreviewColumnSource"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 150,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _entryPreviewGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = EntryOriginalColumnName,
            HeaderText = Localizer.Get("ArchiveMergeEntryPreviewColumnOriginal"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 38,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _entryPreviewGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = EntryTargetColumnName,
            HeaderText = Localizer.Get("ArchiveMergeEntryPreviewColumnTarget"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 38,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _entryPreviewGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = EntryStatusColumnName,
            HeaderText = Localizer.Get("ArchiveMergeEntryPreviewColumnStatus"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 132,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _entryPreviewGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = EntryReasonColumnName,
            HeaderText = Localizer.Get("ArchiveMergeEntryPreviewColumnReason"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 24,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        panel.Controls.Add(_entryPreviewGrid, 0, 1);
        group.Controls.Add(panel);
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
        _okButton.Text = "OK";
        _okButton.Width = 92;
        _okButton.Height = 30;
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 92,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        _okButton.Click += (_, _) => SaveAndClose();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(_okButton);
        AcceptButton = _okButton;
        CancelButton = cancelButton;
        return buttons;
    }

    private void LoadOptions()
    {
        _isLoadingOptions = true;
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

        _isLoadingOptions = false;
    }

    private void WirePreviewRefreshEvents()
    {
        _layoutCombo.SelectedIndexChanged += (_, _) => RefreshEntryPreview();
        _collisionCombo.SelectedIndexChanged += (_, _) => RefreshEntryPreview();
        _duplicateCombo.SelectedIndexChanged += (_, _) => RefreshEntryPreview();
        _failureCombo.SelectedIndexChanged += (_, _) => RefreshEntryPreview();
    }

    private void RefreshEntryPreview()
    {
        if (_isLoadingOptions)
        {
            return;
        }

        _entryPreviewGrid.Rows.Clear();
        _entryPreviewSummaryLabel.Text = Localizer.Get("ArchiveMergeEntryPreviewScanning");
        _okButton.Enabled = false;
        var oldCursor = Cursor.Current;
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            var previewOptions = CreateOptionsFromControls(validateOutputPath: false);
            var preview = ArchiveMergeOperations.CreatePreview(previewOptions);
            _entryPreviewSummaryLabel.Text = Localizer.Format(
                "ArchiveMergeEntryPreviewSummaryFormat",
                preview.Entries.Count,
                preview.CollisionRenamedCount,
                preview.SkippedCount,
                preview.BlockedCount);
            foreach (var entry in preview.Entries)
            {
                var rowIndex = _entryPreviewGrid.Rows.Add();
                var row = _entryPreviewGrid.Rows[rowIndex];
                row.Cells[EntrySourceColumnName].Value = Path.GetFileName(entry.SourceArchivePath);
                row.Cells[EntryOriginalColumnName].Value = entry.OriginalPath;
                row.Cells[EntryTargetColumnName].Value = entry.TargetPath;
                row.Cells[EntryStatusColumnName].Value = GetEntryPreviewStatusText(entry.Status);
                row.Cells[EntryReasonColumnName].Value = entry.Reason;
                foreach (var cell in row.Cells.Cast<DataGridViewCell>())
                {
                    cell.ToolTipText = string.Join(
                        Environment.NewLine,
                        entry.SourceArchivePath,
                        entry.OriginalPath + " -> " + entry.TargetPath,
                        entry.Reason);
                }

                ApplyEntryPreviewRowStyle(row, entry.Status);
            }

            if (preview.Entries.Count == 0 && preview.Sources.Count > 0)
            {
                foreach (var source in preview.Sources.Where(static source => source.Status == ArchiveMergePreviewSourceStatus.Blocked))
                {
                    var rowIndex = _entryPreviewGrid.Rows.Add();
                    var row = _entryPreviewGrid.Rows[rowIndex];
                    row.Cells[EntrySourceColumnName].Value = Path.GetFileName(source.SourcePath);
                    row.Cells[EntryStatusColumnName].Value = GetEntryPreviewStatusText(ArchiveMergePreviewEntryStatus.Blocked);
                    row.Cells[EntryReasonColumnName].Value = source.Reason;
                    ApplyEntryPreviewRowStyle(row, ArchiveMergePreviewEntryStatus.Blocked);
                }
            }

            _okButton.Enabled = preview.BlockedCount == 0 && preview.Entries.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _entryPreviewSummaryLabel.Text = Localizer.Format("ArchiveMergeEntryPreviewFailedFormat", ex.Message);
            var rowIndex = _entryPreviewGrid.Rows.Add();
            var row = _entryPreviewGrid.Rows[rowIndex];
            row.Cells[EntryStatusColumnName].Value = GetEntryPreviewStatusText(ArchiveMergePreviewEntryStatus.Blocked);
            row.Cells[EntryReasonColumnName].Value = ex.Message;
            ApplyEntryPreviewRowStyle(row, ArchiveMergePreviewEntryStatus.Blocked);
            _okButton.Enabled = false;
        }
        finally
        {
            Cursor.Current = oldCursor;
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

    private void OpenAdvancedOutputNameEditor()
    {
        var outputPath = _outputPathBox.Text.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Options.OutputPath;
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Environment.CurrentDirectory;
        }

        var fileName = Path.GetFileName(outputPath);
        var automaticName = Path.GetFileName(Options.OutputPath);
        var edited = AdvancedNameEditDialog.EditName(
            this,
            Localizer.Get("AdvancedNameDialogTitle"),
            Localizer.Get("AdvancedNameArchiveMergeHeader"),
            new NameEditRequest(
                OriginalName: automaticName,
                SuggestedName: fileName,
                AutomaticName: automaticName,
                RequiredExtension: ".zip",
                Recommendations: BuildNameRecommendations()));
        if (edited is not null)
        {
            _outputPathBox.Text = Path.Combine(directory, edited);
            RefreshEntryPreview();
        }
    }

    private IReadOnlyList<string> BuildNameRecommendations()
    {
        var recommendations = new List<string>();
        recommendations.AddRange(Options.SourcePaths.Select(static path => Path.GetFileNameWithoutExtension(path)));
        recommendations.AddRange(Options.SourcePaths.Select(static path => Path.GetFileName(path)));
        return recommendations;
    }

    private void SaveAndClose()
    {
        try
        {
            Options = CreateOptionsFromControls(validateOutputPath: true);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private ArchiveMergeOptions CreateOptionsFromControls(bool validateOutputPath)
    {
        var outputPath = _outputPathBox.Text.Trim().Trim('"');
        if (validateOutputPath && string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException(Localizer.Get("ArchiveMergeOutputPathRequired"));
        }

        var options = Options.Clone();
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            options.OutputPath = Path.GetFullPath(outputPath);
        }

        if (_layoutCombo.SelectedItem is ComboOption<ArchiveMergeLayout> layout)
        {
            options.Layout = layout.Value;
        }

        if (_collisionCombo.SelectedItem is ComboOption<ArchiveMergeCollisionPolicy> collision)
        {
            options.CollisionPolicy = collision.Value;
        }

        if (_duplicateCombo.SelectedItem is ComboOption<ArchiveMergeDuplicatePolicy> duplicate)
        {
            options.DuplicatePolicy = duplicate.Value;
        }

        if (_failureCombo.SelectedItem is ComboOption<ArchiveMergeFailurePolicy> failure)
        {
            options.FailurePolicy = failure.Value;
        }

        if (_compressionCombo.SelectedItem is ComboOption<ArchiveMergeCompressionLevel> compression)
        {
            options.CompressionLevel = compression.Value;
        }

        options.DeleteOriginals = _deleteOriginalsCheckBox.Checked;
        return options;
    }

    private static string GetEntryPreviewStatusText(ArchiveMergePreviewEntryStatus status)
    {
        return status switch
        {
            ArchiveMergePreviewEntryStatus.CollisionRenamed => Localizer.Get("ArchiveMergeEntryPreviewStatusCollisionRenamed"),
            ArchiveMergePreviewEntryStatus.DuplicateSkipped => Localizer.Get("ArchiveMergeEntryPreviewStatusDuplicateSkipped"),
            ArchiveMergePreviewEntryStatus.Skipped => Localizer.Get("ArchiveMergeEntryPreviewStatusSkipped"),
            ArchiveMergePreviewEntryStatus.Blocked => Localizer.Get("ArchiveMergeEntryPreviewStatusBlocked"),
            _ => Localizer.Get("ArchiveMergeEntryPreviewStatusReady")
        };
    }

    private static void ApplyEntryPreviewRowStyle(DataGridViewRow row, ArchiveMergePreviewEntryStatus status)
    {
        if (status == ArchiveMergePreviewEntryStatus.CollisionRenamed)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 251, 235);
            return;
        }

        if (status is ArchiveMergePreviewEntryStatus.Blocked)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
            return;
        }

        if (status is ArchiveMergePreviewEntryStatus.Skipped or ArchiveMergePreviewEntryStatus.DuplicateSkipped)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            return;
        }

        row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
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
