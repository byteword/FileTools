using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class FileKindClassificationEditorDialog : Form
{
    private readonly List<FileKindRuleView> _rules;
    private readonly IReadOnlyList<RegisteredFileExtension> _registeredExtensions;
    private FileKindRuleView? _currentRule;
    private bool _loadingKindList;

    public FileKindClassificationEditorDialog(IEnumerable<FileKindExtensionRule> rules)
    {
        _rules = AutoRelocationFileTypeClassifier
            .NormalizeExtensionRules(rules)
            .Select(static rule => new FileKindRuleView(rule))
            .ToList();
        _registeredExtensions = SystemFileExtensionRegistry.LoadRegisteredExtensions();

        Text = Localizer.Get("DialogFileKindClassificationTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(820, 560);
        MinimumSize = new Size(700, 460);
        MinimizeBox = false;
        MaximizeBox = true;
        FormBorderStyle = FormBorderStyle.Sizable;

        BuildLayout();
        LoadKindList();
        RefreshRegisteredExtensions();
    }

    public IReadOnlyList<FileKindExtensionRule> Rules => _rules
        .Select(static rule => rule.ToRule())
        .ToArray();

    private void LoadKindList(int selectedIndex = 0)
    {
        _loadingKindList = true;
        _kindList.BeginUpdate();
        _kindList.Items.Clear();
        foreach (var rule in _rules)
        {
            _kindList.Items.Add(rule);
        }

        if (_kindList.Items.Count > 0)
        {
            _kindList.SelectedIndex = Math.Clamp(selectedIndex, 0, _kindList.Items.Count - 1);
        }
        _kindList.EndUpdate();
        _loadingKindList = false;
        SelectCurrentRule(commitPreviousRule: false);
    }

    private void SelectCurrentRule()
    {
        SelectCurrentRule(commitPreviousRule: true);
    }

    private void SelectCurrentRule(bool commitPreviousRule)
    {
        if (_loadingKindList)
        {
            return;
        }

        if (commitPreviousRule)
        {
            CommitCurrentRuleExtensions();
        }

        _currentRule = _kindList.SelectedItem as FileKindRuleView;
        _kindNameBox.Text = _currentRule?.Kind ?? "";
        _extensionsBox.Text = _currentRule is null ? "" : FormatExtensions(_currentRule.Extensions);
        UpdateKindEditorState();
        ClearStatus();
    }

    private void Confirm()
    {
        if (!ApplyCurrentKindNameEdit(showUnchangedStatus: false))
        {
            return;
        }

        CommitCurrentRuleExtensions();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void RestoreDefaults()
    {
        _rules.Clear();
        _rules.AddRange(AutoRelocationFileTypeClassifier
            .CreateDefaultExtensionRules()
            .Select(static rule => new FileKindRuleView(rule)));
        _currentRule = null;
        LoadKindList();
        ShowStatus(Localizer.Get("FileKindEditorDefaultsRestored"));
    }

    private void AddFileKind()
    {
        CommitCurrentRuleExtensions();
        var kind = CreateUniqueKindName(Localizer.Get("NewFileKindDisplayName"));
        var rule = new FileKindRuleView(new FileKindExtensionRule
        {
            Kind = kind,
            Extensions = []
        });
        _rules.Add(rule);
        LoadKindList(_rules.Count - 1);
        ShowStatus(Localizer.Format("FileKindEditorKindAddedFormat", kind));
    }

    private void RenameCurrentRule()
    {
        _ = ApplyCurrentKindNameEdit(showUnchangedStatus: true);
    }

    private void DeleteCurrentRule()
    {
        CommitCurrentRuleExtensions();
        var rule = _currentRule;
        if (rule is null)
        {
            ShowStatus(Localizer.Get("FileKindEditorSelectCategory"));
            return;
        }

        var result = MessageBox.Show(
            Localizer.Format("FileKindEditorDeleteQuestionFormat", rule.Kind, rule.Extensions.Count),
            Localizer.Get("DialogFileKindClassificationTitle"),
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.OK)
        {
            return;
        }

        var selectedIndex = _kindList.SelectedIndex;
        _rules.Remove(rule);
        _currentRule = null;
        LoadKindList(Math.Min(selectedIndex, _rules.Count - 1));
        ShowStatus(Localizer.Format("FileKindEditorKindDeletedFormat", rule.Kind));
    }

    private bool ApplyCurrentKindNameEdit(bool showUnchangedStatus)
    {
        var rule = _currentRule;
        if (rule is null)
        {
            if (showUnchangedStatus)
            {
                ShowStatus(Localizer.Get("FileKindEditorSelectCategory"));
            }

            return true;
        }

        var kind = AutoRelocationFileTypeClassifier.NormalizeFileKind(_kindNameBox.Text);
        if (kind.Length == 0)
        {
            ShowStatus(Localizer.Get("FileKindEditorKindNameRequired"));
            return false;
        }

        if (AutoRelocationFileTypeClassifier.IsReservedFileKind(kind))
        {
            ShowStatus(Localizer.Format("FileKindEditorReservedKindFormat", kind));
            return false;
        }

        if (!AutoRelocationFileTypeClassifier.IsValidConfigurableFileKind(kind))
        {
            ShowStatus(Localizer.Get("FileKindEditorKindNameInvalid"));
            return false;
        }

        if (IsDuplicateKind(kind, rule))
        {
            ShowStatus(Localizer.Format("FileKindEditorKindAlreadyExistsFormat", kind));
            return false;
        }

        if (string.Equals(rule.Kind, kind, StringComparison.Ordinal))
        {
            _kindNameBox.Text = rule.Kind;
            if (showUnchangedStatus)
            {
                ClearStatus();
            }

            return true;
        }

        var previousKind = rule.Kind;
        rule.Kind = kind;
        RefreshSelectedKindName();
        ShowStatus(Localizer.Format("FileKindEditorKindRenamedFormat", previousKind, rule.Kind));
        return true;
    }

    private void CommitCurrentRuleExtensions()
    {
        if (_currentRule is null)
        {
            return;
        }

        _currentRule.Extensions = AutoRelocationFileTypeClassifier
            .ParseExtensionList(_extensionsBox.Text)
            .ToList();
    }

    private void RefreshSelectedKindName()
    {
        var selectedIndex = _kindList.SelectedIndex;
        if (selectedIndex < 0)
        {
            return;
        }

        _loadingKindList = true;
        _kindList.Items[selectedIndex] = _currentRule!;
        _kindList.SelectedIndex = selectedIndex;
        _loadingKindList = false;
        _kindList.Invalidate();
    }

    private void UpdateKindEditorState()
    {
        var hasRule = _currentRule is not null;
        _kindNameBox.Enabled = hasRule;
        _renameKindButton.Enabled = hasRule;
        _deleteKindButton.Enabled = hasRule;
        _extensionsBox.Enabled = hasRule;
    }

    private string CreateUniqueKindName(string baseName)
    {
        var normalizedBaseName = AutoRelocationFileTypeClassifier.NormalizeFileKind(baseName);
        if (!AutoRelocationFileTypeClassifier.IsValidConfigurableFileKind(normalizedBaseName))
        {
            normalizedBaseName = "Custom";
        }

        if (!IsDuplicateKind(normalizedBaseName, currentRule: null))
        {
            return normalizedBaseName;
        }

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{normalizedBaseName} {index}";
            if (!IsDuplicateKind(candidate, currentRule: null))
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private bool IsDuplicateKind(string kind, FileKindRuleView? currentRule)
    {
        return _rules.Any(rule =>
            !ReferenceEquals(rule, currentRule) &&
            string.Equals(rule.Kind, kind, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshRegisteredExtensions()
    {
        var filter = _extensionSearchBox.Text.Trim();
        var matches = _registeredExtensions
            .Where(item => MatchesFilter(item, filter))
            .Take(500)
            .ToArray();

        _registeredExtensionList.BeginUpdate();
        _registeredExtensionList.Items.Clear();
        foreach (var item in matches)
        {
            _registeredExtensionList.Items.Add(item);
        }

        _registeredExtensionList.EndUpdate();

        if (_registeredExtensions.Count == 0)
        {
            ShowStatus(Localizer.Get("FileKindEditorNoRegisteredExtensions"));
        }
        else if (matches.Length == 0)
        {
            ShowStatus(Localizer.Get("FileKindEditorNoMatchingExtensions"));
        }
        else
        {
            ClearStatus();
        }
    }

    private static bool MatchesFilter(RegisteredFileExtension item, string filter)
    {
        return string.IsNullOrWhiteSpace(filter) ||
            item.Extension.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            item.Description.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void AddSelectedRegisteredExtension()
    {
        if (_registeredExtensionList.SelectedItem is not RegisteredFileExtension selected)
        {
            ShowStatus(Localizer.Get("FileKindEditorSelectExtension"));
            return;
        }

        CommitCurrentRuleExtensions();
        var rule = _currentRule;
        if (rule is null)
        {
            ShowStatus(Localizer.Get("FileKindEditorSelectCategory"));
            return;
        }

        var extension = AutoRelocationFileTypeClassifier.NormalizeExtension(selected.Extension);
        if (extension.Length == 0)
        {
            return;
        }

        if (rule.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            ShowStatus(Localizer.Format("FileKindEditorExtensionAlreadyExistsFormat", extension, rule.Kind));
            return;
        }

        rule.Extensions.Add(extension);
        _extensionsBox.Text = FormatExtensions(rule.Extensions);
        ShowStatus(Localizer.Format("FileKindEditorExtensionAddedFormat", extension, rule.Kind));
    }

    private static string FormatExtensions(IEnumerable<string> extensions)
    {
        return string.Join(Environment.NewLine, extensions);
    }

    private void ShowStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void ClearStatus()
    {
        _statusLabel.Text = "";
    }

    private sealed class FileKindRuleView
    {
        public FileKindRuleView(FileKindExtensionRule rule)
        {
            Kind = rule.Kind;
            Extensions = rule.Extensions.ToList();
        }

        public string Kind { get; set; }

        public List<string> Extensions { get; set; }

        public FileKindExtensionRule ToRule()
        {
            return new FileKindExtensionRule
            {
                Kind = AutoRelocationFileTypeClassifier.NormalizeFileKind(Kind),
                Extensions = AutoRelocationFileTypeClassifier.NormalizeExtensions(Extensions).ToList()
            };
        }

        public override string ToString()
        {
            return Kind;
        }
    }
}
