using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class StringListEditorDialog : Form
{
    private readonly BindingList<string> _items;

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
        MinimumSize = new Size(420, 320);
        MinimizeBox = false;
        MaximizeBox = true;
        FormBorderStyle = FormBorderStyle.Sizable;

        BuildLayout(itemLabel);
    }

    public IReadOnlyList<string> Items => _items
        .Select(static item => item.Trim())
        .Where(static item => item.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private void Confirm()
    {
        if (!CommitPendingEdit())
        {
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private bool CommitPendingEdit()
    {
        var value = _textBox.Text.Trim();
        if (_list.SelectedIndex < 0)
        {
            return value.Length == 0 || AddItem();
        }

        var current = _items[_list.SelectedIndex];
        return string.Equals(current, value, StringComparison.Ordinal) || UpdateItem();
    }

    private bool AddItem()
    {
        var value = _textBox.Text.Trim();
        if (value.Length == 0)
        {
            ShowStatus(Localizer.Get("EditorValueRequiredMessage"));
            return false;
        }

        if (_items.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
        {
            ShowStatus(Localizer.Get("EditorDuplicateValueMessage"));
            return false;
        }

        _items.Add(value);
        _list.SelectedItem = value;
        ClearStatus();
        return true;
    }

    private bool UpdateItem()
    {
        if (_list.SelectedIndex < 0)
        {
            return AddItem();
        }

        var value = _textBox.Text.Trim();
        if (value.Length == 0)
        {
            ShowStatus(Localizer.Get("EditorValueRequiredMessage"));
            return false;
        }

        if (_items.Where((_, index) => index != _list.SelectedIndex)
            .Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
        {
            ShowStatus(Localizer.Get("EditorDuplicateValueMessage"));
            return false;
        }

        var index = _list.SelectedIndex;
        _items[index] = value;
        _list.SelectedIndex = index;
        ClearStatus();
        return true;
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
