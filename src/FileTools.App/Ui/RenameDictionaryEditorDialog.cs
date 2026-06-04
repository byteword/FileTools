using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class RenameDictionaryEditorDialog : Form
{
    private readonly BindingList<RenameDictionaryEntry> _entries;

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
