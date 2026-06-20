using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace FileTools;

internal sealed class FolderMergeOptionsDialog : Form
{
    private const int DialogClientWidth = 760;
    private const int DialogClientHeight = 520;

    private readonly IReadOnlyList<string> _sourcePaths;
    private readonly FileToolsSettings _settings;
    private readonly bool _allowFolderContentsMode;
    private readonly TextBox _targetFolderNameBox = new();
    private readonly Label _targetFolderPathLabel = new();
    private readonly RadioButton _mergeFolderUnitsRadio = new();
    private readonly RadioButton _mergeFolderContentsRadio = new();
    private readonly Label _modeHelpLabel = new();
    private readonly Label _statusLabel = new();
    private readonly ListView _sourceListView = new();
    private readonly ToolTip _toolTip = new();
    private readonly Button _okButton = new();
    private FolderMergePlanPreview _preview;

    public FolderMergeOptionsDialog(
        IReadOnlyList<string> sourcePaths,
        FileToolsSettings settings,
        FolderMergeOptions options,
        bool allowFolderContentsMode)
    {
        _sourcePaths = sourcePaths;
        _settings = settings;
        _allowFolderContentsMode = allowFolderContentsMode;

        var normalizedMode = NormalizeMode(options.Mode, allowFolderContentsMode);
        var normalizedOptions = new FolderMergeOptions(options.TargetFolderName, normalizedMode);
        _preview = FolderMergeOperations.CreateMergePlanPreview(_sourcePaths, _settings, normalizedOptions);

        Text = Localizer.Get("FolderMergeOptionsDialogTitle");
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(DialogClientWidth, DialogClientHeight);
        MinimumSize = new Size(700, 480);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AcceptButton = _okButton;
        AutoScaleMode = AutoScaleMode.Font;

        BuildLayout(normalizedOptions);
        WireEvents();
        RefreshStatus();
    }

    public FolderMergeOptions ResultOptions { get; private set; } = FolderMergeOptionDefaults.MergeFolders;

    private void BuildLayout(FolderMergeOptions options)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FolderMergeDialogHeader"),
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        root.Controls.Add(CreateTargetNamePanel(options.TargetFolderName), 0, 1);
        root.Controls.Add(CreateModePanel(options.Mode), 0, 2);
        root.Controls.Add(CreateSourceListPanel(), 0, 3);
        root.Controls.Add(CreateButtonPanel(), 0, 4);
        Controls.Add(root);
    }

    private Control CreateTargetNamePanel(string? initialName)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Margin = new Padding(0, 2, 0, 6)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

        panel.Controls.Add(CreateRowLabel(Localizer.Get("FolderMergeTargetName")), 0, 0);

        _targetFolderNameBox.Dock = DockStyle.Fill;
        _targetFolderNameBox.Margin = new Padding(0, 2, 0, 0);
        _targetFolderNameBox.Text = initialName ?? string.Empty;
        panel.Controls.Add(_targetFolderNameBox, 1, 0);

        panel.Controls.Add(CreateRowLabel(Localizer.Get("FolderMergeTargetPath")), 0, 1);

        _targetFolderPathLabel.Dock = DockStyle.Fill;
        _targetFolderPathLabel.ForeColor = Color.FromArgb(71, 85, 105);
        _targetFolderPathLabel.TextAlign = ContentAlignment.MiddleLeft;
        _targetFolderPathLabel.AutoEllipsis = true;
        panel.Controls.Add(_targetFolderPathLabel, 1, 1);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(_statusLabel, 1, 2);

        return panel;
    }

    private Control CreateModePanel(FolderMergeMode mode)
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FolderMergeModeGroup"),
            Padding = new Padding(10, 20, 10, 8),
            Margin = new Padding(0, 0, 0, 8)
        };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _mergeFolderUnitsRadio.Text = Localizer.Get("FolderMergeModeMergeFolders");
        _mergeFolderUnitsRadio.Dock = DockStyle.Fill;
        _mergeFolderUnitsRadio.Margin = new Padding(0);
        _mergeFolderUnitsRadio.TextAlign = ContentAlignment.MiddleLeft;
        _mergeFolderUnitsRadio.Checked = mode == FolderMergeMode.MergeFolderUnits;
        panel.Controls.Add(_mergeFolderUnitsRadio, 0, 0);

        _mergeFolderContentsRadio.Text = Localizer.Get("FolderMergeModeMergeContentsOnly");
        _mergeFolderContentsRadio.Dock = DockStyle.Fill;
        _mergeFolderContentsRadio.Margin = new Padding(0);
        _mergeFolderContentsRadio.TextAlign = ContentAlignment.MiddleLeft;
        _mergeFolderContentsRadio.Enabled = _allowFolderContentsMode;
        _mergeFolderContentsRadio.Checked =
            _allowFolderContentsMode && mode == FolderMergeMode.MergeFolderContentsOnly;
        panel.Controls.Add(_mergeFolderContentsRadio, 0, 1);

        _modeHelpLabel.Dock = DockStyle.Fill;
        _modeHelpLabel.ForeColor = Color.FromArgb(71, 85, 105);
        _modeHelpLabel.AutoEllipsis = true;
        _modeHelpLabel.TextAlign = ContentAlignment.MiddleLeft;
        _modeHelpLabel.Text = _allowFolderContentsMode
            ? Localizer.Get("FolderMergeModeContentsHelp")
            : Localizer.Get("FolderMergeModeContentsDisabledHelp");
        panel.Controls.Add(_modeHelpLabel, 0, 2);

        group.Controls.Add(panel);
        return group;
    }

    private Control CreateSourceListPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FolderMergeSelectedSourcesHeader"),
            Padding = new Padding(8, 20, 8, 8),
            Margin = new Padding(0)
        };

        _sourceListView.Dock = DockStyle.Fill;
        _sourceListView.View = View.Details;
        _sourceListView.FullRowSelect = true;
        _sourceListView.GridLines = true;
        _sourceListView.HideSelection = false;
        _sourceListView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _sourceListView.ShowItemToolTips = true;
        _sourceListView.Columns.Add(Localizer.Get("FolderMergeColumnSourceName"), 260);
        _sourceListView.Columns.Add(Localizer.Get("FolderMergeColumnKind"), 92);
        _sourceListView.Columns.Add(Localizer.Get("FolderMergeColumnTargetName"), 330);
        _sourceListView.Resize += (_, _) => AdjustListColumns();
        group.Controls.Add(_sourceListView);
        return group;
    }

    private Control CreateButtonPanel()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 95,
            Height = 30
        };

        _okButton.Text = Localizer.Get("ButtonOK");
        _okButton.Width = 95;
        _okButton.Height = 30;

        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(_okButton);

        _okButton.Click += (_, _) => SaveAndClose();
        CancelButton = cancelButton;
        return buttons;
    }

    private static Label CreateRowLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private void WireEvents()
    {
        _targetFolderNameBox.TextChanged += (_, _) => RefreshStatus();
        _mergeFolderUnitsRadio.CheckedChanged += (_, _) => RefreshStatus();
        _mergeFolderContentsRadio.CheckedChanged += (_, _) => RefreshStatus();
    }

    private void RefreshStatus()
    {
        _preview = FolderMergeOperations.CreateMergePlanPreview(
            _sourcePaths,
            _settings,
            BuildOptionsFromInputs());

        if (!_preview.IsReady || string.IsNullOrWhiteSpace(_preview.TargetFolderPath))
        {
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = _preview.FailureReason ?? Localizer.Get("PlanPreviewUnavailable");
            _okButton.Enabled = false;
            _targetFolderPathLabel.Text = string.Empty;
            _toolTip.SetToolTip(_targetFolderPathLabel, null);
            LoadSourceList();
            return;
        }

        _okButton.Enabled = true;
        _targetFolderPathLabel.Text = _preview.TargetFolderPath;
        _toolTip.SetToolTip(_targetFolderPathLabel, _preview.TargetFolderPath);

        var statusParts = new List<string>
        {
            Localizer.Format("FolderMergeStatusReadyFormat", _sourcePaths.Count)
        };
        if (_preview.HasMultipleParents)
        {
            statusParts.Add(Localizer.Get("FolderMergeMultiParentWarning"));
        }

        _statusLabel.ForeColor = _preview.HasMultipleParents
            ? Color.FromArgb(146, 64, 14)
            : Color.FromArgb(55, 65, 81);
        _statusLabel.Text = string.Join(" ", statusParts);
        _toolTip.SetToolTip(_statusLabel, _statusLabel.Text);
        LoadSourceList();
    }

    private void LoadSourceList()
    {
        _sourceListView.BeginUpdate();
        _sourceListView.Items.Clear();
        var mode = BuildModeFromInputs();
        foreach (var sourcePath in _sourcePaths)
        {
            var sourceName = Path.GetFileName(sourcePath);
            var isFolder = Directory.Exists(sourcePath);
            var item = new ListViewItem(string.IsNullOrWhiteSpace(sourceName) ? sourcePath : sourceName);
            item.SubItems.Add(isFolder
                ? Localizer.Get("FolderMergeSourceKindFolder")
                : Localizer.Get("FolderMergeSourceKindFile"));
            item.SubItems.Add(CreateTargetDisplay(sourcePath, mode));
            item.ToolTipText = CreateItemToolTip(sourcePath, mode);
            _sourceListView.Items.Add(item);
        }

        _sourceListView.EndUpdate();
        AdjustListColumns();
    }

    private string CreateTargetDisplay(string sourcePath, FolderMergeMode mode)
    {
        if (string.IsNullOrWhiteSpace(_preview.TargetFolderPath))
        {
            return "";
        }

        var targetFolderName = Path.GetFileName(_preview.TargetFolderPath);
        var sourceName = Path.GetFileName(sourcePath);
        if (Directory.Exists(sourcePath))
        {
            return mode == FolderMergeMode.MergeFolderContentsOnly
                ? targetFolderName + @"\*"
                : targetFolderName + @"\" + sourceName + @"\";
        }

        return targetFolderName + @"\" + sourceName;
    }

    private string CreateItemToolTip(string sourcePath, FolderMergeMode mode)
    {
        if (string.IsNullOrWhiteSpace(_preview.TargetFolderPath))
        {
            return sourcePath;
        }

        var targetDisplay = CreateTargetDisplay(sourcePath, mode);
        return sourcePath + Environment.NewLine + _preview.TargetFolderPath + Environment.NewLine + targetDisplay;
    }

    private void AdjustListColumns()
    {
        if (_sourceListView.Columns.Count < 3 || _sourceListView.ClientSize.Width <= 0)
        {
            return;
        }

        var width = Math.Max(360, _sourceListView.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
        _sourceListView.Columns[1].Width = 92;
        _sourceListView.Columns[0].Width = Math.Max(180, width / 3);
        _sourceListView.Columns[2].Width = Math.Max(180, width - _sourceListView.Columns[0].Width - _sourceListView.Columns[1].Width);
    }

    private FolderMergeOptions BuildOptionsFromInputs()
    {
        var targetFolderName = string.IsNullOrWhiteSpace(_targetFolderNameBox.Text)
            ? null
            : _targetFolderNameBox.Text;
        var mode = BuildModeFromInputs();
        return new FolderMergeOptions(targetFolderName, mode);
    }

    private void SaveAndClose()
    {
        RefreshStatus();
        if (!_preview.IsReady || string.IsNullOrWhiteSpace(_preview.TargetFolderPath))
        {
            return;
        }

        ResultOptions = new FolderMergeOptions(_preview.TargetFolderName, BuildModeFromInputs());
        DialogResult = DialogResult.OK;
        Close();
    }

    private FolderMergeMode BuildModeFromInputs()
    {
        return _mergeFolderContentsRadio.Checked && _allowFolderContentsMode
            ? FolderMergeMode.MergeFolderContentsOnly
            : FolderMergeMode.MergeFolderUnits;
    }

    private static FolderMergeMode NormalizeMode(FolderMergeMode mode, bool allowFolderContentsMode)
    {
        return allowFolderContentsMode && mode == FolderMergeMode.MergeFolderContentsOnly
            ? FolderMergeMode.MergeFolderContentsOnly
            : FolderMergeMode.MergeFolderUnits;
    }
}
