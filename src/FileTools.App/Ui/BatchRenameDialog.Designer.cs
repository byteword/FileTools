using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class BatchRenameDialog
{
    private TextBox _findBox = null!, _replaceBox = null!, _removePrefixBox = null!, _removeSuffixBox = null!;
    private TextBox _prefixBox = null!, _suffixBox = null!, _patternBox = null!;
    private CheckBox _preserveExtension = null!, _ignoreCase = null!;
    private NumericUpDown _startNumber = null!, _incrementNumber = null!, _digitsNumber = null!;
    private ComboBox _orderCombo = null!;
    private DataGridView _previewGrid = null!;
    private Label _help = null!, _summary = null!;
    private Button _acceptButton = null!, _cancelButton = null!;
    private readonly Dictionary<string, Label> _fieldLabels = [];

    private void InitializeComponent()
    {
        Text = "Batch filename editing";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1080, 740);
        MinimumSize = new Size(880, 600);
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 7 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        _help = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 8), Text = "Review the results before adding the changes to the work plan." };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var row = 0; row < 4; row++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        _findBox = AddField(fields, "BatchRenameFind", "Find", 0, 0);
        _replaceBox = AddField(fields, "BatchRenameReplace", "Replace with", 2, 0);
        _removePrefixBox = AddField(fields, "BatchRenameRemovePrefix", "Remove prefix", 0, 1);
        _removeSuffixBox = AddField(fields, "BatchRenameRemoveSuffix", "Remove suffix", 2, 1);
        _prefixBox = AddField(fields, "BatchRenamePrefix", "Add prefix", 0, 2);
        _suffixBox = AddField(fields, "BatchRenameSuffix", "Add suffix", 2, 2);
        _patternBox = AddField(fields, "BatchRenamePattern", "Name pattern", 0, 3);
        _patternBox.Text = "{Stem}";
        fields.SetColumnSpan(_patternBox, 3);
        var flags = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _preserveExtension = new CheckBox { AutoSize = true, Text = "Preserve extension", Checked = true, Margin = new Padding(3, 7, 20, 3) };
        _ignoreCase = new CheckBox { AutoSize = true, Text = "Ignore case", Checked = true, Margin = new Padding(3, 7, 20, 3) };
        flags.Controls.AddRange([_preserveExtension, _ignoreCase]);
        var numbers = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        _orderCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 162 };
        _startNumber = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = 1, Width = 95 };
        _incrementNumber = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue, Value = 1, Width = 85 };
        _digitsNumber = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 3, Width = 55 };
        AddNumberField(numbers, "BatchRenameOrder", "Order", _orderCombo);
        AddNumberField(numbers, "BatchRenameStart", "Start", _startNumber);
        AddNumberField(numbers, "BatchRenameIncrement", "Increment", _incrementNumber);
        AddNumberField(numbers, "BatchRenameDigits", "Digits", _digitsNumber);
        _previewGrid = new DataGridView
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, AutoGenerateColumns = false, RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            BackgroundColor = SystemColors.Window, ScrollBars = ScrollBars.Both
        };
        _previewGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Include", HeaderText = "Use", Width = 54 });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Before", HeaderText = "Original name", Width = 235, ReadOnly = true });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "After", HeaderText = "New name", Width = 250, ReadOnly = true });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Number", HeaderText = "Number", Width = 80, ReadOnly = true });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "State", HeaderText = "State", Width = 220, ReadOnly = true });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "Location", Width = 380, ReadOnly = true });
        foreach (DataGridViewColumn column in _previewGrid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        _summary = new Label { Dock = DockStyle.Fill, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        _acceptButton = new Button { Text = "Add to work plan", Width = 150, Height = 30, Enabled = false };
        _cancelButton = new Button { Text = "Cancel", Width = 100, Height = 30, DialogResult = DialogResult.Cancel };
        layout.Controls.Add(_help, 0, 0);
        layout.Controls.Add(fields, 0, 1);
        layout.Controls.Add(flags, 0, 2);
        layout.Controls.Add(numbers, 0, 3);
        layout.Controls.Add(_previewGrid, 0, 4);
        layout.Controls.Add(_summary, 0, 5);
        layout.Controls.Add(DialogButtonPanelFactory.CreateRightAligned(_acceptButton, _cancelButton), 0, 6);
        Controls.Add(layout);
        AcceptButton = _acceptButton;
        CancelButton = _cancelButton;
    }

    private TextBox AddField(TableLayoutPanel panel, string key, string label, int column, int row)
    {
        var caption = new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Text = label };
        _fieldLabels.Add(key, caption);
        var box = new TextBox { Dock = DockStyle.Fill, MaxLength = 255, Margin = new Padding(3, 5, 12, 3) };
        panel.Controls.Add(caption, column, row);
        panel.Controls.Add(box, column + 1, row);
        return box;
    }

    private void AddNumberField(FlowLayoutPanel panel, string key, string label, Control input)
    {
        var caption = new Label { AutoSize = true, Text = label, Margin = new Padding(3, 7, 6, 3) };
        _fieldLabels.Add(key, caption);
        input.Margin = new Padding(3, 3, 16, 3);
        panel.Controls.Add(caption);
        panel.Controls.Add(input);
    }
}
