using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class StringListEditorDialog : Form
{
    private readonly BindingList<string> _items;
    private readonly ListBox _list = new();
    private readonly TextBox _textBox = new();
    private readonly Label _statusLabel = new();

    public StringListEditorDialog(string title, string itemLabel, IEnumerable<string> items)
    {
        _items = new BindingList<string>(items
            .Select(static item => item.Trim())
            .Where(static item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList());

        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        Width = 520;
        Height = 420;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        BuildLayout(itemLabel);
    }

    public IReadOnlyList<string> Items => _items
        .Select(static item => item.Trim())
        .Where(static item => item.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private void BuildLayout(string itemLabel)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);

        _list.Dock = DockStyle.Fill;
        _list.DataSource = _items;
        _list.SelectedIndexChanged += (_, _) =>
        {
            _textBox.Text = _list.SelectedItem as string ?? "";
            ClearStatus();
        };
        root.Controls.Add(_list, 0, 0);

        var editRow = new Panel { Dock = DockStyle.Fill };
        var label = new Label
        {
            Text = itemLabel,
            Left = 0,
            Top = 8,
            Width = 120,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        editRow.Controls.Add(label);
        _textBox.Left = 128;
        _textBox.Top = 6;
        _textBox.Width = 340;
        _textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        editRow.Controls.Add(_textBox);
        editRow.Resize += (_, _) =>
        {
            var labelWidth = Math.Clamp(editRow.ClientSize.Width / 3, 100, 160);
            label.Width = labelWidth;
            _textBox.Left = labelWidth + 8;
            _textBox.Width = Math.Max(180, editRow.ClientSize.Width - _textBox.Left);
        };
        root.Controls.Add(editRow, 0, 1);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(128, 23, 23);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(_statusLabel, 0, 2);

        var editButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false
        };
        var addButton = new Button { Text = Localizer.Get("ButtonAdd"), Width = 90, Height = 30 };
        var updateButton = new Button { Text = Localizer.Get("ButtonUpdate"), Width = 90, Height = 30 };
        var deleteButton = new Button { Text = Localizer.Get("ButtonDelete"), Width = 90, Height = 30 };
        addButton.Click += (_, _) => AddItem();
        updateButton.Click += (_, _) => UpdateItem();
        deleteButton.Click += (_, _) => DeleteItem();
        editButtons.Controls.Add(addButton);
        editButtons.Controls.Add(updateButton);
        editButtons.Controls.Add(deleteButton);
        root.Controls.Add(editButtons, 0, 3);

        var dialogButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90 };
        var cancelButton = new Button { Text = Localizer.Get("ButtonCancel"), DialogResult = DialogResult.Cancel, Width = 90 };
        dialogButtons.Controls.Add(cancelButton);
        dialogButtons.Controls.Add(okButton);
        root.Controls.Add(dialogButtons, 0, 4);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void AddItem()
    {
        var value = _textBox.Text.Trim();
        if (value.Length == 0)
        {
            ShowStatus(Localizer.Get("EditorValueRequiredMessage"));
            return;
        }

        if (_items.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
        {
            ShowStatus(Localizer.Get("EditorDuplicateValueMessage"));
            return;
        }

        _items.Add(value);
        _list.SelectedItem = value;
        ClearStatus();
    }

    private void UpdateItem()
    {
        if (_list.SelectedIndex < 0)
        {
            AddItem();
            return;
        }

        var value = _textBox.Text.Trim();
        if (value.Length == 0)
        {
            ShowStatus(Localizer.Get("EditorValueRequiredMessage"));
            return;
        }

        if (_items.Where((_, index) => index != _list.SelectedIndex)
            .Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
        {
            ShowStatus(Localizer.Get("EditorDuplicateValueMessage"));
            return;
        }

        var index = _list.SelectedIndex;
        _items[index] = value;
        _list.SelectedIndex = index;
        ClearStatus();
    }

    private void DeleteItem()
    {
        if (_list.SelectedIndex >= 0)
        {
            _items.RemoveAt(_list.SelectedIndex);
            ClearStatus();
        }
    }

    private void ShowStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void ClearStatus()
    {
        _statusLabel.Text = "";
    }
}
