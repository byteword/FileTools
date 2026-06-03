using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class FileKindClassificationEditorDialog : Form
{
    private readonly List<FileKindRuleView> _rules;
    private readonly IReadOnlyList<RegisteredFileExtension> _registeredExtensions;
    private readonly ListBox _kindList = new();
    private readonly TextBox _kindNameBox = new();
    private readonly TextBox _extensionsBox = new();
    private readonly TextBox _extensionSearchBox = new();
    private readonly ListBox _registeredExtensionList = new();
    private readonly Label _statusLabel = new();
    private readonly Button _addKindButton = new();
    private readonly Button _renameKindButton = new();
    private readonly Button _deleteKindButton = new();
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

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(CreateHelpLabel(), 0, 0);
        root.Controls.Add(CreateEditorPanel(), 0, 1);
        root.Controls.Add(CreateButtonPanel(), 0, 2);
    }

    private Control CreateHelpLabel()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FileKindClassificationHelp"),
            ForeColor = Color.FromArgb(55, 65, 81),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private Control CreateEditorPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(CreateKindPanel(), 0, 0);
        panel.Controls.Add(CreateRulePanel(), 1, 0);
        return panel;
    }

    private Control CreateKindPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 10, 0),
            ColumnCount = 1,
            RowCount = 5
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.Controls.Add(CreateFieldLabel(Localizer.Get("LabelFileKindCategories")), 0, 0);

        _kindList.Dock = DockStyle.Fill;
        _kindList.SelectedIndexChanged += (_, _) => SelectCurrentRule();
        panel.Controls.Add(_kindList, 0, 1);

        panel.Controls.Add(CreateFieldLabel(Localizer.Get("LabelFileKindRepresentativeName")), 0, 2);
        _kindNameBox.Dock = DockStyle.Fill;
        _kindNameBox.PlaceholderText = Localizer.Get("LabelFileKindRepresentativeName");
        _kindNameBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                RenameCurrentRule();
                e.SuppressKeyPress = true;
            }
        };
        panel.Controls.Add(_kindNameBox, 0, 3);
        panel.Controls.Add(CreateKindButtonPanel(), 0, 4);
        return panel;
    }

    private Control CreateKindButtonPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0)
        };

        _addKindButton.Text = Localizer.Get("ButtonAdd");
        _addKindButton.Width = 64;
        _addKindButton.Height = 30;
        _addKindButton.Click += (_, _) => AddFileKind();

        _renameKindButton.Text = Localizer.Get("ButtonRenameFileKind");
        _renameKindButton.Width = 86;
        _renameKindButton.Height = 30;
        _renameKindButton.Click += (_, _) => RenameCurrentRule();

        _deleteKindButton.Text = Localizer.Get("ButtonDelete");
        _deleteKindButton.Width = 64;
        _deleteKindButton.Height = 30;
        _deleteKindButton.Click += (_, _) => DeleteCurrentRule();

        panel.Controls.Add(_addKindButton);
        panel.Controls.Add(_renameKindButton);
        panel.Controls.Add(_deleteKindButton);
        return panel;
    }

    private Control CreateRulePanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        panel.Controls.Add(CreateFieldLabel(Localizer.Get("LabelFileKindExtensions")), 0, 0);
        _extensionsBox.Dock = DockStyle.Fill;
        _extensionsBox.Multiline = true;
        _extensionsBox.ScrollBars = ScrollBars.Vertical;
        _extensionsBox.AcceptsReturn = true;
        panel.Controls.Add(_extensionsBox, 0, 1);

        panel.Controls.Add(CreateFieldLabel(Localizer.Get("LabelRegisteredExtensions")), 0, 2);
        _extensionSearchBox.Dock = DockStyle.Fill;
        _extensionSearchBox.PlaceholderText = Localizer.Get("LabelExtensionSearch");
        _extensionSearchBox.TextChanged += (_, _) => RefreshRegisteredExtensions();
        panel.Controls.Add(_extensionSearchBox, 0, 3);

        _registeredExtensionList.Dock = DockStyle.Fill;
        _registeredExtensionList.DoubleClick += (_, _) => AddSelectedRegisteredExtension();
        panel.Controls.Add(_registeredExtensionList, 0, 4);

        var addButton = new Button
        {
            Text = Localizer.Get("ButtonAddExtension"),
            Width = 150,
            Height = 30
        };
        addButton.Click += (_, _) => AddSelectedRegisteredExtension();
        panel.Controls.Add(addButton, 0, 5);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(100, 116, 139);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(_statusLabel, 0, 6);
        return panel;
    }

    private Control CreateButtonPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 90
        };
        var okButton = new Button { Text = "OK", Width = 90 };
        var resetButton = new Button
        {
            Text = Localizer.Get("ButtonRestoreDefaults"),
            Width = 140
        };

        okButton.Click += (_, _) => Confirm();
        resetButton.Click += (_, _) => RestoreDefaults();
        panel.Controls.Add(cancelButton);
        panel.Controls.Add(okButton);
        panel.Controls.Add(resetButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        return panel;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

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
