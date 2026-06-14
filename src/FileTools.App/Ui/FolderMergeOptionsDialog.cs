using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace FileTools;

internal sealed class FolderMergeOptionsDialog : Form
{
    private const int DialogClientWidth = 584;
    private const int DialogClientHeight = 380;

    private readonly IReadOnlyList<string> _sourcePaths;
    private readonly FileToolsSettings _settings;
    private readonly bool _allowFolderContentsMode;
    private readonly TextBox _targetFolderNameBox = new();
    private readonly Label _targetFolderPathLabel = new();
    private readonly RadioButton _mergeFolderUnitsRadio = new();
    private readonly RadioButton _mergeFolderContentsRadio = new();
    private readonly Label _messageLabel = new();
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
        MinimumSize = new Size(600, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
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
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FolderMergeDialogHeader"),
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        root.Controls.Add(CreateTargetNamePanel(options.TargetFolderName), 0, 1);
        root.Controls.Add(CreateModePanel(options.Mode), 0, 2);
        root.Controls.Add(CreateMessagePanel(), 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
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
        root.Controls.Add(buttons, 0, 4);
        root.SetColumnSpan(buttons, 1);
        Controls.Add(root);
    }

    private Control CreateTargetNamePanel(string? initialName)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 4, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FolderMergeTargetName"),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _targetFolderNameBox.Dock = DockStyle.Fill;
        _targetFolderNameBox.Margin = new Padding(0, 2, 0, 0);
        _targetFolderNameBox.Text = initialName ?? string.Empty;
        panel.Controls.Add(_targetFolderNameBox, 1, 0);

        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FolderMergeTargetPath"),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);

        _targetFolderPathLabel.Dock = DockStyle.Fill;
        _targetFolderPathLabel.ForeColor = Color.FromArgb(71, 85, 105);
        _targetFolderPathLabel.TextAlign = ContentAlignment.MiddleLeft;
        _targetFolderPathLabel.AutoEllipsis = true;
        panel.Controls.Add(_targetFolderPathLabel, 1, 1);

        return panel;
    }

    private Control CreateModePanel(FolderMergeMode mode)
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FolderMergeModeGroup"),
            Padding = new Padding(8, 20, 8, 8)
        };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

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
        _mergeFolderContentsRadio.Visible = _allowFolderContentsMode;
        _mergeFolderContentsRadio.Checked =
            _allowFolderContentsMode && mode == FolderMergeMode.MergeFolderContentsOnly;
        panel.Controls.Add(_mergeFolderContentsRadio, 0, 1);

        group.Controls.Add(panel);
        return group;
    }

    private Control CreateMessagePanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle
        };
        _messageLabel.Dock = DockStyle.Fill;
        _messageLabel.Padding = new Padding(8);
        _messageLabel.TextAlign = ContentAlignment.TopLeft;
        _messageLabel.AutoSize = false;
        _messageLabel.AutoEllipsis = true;
        panel.Controls.Add(_messageLabel);
        return panel;
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
            _messageLabel.ForeColor = Color.Firebrick;
            _messageLabel.Text = _preview.FailureReason ?? Localizer.Get("PlanPreviewUnavailable");
            _okButton.Enabled = false;
            _targetFolderPathLabel.Text = string.Empty;
            return;
        }

        _okButton.Enabled = true;
        _targetFolderPathLabel.Text = _preview.TargetFolderPath;

        var messageParts = new List<string>
        {
            Localizer.Format("FolderMergeConfirmFormat", _sourcePaths.Count, _preview.TargetFolderPath),
            _preview.TargetParentPath is not null ? Localizer.Format("FolderMergeTargetParentFormat", _preview.TargetParentPath) : string.Empty
        };
        if (_preview.HasMultipleParents)
        {
            messageParts.Add(Localizer.Get("FolderMergeMultiParentWarning"));
        }

        _messageLabel.ForeColor = Color.FromArgb(55, 65, 81);
        _messageLabel.Text = string.Join(
            Environment.NewLine,
            messageParts.Where(static text => !string.IsNullOrWhiteSpace(text)));
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
