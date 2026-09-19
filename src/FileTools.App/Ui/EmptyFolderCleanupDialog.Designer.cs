using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class EmptyFolderCleanupDialog
{
    private CheckBox _recursive = null!;
    private CheckBox _includeRoots = null!;
    private Button _scanButton = null!;
    private Button _acceptButton = null!;
    private Button _cancelButton = null!;
    private ListView _candidates = null!;
    private TextBox _errors = null!;
    private Label _summary = null!;
    private Label _description = null!;

    private void InitializeComponent()
    {
        Text = "Empty-folder cleanup";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(920, 620);
        MinimumSize = new Size(720, 480);
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 6 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        _description = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "Review empty folders before sending them to the Recycle Bin.", Padding = new Padding(0, 0, 0, 8) };
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _recursive = new CheckBox { AutoSize = true, Checked = true, Text = "Search all subfolders", Margin = new Padding(3, 9, 16, 3) };
        _includeRoots = new CheckBox { AutoSize = true, Text = "Include selected roots", Margin = new Padding(3, 9, 16, 3) };
        _scanButton = new Button { AutoSize = true, Height = 30, Text = "Scan" };
        options.Controls.AddRange([_recursive, _includeRoots, _scanButton]);
        _candidates = new ListView { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false, ShowItemToolTips = true };
        _candidates.Columns.Add("Folder", 635);
        _candidates.Columns.Add("State", 190);
        _errors = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, AccessibleName = "Scan messages" };
        _summary = new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        _acceptButton = new Button { Text = "Add to plan", Width = 150, Height = 30, Enabled = false };
        _cancelButton = new Button { Text = "Cancel", Width = 100, Height = 30, DialogResult = DialogResult.Cancel };
        layout.Controls.Add(_description, 0, 0);
        layout.Controls.Add(options, 0, 1);
        layout.Controls.Add(_candidates, 0, 2);
        layout.Controls.Add(_errors, 0, 3);
        layout.Controls.Add(_summary, 0, 4);
        layout.Controls.Add(DialogButtonPanelFactory.CreateRightAligned(_acceptButton, _cancelButton), 0, 5);
        Controls.Add(layout);
        AcceptButton = _acceptButton;
        CancelButton = _cancelButton;
    }
}
