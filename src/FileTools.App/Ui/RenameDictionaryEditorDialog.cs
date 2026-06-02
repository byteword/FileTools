using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class RenameDictionaryEditorDialog : Form
{
    private readonly BindingList<RenameDictionaryEntry> _entries;
    private readonly ListBox _list = new();
    private readonly TextBox _sourceBox = new();
    private readonly TextBox _replacementBox = new();
    private readonly Label _statusLabel = new();

    public RenameDictionaryEditorDialog(IEnumerable<RenameDictionaryEntry> entries)
    {
        _entries = new BindingList<RenameDictionaryEntry>(entries
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Source))
            .Select(static entry => new RenameDictionaryEntry
            {
                Source = entry.Source.Trim(),
                Replacement = entry.Replacement.Trim()
            })
            .ToList());

        Text = Localizer.Get("DialogRenameDictionaryTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 620;
        Height = 450;
        MinimumSize = new Size(520, 360);
        MinimizeBox = false;
        MaximizeBox = true;
        FormBorderStyle = FormBorderStyle.Sizable;

        BuildLayout();
    }

    public IReadOnlyList<RenameDictionaryEntry> Entries => _entries
        .Where(static entry => !string.IsNullOrWhiteSpace(entry.Source))
        .Select(static entry => new RenameDictionaryEntry
        {
            Source = entry.Source.Trim(),
            Replacement = entry.Replacement.Trim()
        })
        .DistinctBy(static entry => entry.Source, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);

        _list.Dock = DockStyle.Fill;
        _list.DataSource = _entries;
        _list.SelectedIndexChanged += (_, _) => LoadSelectedEntry();
        root.Controls.Add(_list, 0, 0);

        root.Controls.Add(CreateTextRow(Localizer.Get("LabelSourceText"), _sourceBox), 0, 1);
        root.Controls.Add(CreateTextRow(Localizer.Get("LabelReplacementText"), _replacementBox), 0, 2);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(128, 23, 23);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(_statusLabel, 0, 3);

        var editButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false
        };
        var addButton = new Button { Text = Localizer.Get("ButtonAdd"), Width = 90, Height = 30 };
        var updateButton = new Button { Text = Localizer.Get("ButtonUpdate"), Width = 90, Height = 30 };
        var deleteButton = new Button { Text = Localizer.Get("ButtonDelete"), Width = 90, Height = 30 };
        addButton.Click += (_, _) => AddEntry();
        updateButton.Click += (_, _) => UpdateEntry();
        deleteButton.Click += (_, _) => DeleteEntry();
        editButtons.Controls.Add(addButton);
        editButtons.Controls.Add(updateButton);
        editButtons.Controls.Add(deleteButton);
        root.Controls.Add(editButtons, 0, 4);

        var dialogButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var okButton = new Button { Text = "OK", Width = 90 };
        var cancelButton = new Button { Text = Localizer.Get("ButtonCancel"), DialogResult = DialogResult.Cancel, Width = 90 };
        okButton.Click += (_, _) => Confirm();
        dialogButtons.Controls.Add(cancelButton);
        dialogButtons.Controls.Add(okButton);
        root.Controls.Add(dialogButtons, 0, 5);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private static Control CreateTextRow(string labelText, TextBox textBox)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var label = new Label
        {
            Text = labelText,
            Left = 0,
            Top = 8,
            Width = 150,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label);
        textBox.Left = 158;
        textBox.Top = 6;
        textBox.Width = 390;
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(textBox);
        panel.Resize += (_, _) =>
        {
            var labelWidth = Math.Clamp(panel.ClientSize.Width / 3, 120, 180);
            label.Width = labelWidth;
            textBox.Left = labelWidth + 8;
            textBox.Width = Math.Max(180, panel.ClientSize.Width - textBox.Left);
        };
        return panel;
    }

    private void LoadSelectedEntry()
    {
        if (_list.SelectedItem is not RenameDictionaryEntry entry)
        {
            _sourceBox.Text = "";
            _replacementBox.Text = "";
            ClearStatus();
            return;
        }

        _sourceBox.Text = entry.Source;
        _replacementBox.Text = entry.Replacement;
        ClearStatus();
    }

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
        if (_list.SelectedIndex < 0)
        {
            return string.IsNullOrWhiteSpace(_sourceBox.Text) &&
                string.IsNullOrWhiteSpace(_replacementBox.Text) || AddEntry();
        }

        var current = _entries[_list.SelectedIndex];
        return string.Equals(current.Source, _sourceBox.Text.Trim(), StringComparison.Ordinal) &&
            string.Equals(current.Replacement, _replacementBox.Text.Trim(), StringComparison.Ordinal) || UpdateEntry();
    }

    private bool AddEntry()
    {
        var entry = CreateEntryFromFields();
        if (entry is null)
        {
            ShowStatus(Localizer.Get("EditorValueRequiredMessage"));
            return false;
        }

        if (HasDuplicateSource(entry.Source, exceptIndex: -1))
        {
            ShowStatus(Localizer.Get("EditorDuplicateValueMessage"));
            return false;
        }

        _entries.Add(entry);
        _list.SelectedItem = entry;
        ClearStatus();
        return true;
    }

    private bool UpdateEntry()
    {
        if (_list.SelectedIndex < 0)
        {
            return AddEntry();
        }

        var entry = CreateEntryFromFields();
        if (entry is null)
        {
            ShowStatus(Localizer.Get("EditorValueRequiredMessage"));
            return false;
        }

        if (HasDuplicateSource(entry.Source, _list.SelectedIndex))
        {
            ShowStatus(Localizer.Get("EditorDuplicateValueMessage"));
            return false;
        }

        var index = _list.SelectedIndex;
        _entries[index] = entry;
        _list.SelectedIndex = index;
        ClearStatus();
        return true;
    }

    private void DeleteEntry()
    {
        if (_list.SelectedIndex >= 0)
        {
            _entries.RemoveAt(_list.SelectedIndex);
            ClearStatus();
        }
    }

    private RenameDictionaryEntry? CreateEntryFromFields()
    {
        var source = _sourceBox.Text.Trim();
        if (source.Length == 0)
        {
            return null;
        }

        return new RenameDictionaryEntry
        {
            Source = source,
            Replacement = _replacementBox.Text.Trim()
        };
    }

    private bool HasDuplicateSource(string source, int exceptIndex)
    {
        return _entries
            .Where((_, index) => index != exceptIndex)
            .Any(entry => string.Equals(entry.Source, source, StringComparison.OrdinalIgnoreCase));
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
