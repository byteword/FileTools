using System.Globalization;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class FileCompareRequestDialog : Form
{
    private readonly List<string> _paths = [];
    private readonly FileCompareOptions _options;

    private readonly ListBox _targetList = new();
    private readonly Label _targetSummaryLabel = new();
    private readonly Button _addFilesButton = new();
    private readonly Button _addFolderButton = new();
    private readonly Button _removeTargetsButton = new();
    private readonly Button _runButton = new();
    private readonly Button _cancelButton = new();

    private readonly CheckBox _nameCheckBox = new();
    private readonly ComboBox _nameModeCombo = new();
    private readonly CheckBox _createdTimeCheckBox = new();
    private readonly CheckBox _modifiedTimeCheckBox = new();
    private readonly CheckBox _sizeCheckBox = new();
    private readonly CheckBox _contentCheckBox = new();
    private readonly ComboBox _contentModeCombo = new();
    private readonly ComboBox _rangeModeCombo = new();
    private readonly TextBox _rangeBytesBox = new();
    private readonly CheckBox _extractArchivesCheckBox = new();
    private readonly ComboBox _archiveOrderCombo = new();
    private readonly CheckBox _earlyExitCheckBox = new();
    private readonly CheckBox _hashCacheCheckBox = new();
    private readonly TextBox _prefilterPercentBox = new();

    public FileCompareRequestDialog(IEnumerable<string> initialPaths, FileCompareOptions options)
    {
        _options = options.Clone();
        Text = Localizer.Get("FileCompareDialogTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 680);
        MinimumSize = new Size(860, 580);

        BuildLayout();
        ConfigureOptionCombos();
        LoadOptions();
        AddPaths(initialPaths);
        UpdateOptionControlState();
        UpdateTargetSummary();
    }

    public IReadOnlyList<string> SelectedPaths => _paths.ToArray();

    public FileCompareOptions Options => _options.Clone();

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        root.Controls.Add(CreateTargetsGroup(), 0, 0);
        root.Controls.Add(CreateOptionsGroup(), 1, 0);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        _runButton.Text = Localizer.Get("FileCompareDialogRun");
        _runButton.Width = 120;
        _runButton.Height = 30;
        _runButton.Click += (_, _) => RunComparison();
        _cancelButton.Text = Localizer.Get("ButtonCancel");
        _cancelButton.Width = 96;
        _cancelButton.Height = 30;
        _cancelButton.DialogResult = DialogResult.Cancel;
        buttonPanel.Controls.Add(_runButton);
        buttonPanel.Controls.Add(_cancelButton);
        root.Controls.Add(buttonPanel, 0, 1);
        root.SetColumnSpan(buttonPanel, 2);

        CancelButton = _cancelButton;
    }

    private Control CreateTargetsGroup()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FileCompareDialogTargets"),
            Padding = new Padding(10)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        group.Controls.Add(layout);

        _targetList.Dock = DockStyle.Fill;
        _targetList.HorizontalScrollbar = true;
        _targetList.SelectionMode = SelectionMode.MultiExtended;
        _targetList.SelectedIndexChanged += (_, _) => UpdateTargetSummary();
        layout.Controls.Add(_targetList, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        _addFilesButton.Text = Localizer.Get("ButtonAddFiles");
        _addFilesButton.Width = 110;
        _addFilesButton.Click += (_, _) => AddFiles();
        _addFolderButton.Text = Localizer.Get("ButtonAddFolder");
        _addFolderButton.Width = 110;
        _addFolderButton.Click += (_, _) => AddFolder();
        _removeTargetsButton.Text = Localizer.Get("ButtonRemoveSelected");
        _removeTargetsButton.Width = 130;
        _removeTargetsButton.Click += (_, _) => RemoveSelectedTargets();
        buttons.Controls.Add(_addFilesButton);
        buttons.Controls.Add(_addFolderButton);
        buttons.Controls.Add(_removeTargetsButton);
        layout.Controls.Add(buttons, 0, 1);

        _targetSummaryLabel.Dock = DockStyle.Fill;
        _targetSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _targetSummaryLabel.AutoEllipsis = true;
        layout.Controls.Add(_targetSummaryLabel, 0, 2);
        return group;
    }

    private Control CreateOptionsGroup()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FileCompareDialogOptions"),
            Padding = new Padding(10)
        };
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4
        };
        scroll.Controls.Add(stack);
        group.Controls.Add(scroll);

        stack.Controls.Add(CreateFileNameGroup(), 0, 0);
        stack.Controls.Add(CreateMetadataGroup(), 0, 1);
        stack.Controls.Add(CreateContentGroup(), 0, 2);
        stack.Controls.Add(CreateOtherGroup(), 0, 3);
        return group;
    }

    private Control CreateFileNameGroup()
    {
        _nameCheckBox.Text = Localizer.Get("FileCompareCheckFileName");
        _nameCheckBox.AutoSize = true;
        _nameCheckBox.CheckedChanged += (_, _) => UpdateOptionControlState();

        var layout = CreateSectionGroup(Localizer.Get("FileCompareGroupFileName"), out var group);
        layout.Controls.Add(_nameCheckBox, 0, 0);
        layout.Controls.Add(CreateComboRow(Localizer.Get("FileCompareLabelNameMode"), _nameModeCombo), 0, 1);
        return group;
    }

    private Control CreateMetadataGroup()
    {
        _createdTimeCheckBox.Text = Localizer.Get("FileCompareCheckCreatedTime");
        _modifiedTimeCheckBox.Text = Localizer.Get("FileCompareCheckModifiedTime");
        _sizeCheckBox.Text = Localizer.Get("FileCompareCheckFileSize");
        foreach (var checkBox in new[] { _createdTimeCheckBox, _modifiedTimeCheckBox, _sizeCheckBox })
        {
            checkBox.AutoSize = true;
        }

        var layout = CreateSectionGroup(Localizer.Get("FileCompareGroupMetadata"), out var group);
        layout.Controls.Add(_createdTimeCheckBox, 0, 0);
        layout.Controls.Add(_modifiedTimeCheckBox, 0, 1);
        layout.Controls.Add(_sizeCheckBox, 0, 2);
        return group;
    }

    private Control CreateContentGroup()
    {
        _contentCheckBox.Text = Localizer.Get("FileCompareCheckContent");
        _contentCheckBox.AutoSize = true;
        _contentCheckBox.CheckedChanged += (_, _) => UpdateOptionControlState();
        _extractArchivesCheckBox.Text = Localizer.Get("FileCompareCheckExtractArchives");
        _extractArchivesCheckBox.AutoSize = true;
        _extractArchivesCheckBox.CheckedChanged += (_, _) => UpdateOptionControlState();
        _rangeModeCombo.SelectedIndexChanged += (_, _) => UpdateOptionControlState();
        _contentModeCombo.SelectedIndexChanged += (_, _) => UpdateOptionControlState();

        var layout = CreateSectionGroup(Localizer.Get("FileCompareGroupContent"), out var group);
        layout.Controls.Add(_contentCheckBox, 0, 0);
        layout.Controls.Add(CreateComboRow(Localizer.Get("FileCompareLabelContentMode"), _contentModeCombo), 0, 1);
        layout.Controls.Add(CreateComboRow(Localizer.Get("FileCompareLabelRangeMode"), _rangeModeCombo), 0, 2);
        layout.Controls.Add(CreateTextRow(Localizer.Get("FileCompareLabelRangeBytes"), _rangeBytesBox), 0, 3);
        layout.Controls.Add(_extractArchivesCheckBox, 0, 4);
        layout.Controls.Add(CreateComboRow(Localizer.Get("FileCompareLabelArchiveOrder"), _archiveOrderCombo), 0, 5);
        return group;
    }

    private Control CreateOtherGroup()
    {
        _earlyExitCheckBox.Text = Localizer.Get("FileCompareCheckEarlyExit");
        _hashCacheCheckBox.Text = Localizer.Get("FileCompareCheckHashCache");
        foreach (var checkBox in new[] { _earlyExitCheckBox, _hashCacheCheckBox })
        {
            checkBox.AutoSize = true;
        }

        var layout = CreateSectionGroup(Localizer.Get("FileCompareGroupOther"), out var group);
        layout.Controls.Add(_earlyExitCheckBox, 0, 0);
        layout.Controls.Add(_hashCacheCheckBox, 0, 1);
        layout.Controls.Add(CreateTextRow(Localizer.Get("FileCompareLabelPrefilterPercent"), _prefilterPercentBox), 0, 2);
        return group;
    }

    private static TableLayoutPanel CreateSectionGroup(string title, out GroupBox group)
    {
        group = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = title,
            AutoSize = true,
            Padding = new Padding(10)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(0, 4, 0, 4)
        };
        group.Controls.Add(layout);
        return layout;
    }

    private static Control CreateComboRow(string labelText, ComboBox combo)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Width = 240;
        return CreateInputRow(labelText, combo);
    }

    private static Control CreateTextRow(string labelText, TextBox textBox)
    {
        textBox.Width = 140;
        return CreateInputRow(labelText, textBox);
    }

    private static Control CreateInputRow(string labelText, Control input)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 2, 0, 2)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        input.Anchor = AnchorStyles.Left;
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(input, 1, 0);
        return row;
    }

    private void ConfigureOptionCombos()
    {
        ConfigureCombo(_nameModeCombo, Enum.GetValues<FileCompareNameMatchMode>()
            .Select(value => new ComboOption<FileCompareNameMatchMode>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
        ConfigureCombo(_contentModeCombo, Enum.GetValues<FileCompareContentMode>()
            .Select(value => new ComboOption<FileCompareContentMode>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
        ConfigureCombo(_rangeModeCombo, Enum.GetValues<FileCompareRangeMode>()
            .Select(value => new ComboOption<FileCompareRangeMode>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
        ConfigureCombo(_archiveOrderCombo, Enum.GetValues<FileCompareArchiveEntryOrder>()
            .Select(value => new ComboOption<FileCompareArchiveEntryOrder>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
    }

    private void LoadOptions()
    {
        _nameCheckBox.Checked = _options.CompareFileName;
        _createdTimeCheckBox.Checked = _options.CompareCreatedTime;
        _modifiedTimeCheckBox.Checked = _options.CompareModifiedTime;
        _sizeCheckBox.Checked = _options.CompareFileSize;
        _contentCheckBox.Checked = _options.CompareContent;
        _extractArchivesCheckBox.Checked = _options.ArchiveMode == FileCompareArchiveMode.ExtractEntries;
        _earlyExitCheckBox.Checked = _options.EnableEarlyExit;
        _hashCacheCheckBox.Checked = _options.UseHashCache;
        _rangeBytesBox.Text = _options.RangeBytes.ToString(CultureInfo.CurrentCulture);
        _prefilterPercentBox.Text = (_options.ByteToBytePrefilterRatio * 100).ToString("0.##", CultureInfo.CurrentCulture);
        SelectComboValue(_nameModeCombo, _options.NameMatchMode);
        SelectComboValue(_contentModeCombo, _options.ContentMode);
        SelectComboValue(_rangeModeCombo, _options.RangeMode);
        SelectComboValue(_archiveOrderCombo, _options.ArchiveEntryOrder);
    }

    private void AddFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Title = Localizer.Get("FileCompareAddFilesTitle")
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddPaths(dialog.FileNames);
        }
    }

    private void AddFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = Localizer.Get("FileCompareAddFolderDescription"),
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddPaths([dialog.SelectedPath]);
        }
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var existing = _paths.ToHashSet(comparer);
        foreach (var path in paths.Where(static path => File.Exists(path) || Directory.Exists(path)))
        {
            var fullPath = Path.GetFullPath(path);
            if (existing.Add(fullPath))
            {
                _paths.Add(fullPath);
            }
        }

        RefreshTargetList();
    }

    private void RemoveSelectedTargets()
    {
        var selected = _targetList.SelectedItems
            .Cast<string>()
            .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (selected.Count == 0)
        {
            return;
        }

        _paths.RemoveAll(selected.Contains);
        RefreshTargetList();
    }

    private void RefreshTargetList()
    {
        _targetList.BeginUpdate();
        try
        {
            _targetList.Items.Clear();
            foreach (var path in _paths)
            {
                _targetList.Items.Add(path);
            }
        }
        finally
        {
            _targetList.EndUpdate();
        }

        UpdateTargetSummary();
    }

    private void UpdateTargetSummary()
    {
        _targetSummaryLabel.Text = Localizer.Format("FileCompareDialogTargetSummaryFormat", _paths.Count);
        _removeTargetsButton.Enabled = _targetList.SelectedItems.Count > 0;
        _runButton.Enabled = _paths.Count >= 2;
    }

    private void UpdateOptionControlState()
    {
        _nameModeCombo.Enabled = _nameCheckBox.Checked;
        var contentEnabled = _contentCheckBox.Checked;
        _contentModeCombo.Enabled = contentEnabled;
        _rangeModeCombo.Enabled = contentEnabled;
        var rangeMode = GetComboValue(_rangeModeCombo, FileCompareRangeMode.Full);
        _rangeBytesBox.Enabled = contentEnabled && rangeMode != FileCompareRangeMode.Full;
        _extractArchivesCheckBox.Enabled = contentEnabled;
        _archiveOrderCombo.Enabled = contentEnabled && _extractArchivesCheckBox.Checked;
        var contentMode = GetComboValue(_contentModeCombo, FileCompareContentMode.Hash);
        _prefilterPercentBox.Enabled = contentEnabled && contentMode == FileCompareContentMode.ByteToByte;
    }

    private void RunComparison()
    {
        if (_paths.Count < 2)
        {
            MessageBox.Show(
                Localizer.Get("FileCompareNeedsMultipleTargets"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            SaveOptionsFromUi();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void SaveOptionsFromUi()
    {
        _options.CompareFileName = _nameCheckBox.Checked;
        _options.NameMatchMode = GetComboValue(_nameModeCombo, _options.NameMatchMode);
        _options.CompareCreatedTime = _createdTimeCheckBox.Checked;
        _options.CompareModifiedTime = _modifiedTimeCheckBox.Checked;
        _options.CompareFileSize = _sizeCheckBox.Checked;
        _options.CompareContent = _contentCheckBox.Checked;
        _options.ContentMode = GetComboValue(_contentModeCombo, _options.ContentMode);
        _options.RangeMode = GetComboValue(_rangeModeCombo, _options.RangeMode);
        _options.RangeBytes = ParseLong(_rangeBytesBox.Text, 1, long.MaxValue);
        _options.ArchiveMode = _extractArchivesCheckBox.Checked
            ? FileCompareArchiveMode.ExtractEntries
            : FileCompareArchiveMode.AsFile;
        _options.ArchiveEntryOrder = GetComboValue(_archiveOrderCombo, _options.ArchiveEntryOrder);
        _options.EnableEarlyExit = _earlyExitCheckBox.Checked;
        _options.UseHashCache = _hashCacheCheckBox.Checked;
        _options.ByteToBytePrefilterRatio = ParsePercent(_prefilterPercentBox.Text) / 100;
    }

    private static long ParseLong(string text, long minimum, long maximum)
    {
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value))
        {
            throw new InvalidOperationException(Localizer.Format("SettingsInvalidNumberFormat", text));
        }

        return Math.Clamp(value, minimum, maximum);
    }

    private static double ParsePercent(string text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            throw new InvalidOperationException(Localizer.Format("SettingsInvalidNumberFormat", text));
        }

        return Math.Clamp(value, 0, 100);
    }

    private static void ConfigureCombo<T>(ComboBox combo, ComboOption<T>[] options)
    {
        combo.Items.Clear();
        combo.DisplayMember = nameof(ComboOption<T>.Text);
        combo.ValueMember = nameof(ComboOption<T>.Value);
        combo.Items.AddRange(options);
    }

    private static void SelectComboValue<T>(ComboBox combo, T value)
    {
        for (var index = 0; index < combo.Items.Count; index++)
        {
            if (combo.Items[index] is ComboOption<T> option &&
                EqualityComparer<T>.Default.Equals(option.Value, value))
            {
                combo.SelectedIndex = index;
                return;
            }
        }

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private static T GetComboValue<T>(ComboBox combo, T fallback)
    {
        return combo.SelectedItem is ComboOption<T> option ? option.Value : fallback;
    }
}
