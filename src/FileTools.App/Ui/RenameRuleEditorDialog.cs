using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class RenameRuleEditorDialog : Form
{
    private static readonly RenameCorrectionRuleKind[] UserRuleKinds =
    [
        RenameCorrectionRuleKind.LiteralReplace,
        RenameCorrectionRuleKind.PrefixTrim,
        RenameCorrectionRuleKind.SuffixTrim,
        RenameCorrectionRuleKind.WhitespaceNormalize,
        RenameCorrectionRuleKind.SeparatorNormalize,
        RenameCorrectionRuleKind.RegexReplace
    ];

    private static readonly RenameCorrectionRuleStage[] UserRuleStages =
    [
        RenameCorrectionRuleStage.Preprocess,
        RenameCorrectionRuleStage.UserRewrite
    ];

    private readonly ListBox _ruleList = new();
    private readonly CheckBox _enabledCheckBox = new();
    private readonly CheckBox _ignoreCaseCheckBox = new();
    private readonly ComboBox _stageCombo = new();
    private readonly ComboBox _kindCombo = new();
    private readonly ComboBox _modeCombo = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _sourceBox = new();
    private readonly TextBox _replacementBox = new();
    private readonly TextBox _descriptionBox = new();
    private readonly TabControl _editorTabs = new();
    private readonly TabPage _generalTab = new();
    private readonly TabPage _detailsTab = new();
    private readonly Panel _detailsScrollHost = new();
    private readonly FlowLayoutPanel _detailsStack = new();
    private readonly Label _statusLabel = new();
    private readonly Button _updateButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _moveUpButton = new();
    private readonly Button _moveDownButton = new();

    private readonly BindingList<RenameDictionaryEntry> _dictionaryEntries;
    private readonly BindingList<string> _commonPhrases;
    private readonly ListBox _dictionaryEntryList = new();
    private readonly TextBox _dictionarySourceBox = new();
    private readonly TextBox _dictionaryReplacementBox = new();
    private readonly Label _dictionaryStatusLabel = new();
    private readonly ListBox _commonPhraseList = new();
    private readonly TextBox _commonPhraseBox = new();
    private readonly Label _commonPhraseStatusLabel = new();

    private readonly BindingList<string> _knownTags;
    private readonly BindingList<string> _authorPrefixes;
    private readonly BindingList<string> _episodePrefixes;
    private readonly BindingList<string> _episodeUnits;
    private readonly BindingList<string> _titleNoiseWords;
    private readonly StringListDetailEditor _knownTagsEditor;
    private readonly StringListDetailEditor _authorPrefixesEditor;
    private readonly StringListDetailEditor _episodePrefixesEditor;
    private readonly StringListDetailEditor _episodeUnitsEditor;
    private readonly StringListDetailEditor _titleNoiseWordsEditor;

    private List<RenameCorrectionRule> _rules;
    private bool _loading;

    public RenameRuleEditorDialog(
        IEnumerable<RenameCorrectionRule> rules,
        RenameDictionaryDocument? renameDictionary = null,
        RenameParserProfileDocument? parserProfile = null)
    {
        _rules = RenameRuleStore.NormalizeRules(rules)
            .Select(static rule => rule.Clone())
            .ToList();

        var dictionary = renameDictionary ?? RenameDictionaryStore.Load();
        _dictionaryEntries = new BindingList<RenameDictionaryEntry>(dictionary.Replacements
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Source))
            .Select(static entry => new RenameDictionaryEntry
            {
                Source = entry.Source.Trim(),
                Replacement = entry.Replacement.Trim()
            })
            .DistinctBy(static entry => entry.Source, StringComparer.OrdinalIgnoreCase)
            .ToList());
        _commonPhrases = new BindingList<string>(dictionary.CommonPhrases
            .Select(static phrase => phrase.Trim())
            .Where(static phrase => phrase.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList());
        var profile = RenameParserProfileStore.Normalize(parserProfile ?? RenameParserProfileStore.Load());
        _knownTags = new BindingList<string>(profile.KnownTags.ToList());
        _authorPrefixes = new BindingList<string>(profile.AuthorPrefixes.ToList());
        _episodePrefixes = new BindingList<string>(profile.EpisodePrefixes.ToList());
        _episodeUnits = new BindingList<string>(profile.EpisodeUnits.ToList());
        _titleNoiseWords = new BindingList<string>(profile.TitleNoiseWords.ToList());
        _knownTagsEditor = new StringListDetailEditor(_knownTags);
        _authorPrefixesEditor = new StringListDetailEditor(_authorPrefixes);
        _episodePrefixesEditor = new StringListDetailEditor(_episodePrefixes);
        _episodeUnitsEditor = new StringListDetailEditor(_episodeUnits);
        _titleNoiseWordsEditor = new StringListDetailEditor(_titleNoiseWords);

        Text = Localizer.Get("DialogRenameRulesTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 1040;
        Height = 680;
        MinimumSize = new Size(920, 560);
        MinimizeBox = false;
        MaximizeBox = true;

        BindDetailEditorData();
        WireDetailEditorEvents();
        BuildLayout();
        BindCombos();
        RefreshList(_rules.FirstOrDefault());
    }

    public IReadOnlyList<RenameCorrectionRule> Rules => RenameRuleStore.NormalizeRules(_rules);

    public RenameDictionaryDocument RenameDictionary => new()
    {
        Replacements = _dictionaryEntries
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Source))
            .Select(static entry => new RenameDictionaryEntry
            {
                Source = entry.Source.Trim(),
                Replacement = entry.Replacement.Trim()
            })
            .DistinctBy(static entry => entry.Source, StringComparer.OrdinalIgnoreCase)
            .ToList(),
        CommonPhrases = _commonPhrases
            .Select(static phrase => phrase.Trim())
            .Where(static phrase => phrase.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
    };

    public RenameParserProfileDocument ParserProfile => RenameParserProfileStore.Normalize(new RenameParserProfileDocument
    {
        KnownTags = _knownTags.ToList(),
        AuthorPrefixes = _authorPrefixes.ToList(),
        EpisodePrefixes = _episodePrefixes.ToList(),
        EpisodeUnits = _episodeUnits.ToList(),
        TitleNoiseWords = _titleNoiseWords.ToList()
    });

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 2
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        root.Controls.Add(BuildRuleList(), 0, 0);
        root.Controls.Add(BuildEditorTabs(), 1, 0);

        var footer = BuildFooter();
        root.Controls.Add(footer, 0, 1);
        root.SetColumnSpan(footer, 2);
    }

    private Control BuildRuleList()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(0, 0, 12, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameRulesListLabel"),
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label, 0, 0);

        _ruleList.Dock = DockStyle.Fill;
        _ruleList.SelectedIndexChanged += (_, _) => LoadSelectedRule();
        panel.Controls.Add(_ruleList, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        var addButton = new Button { Text = Localizer.Get("ButtonAdd"), Width = 82, Height = 30 };
        _updateButton.Text = Localizer.Get("ButtonUpdate");
        _updateButton.Width = 82;
        _updateButton.Height = 30;
        _deleteButton.Text = Localizer.Get("ButtonDelete");
        _deleteButton.Width = 82;
        _deleteButton.Height = 30;
        _moveUpButton.Text = Localizer.Get("ButtonMoveUp");
        _moveUpButton.Width = 82;
        _moveUpButton.Height = 30;
        _moveDownButton.Text = Localizer.Get("ButtonMoveDown");
        _moveDownButton.Width = 82;
        _moveDownButton.Height = 30;
        addButton.Click += (_, _) => AddRule();
        _updateButton.Click += (_, _) => UpdateSelectedRule();
        _deleteButton.Click += (_, _) => DeleteSelectedRule();
        _moveUpButton.Click += (_, _) => MoveSelectedRule(-1);
        _moveDownButton.Click += (_, _) => MoveSelectedRule(1);
        buttons.Controls.Add(addButton);
        buttons.Controls.Add(_updateButton);
        buttons.Controls.Add(_deleteButton);
        buttons.Controls.Add(_moveUpButton);
        buttons.Controls.Add(_moveDownButton);
        panel.Controls.Add(buttons, 0, 2);
        return panel;
    }

    private Control BuildEditorTabs()
    {
        _editorTabs.Dock = DockStyle.Fill;
        _generalTab.Text = Localizer.Get("RenameRuleGeneralTab");
        _generalTab.Padding = new Padding(8);
        _detailsTab.Text = Localizer.Get("RenameRuleDetailsTab");
        _detailsTab.Padding = new Padding(8);

        _generalTab.Controls.Add(BuildGeneralEditor());
        _detailsTab.Controls.Add(BuildDetailsEditor());
        _editorTabs.TabPages.Add(_generalTab);
        _editorTabs.TabPages.Add(_detailsTab);
        return _editorTabs;
    }

    private Control BuildGeneralEditor()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(0, 4, 0, 0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 7; row++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddRow(panel, 0, Localizer.Get("LabelRuleEnabled"), _enabledCheckBox);
        AddRow(panel, 1, Localizer.Get("LabelRuleName"), _nameBox);
        AddRow(panel, 2, Localizer.Get("LabelRuleStage"), _stageCombo);
        AddRow(panel, 3, Localizer.Get("LabelRuleKind"), _kindCombo);
        AddRow(panel, 4, Localizer.Get("LabelRuleMode"), _modeCombo);
        AddRow(panel, 5, Localizer.Get("LabelRuleSource"), _sourceBox);
        AddRow(panel, 6, Localizer.Get("LabelRuleReplacement"), _replacementBox);

        _descriptionBox.Multiline = true;
        _descriptionBox.ScrollBars = ScrollBars.Vertical;
        AddRow(panel, 7, Localizer.Get("LabelRuleDescription"), _descriptionBox);

        _ignoreCaseCheckBox.Text = Localizer.Get("CheckRuleIgnoreCase");
        _ignoreCaseCheckBox.Dock = DockStyle.Fill;
        panel.Controls.Add(new Label(), 0, 8);
        panel.Controls.Add(_ignoreCaseCheckBox, 1, 8);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(128, 23, 23);
        _statusLabel.TextAlign = ContentAlignment.TopLeft;
        panel.Controls.Add(_statusLabel, 1, 9);

        _kindCombo.SelectedIndexChanged += (_, _) => UpdateFieldState();
        _stageCombo.SelectedIndexChanged += (_, _) => UpdateFieldState();
        return panel;
    }

    private Control BuildDetailsEditor()
    {
        _detailsScrollHost.Dock = DockStyle.Fill;
        _detailsScrollHost.AutoScroll = true;
        _detailsScrollHost.BackColor = SystemColors.Control;

        _detailsStack.FlowDirection = FlowDirection.TopDown;
        _detailsStack.WrapContents = false;
        _detailsStack.AutoSize = true;
        _detailsStack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _detailsStack.Dock = DockStyle.Top;
        _detailsStack.Padding = new Padding(0);
        _detailsScrollHost.Controls.Add(_detailsStack);
        _detailsScrollHost.Resize += (_, _) => ResizeDetailsControls();
        return _detailsScrollHost;
    }

    private Control BuildFooter()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(0, 10, 0, 0)
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0)
        };
        var okButton = new Button { Text = "OK", Width = 90, Height = 30, Margin = new Padding(8, 0, 0, 0) };
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 90,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        okButton.Click += (_, _) => Confirm();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);
        panel.Controls.Add(buttons, 0, 0);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return panel;
    }

    private static void AddRow(TableLayoutPanel panel, int row, string labelText, Control control)
    {
        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft
        };
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(label, 0, row);
        panel.Controls.Add(control, 1, row);
    }

    private void BindCombos()
    {
        _stageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _kindCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _modeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _stageCombo.DataSource = UserRuleStages
            .Select(stage => new ComboOption<RenameCorrectionRuleStage>(RenameCorrectionRuleText.GetStageDisplayName(stage), stage))
            .ToArray();
        _kindCombo.DataSource = UserRuleKinds
            .Select(kind => new ComboOption<RenameCorrectionRuleKind>(RenameCorrectionRuleText.GetKindDisplayName(kind), kind))
            .ToArray();
        _modeCombo.DataSource = Enum.GetValues<RenameCorrectionRuleMode>()
            .Select(mode => new ComboOption<RenameCorrectionRuleMode>(RenameCorrectionRuleText.GetModeDisplayName(mode), mode))
            .ToArray();
    }

    private void BindDetailEditorData()
    {
        _dictionaryEntryList.DataSource = _dictionaryEntries;
        _commonPhraseList.DataSource = _commonPhrases;
    }

    private void WireDetailEditorEvents()
    {
        _dictionaryEntryList.SelectedIndexChanged += (_, _) => LoadSelectedDictionaryEntry();
        _commonPhraseList.SelectedIndexChanged += (_, _) => LoadSelectedCommonPhrase();
        LoadSelectedDictionaryEntry();
        LoadSelectedCommonPhrase();
    }

    private void RefreshList(RenameCorrectionRule? selected)
    {
        _rules = RenameRuleStore.NormalizeRules(_rules)
            .Select(static rule => rule.Clone())
            .ToList();
        _ruleList.DataSource = null;
        _ruleList.DataSource = _rules;
        if (selected is not null)
        {
            _ruleList.SelectedItem = _rules.FirstOrDefault(rule => string.Equals(rule.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
        }

        if (_ruleList.SelectedIndex < 0 && _rules.Count > 0)
        {
            _ruleList.SelectedIndex = 0;
        }

        UpdateFieldState();
    }

    private void LoadSelectedRule()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        try
        {
            if (_ruleList.SelectedItem is not RenameCorrectionRule rule)
            {
                ClearFields();
                return;
            }

            _enabledCheckBox.Checked = rule.Enabled || rule.IsRequired;
            _nameBox.Text = rule.DisplayName;
            SelectComboValue(_stageCombo, rule.Stage);
            SelectComboValue(_kindCombo, rule.IsBuiltIn ? RenameCorrectionRuleKind.LiteralReplace : rule.Kind);
            SelectComboValue(_modeCombo, rule.Mode);
            _sourceBox.Text = rule.Source;
            _replacementBox.Text = rule.Replacement;
            _descriptionBox.Text = rule.Description;
            _ignoreCaseCheckBox.Checked = rule.IgnoreCase;
            ClearStatus();
        }
        finally
        {
            _loading = false;
        }

        UpdateFieldState();
    }

    private void ClearFields()
    {
        _enabledCheckBox.Checked = false;
        _nameBox.Text = "";
        _sourceBox.Text = "";
        _replacementBox.Text = "";
        _descriptionBox.Text = "";
        _ignoreCaseCheckBox.Checked = true;
        UpdateRuleSpecificSettingsPanel(null);
    }

    private void AddRule()
    {
        var rule = new RenameCorrectionRule
        {
            Id = "user." + Guid.NewGuid().ToString("N"),
            DisplayName = Localizer.Get("NewRenameRuleDisplayName"),
            Kind = RenameCorrectionRuleKind.LiteralReplace,
            Stage = RenameCorrectionRuleStage.UserRewrite,
            Mode = RenameCorrectionRuleMode.Automatic,
            Enabled = true,
            IgnoreCase = true,
            Order = GetNextOrder(RenameCorrectionRuleStage.UserRewrite)
        };
        _rules.Add(rule);
        RefreshList(rule);
        _nameBox.Focus();
        _nameBox.SelectAll();
    }

    private void UpdateSelectedRule()
    {
        if (_ruleList.SelectedItem is not RenameCorrectionRule rule)
        {
            return;
        }

        if (!ApplyFieldsToRule(rule))
        {
            return;
        }

        RefreshList(rule);
    }

    private void DeleteSelectedRule()
    {
        if (_ruleList.SelectedItem is not RenameCorrectionRule rule || rule.IsBuiltIn)
        {
            return;
        }

        var index = _ruleList.SelectedIndex;
        _rules.RemoveAll(item => string.Equals(item.Id, rule.Id, StringComparison.OrdinalIgnoreCase));
        RefreshList(_rules.ElementAtOrDefault(Math.Clamp(index, 0, Math.Max(0, _rules.Count - 1))));
    }

    private void MoveSelectedRule(int direction)
    {
        if (_ruleList.SelectedItem is not RenameCorrectionRule rule)
        {
            return;
        }

        var stageRules = _rules
            .Where(item => item.Stage == rule.Stage)
            .OrderBy(item => item.Order)
            .ToList();
        var index = stageRules.FindIndex(item => string.Equals(item.Id, rule.Id, StringComparison.OrdinalIgnoreCase));
        var targetIndex = index + direction;
        if (index < 0 || targetIndex < 0 || targetIndex >= stageRules.Count)
        {
            return;
        }

        (stageRules[index].Order, stageRules[targetIndex].Order) = (stageRules[targetIndex].Order, stageRules[index].Order);
        RefreshList(rule);
    }

    private void Confirm()
    {
        if (_ruleList.SelectedItem is RenameCorrectionRule rule && !ApplyFieldsToRule(rule))
        {
            _editorTabs.SelectedTab = _generalTab;
            return;
        }

        if (!CommitPendingDetailEdit())
        {
            _editorTabs.SelectedTab = _detailsTab;
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private bool ApplyFieldsToRule(RenameCorrectionRule rule)
    {
        if (rule.IsBuiltIn)
        {
            rule.Enabled = rule.IsRequired || _enabledCheckBox.Checked;
            rule.Mode = GetComboValue(_modeCombo, rule.Mode);
            ClearStatus();
            return true;
        }

        var name = _nameBox.Text.Trim();
        var kind = GetComboValue(_kindCombo, rule.Kind);
        var source = _sourceBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowStatus(Localizer.Get("EditorValueRequiredMessage"));
            return false;
        }

        if (RequiresSource(kind) && source.Length == 0)
        {
            ShowStatus(Localizer.Get("RenameRuleSourceRequiredMessage"));
            return false;
        }

        rule.DisplayName = name;
        rule.Enabled = _enabledCheckBox.Checked;
        rule.Stage = GetComboValue(_stageCombo, rule.Stage);
        rule.Kind = kind;
        rule.Mode = GetComboValue(_modeCombo, rule.Mode);
        rule.Source = source;
        rule.Replacement = _replacementBox.Text.Trim();
        rule.Description = _descriptionBox.Text.Trim();
        rule.IgnoreCase = _ignoreCaseCheckBox.Checked;
        if (rule.Order <= 0)
        {
            rule.Order = GetNextOrder(rule.Stage);
        }

        ClearStatus();
        return true;
    }

    private void UpdateFieldState()
    {
        var rule = _ruleList.SelectedItem as RenameCorrectionRule;
        var hasRule = rule is not null;
        var isUserRule = hasRule && !rule!.IsBuiltIn;
        var kind = GetComboValue(_kindCombo, RenameCorrectionRuleKind.LiteralReplace);
        var hasTextParameters = RequiresSource(kind);

        _enabledCheckBox.Enabled = hasRule && !rule!.IsRequired;
        _modeCombo.Enabled = hasRule && !rule!.IsRequired;
        _nameBox.ReadOnly = !isUserRule;
        _stageCombo.Enabled = isUserRule;
        _kindCombo.Enabled = isUserRule;
        _sourceBox.ReadOnly = !isUserRule || !hasTextParameters;
        _replacementBox.ReadOnly = !isUserRule || kind is RenameCorrectionRuleKind.PrefixTrim or RenameCorrectionRuleKind.SuffixTrim;
        _descriptionBox.ReadOnly = !isUserRule;
        _ignoreCaseCheckBox.Enabled = isUserRule && hasTextParameters;
        _updateButton.Enabled = hasRule;
        _deleteButton.Enabled = isUserRule;
        _moveUpButton.Enabled = CanMove(rule, -1);
        _moveDownButton.Enabled = CanMove(rule, 1);
        UpdateRuleSpecificSettingsPanel(rule);
    }

    private void UpdateRuleSpecificSettingsPanel(RenameCorrectionRule? rule)
    {
        _detailsStack.SuspendLayout();
        try
        {
            _detailsStack.Controls.Clear();
            ClearDetailStatuses();

            if (rule?.Kind == RenameCorrectionRuleKind.BuiltInRenameDictionary)
            {
                BuildDictionaryDetailPanel();
            }
            else if (rule?.Kind == RenameCorrectionRuleKind.BuiltInObfuscatedHangulCandidate)
            {
                BuildCommonPhraseDetailPanel();
            }
            else if (rule?.Kind == RenameCorrectionRuleKind.BuiltInBracketMetadataExtraction)
            {
                BuildKnownTagsDetailPanel();
            }
            else if (rule?.Kind == RenameCorrectionRuleKind.BuiltInAuthorExtraction)
            {
                BuildAuthorPrefixesDetailPanel();
            }
            else if (rule?.Kind == RenameCorrectionRuleKind.BuiltInEpisodeExtraction)
            {
                BuildEpisodeDetailPanel();
            }
            else if (rule?.Kind == RenameCorrectionRuleKind.BuiltInTitleCleanup)
            {
                BuildTitleNoiseWordsDetailPanel();
            }
            else
            {
                BuildNoDetailPanel(rule);
            }
        }
        finally
        {
            _detailsStack.ResumeLayout();
            ResizeDetailsControls();
        }
    }

    private void BuildDictionaryDetailPanel()
    {
        AddDetailHeader(
            Localizer.Get("RenameRuleSpecificDictionaryTitle"),
            Localizer.Get("RenameRuleSpecificDictionaryHelp"));

        var listHost = new Panel { Height = 220, Margin = new Padding(0, 8, 0, 10) };
        _dictionaryEntryList.Dock = DockStyle.Fill;
        listHost.Controls.Add(_dictionaryEntryList);
        AddDetailControl(listHost);

        AddDetailControl(CreateDetailTextRow(Localizer.Get("LabelSourceText"), _dictionarySourceBox));
        AddDetailControl(CreateDetailTextRow(Localizer.Get("LabelReplacementText"), _dictionaryReplacementBox));

        _dictionaryStatusLabel.ForeColor = Color.FromArgb(128, 23, 23);
        _dictionaryStatusLabel.Height = 26;
        _dictionaryStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        AddDetailControl(_dictionaryStatusLabel);

        var buttons = CreateDetailButtonRow(
            () => AddDictionaryEntry(),
            () => UpdateDictionaryEntry(),
            DeleteDictionaryEntry);
        AddDetailControl(buttons);
    }

    private void BuildCommonPhraseDetailPanel()
    {
        AddDetailHeader(
            Localizer.Get("RenameRuleDetailCommonPhrasesTitle"),
            Localizer.Get("RenameRuleDetailCommonPhrasesHelp"));

        var listHost = new Panel { Height = 260, Margin = new Padding(0, 8, 0, 10) };
        _commonPhraseList.Dock = DockStyle.Fill;
        listHost.Controls.Add(_commonPhraseList);
        AddDetailControl(listHost);

        AddDetailControl(CreateDetailTextRow(Localizer.Get("LabelCommonPhrase"), _commonPhraseBox));

        _commonPhraseStatusLabel.ForeColor = Color.FromArgb(128, 23, 23);
        _commonPhraseStatusLabel.Height = 26;
        _commonPhraseStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        AddDetailControl(_commonPhraseStatusLabel);

        var buttons = CreateDetailButtonRow(
            () => AddCommonPhrase(),
            () => UpdateCommonPhrase(),
            DeleteCommonPhrase);
        AddDetailControl(buttons);
    }

    private void BuildKnownTagsDetailPanel()
    {
        AddDetailHeader(
            Localizer.Get("RenameRuleSpecificBracketMetadataTitle"),
            Localizer.Get("RenameRuleSpecificBracketMetadataHelp"));
        AddDetailControl(CreateStringListEditorPanel(
            _knownTagsEditor,
            Localizer.Get("LabelKnownTag"),
            Localizer.Get("RenameRuleDetailKnownTagsHelp")));
    }

    private void BuildAuthorPrefixesDetailPanel()
    {
        AddDetailHeader(
            Localizer.Get("RenameRuleSpecificAuthorExtractionTitle"),
            Localizer.Get("RenameRuleSpecificAuthorExtractionHelp"));
        AddDetailControl(CreateStringListEditorPanel(
            _authorPrefixesEditor,
            Localizer.Get("LabelAuthorPrefix"),
            Localizer.Get("RenameRuleDetailAuthorPrefixesHelp")));
    }

    private void BuildEpisodeDetailPanel()
    {
        AddDetailHeader(
            Localizer.Get("RenameRuleSpecificEpisodeExtractionTitle"),
            Localizer.Get("RenameRuleSpecificEpisodeExtractionHelp"));

        var grid = new TableLayoutPanel
        {
            Height = 370,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 10)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.Controls.Add(CreateStringListEditorPanel(
            _episodePrefixesEditor,
            Localizer.Get("LabelEpisodePrefix"),
            Localizer.Get("RenameRuleDetailEpisodePrefixesHelp"),
            new Padding(0, 0, 8, 0)), 0, 0);
        grid.Controls.Add(CreateStringListEditorPanel(
            _episodeUnitsEditor,
            Localizer.Get("LabelEpisodeUnit"),
            Localizer.Get("RenameRuleDetailEpisodeUnitsHelp"),
            new Padding(8, 0, 0, 0)), 1, 0);
        AddDetailControl(grid);
    }

    private void BuildTitleNoiseWordsDetailPanel()
    {
        AddDetailHeader(
            Localizer.Get("RenameRuleSpecificTitleCleanupTitle"),
            Localizer.Get("RenameRuleSpecificTitleCleanupHelp"));
        AddDetailControl(CreateStringListEditorPanel(
            _titleNoiseWordsEditor,
            Localizer.Get("LabelTitleNoiseWord"),
            Localizer.Get("RenameRuleDetailTitleNoiseWordsHelp")));
    }

    private void BuildNoDetailPanel(RenameCorrectionRule? rule)
    {
        var title = rule is null
            ? Localizer.Get("RenameRuleNoSpecificSettingsTitle")
            : rule.DisplayName;
        AddDetailHeader(title, Localizer.Get("RenameRuleNoSpecificSettingsHelp"));
    }

    private void AddDetailHeader(string title, string helpText)
    {
        var titleLabel = new Label
        {
            Text = title,
            Height = 28,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var helpLabel = new Label
        {
            Text = helpText,
            Height = 44,
            ForeColor = Color.FromArgb(75, 85, 99),
            TextAlign = ContentAlignment.MiddleLeft
        };
        AddDetailControl(titleLabel);
        AddDetailControl(helpLabel);
    }

    private void AddDetailControl(Control control)
    {
        control.Margin = new Padding(0, 0, 0, 8);
        _detailsStack.Controls.Add(control);
    }

    private static Control CreateDetailTextRow(string labelText, TextBox textBox)
    {
        var panel = new Panel { Height = 38, Margin = new Padding(0, 0, 0, 8) };
        var label = new Label
        {
            Text = labelText,
            Left = 0,
            Top = 7,
            Width = 130,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        textBox.Left = 140;
        textBox.Top = 5;
        textBox.Height = 26;
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(label);
        panel.Controls.Add(textBox);
        panel.Resize += (_, _) =>
        {
            var labelWidth = Math.Clamp(panel.ClientSize.Width / 3, 110, 170);
            label.Width = labelWidth;
            textBox.Left = labelWidth + 10;
            textBox.Width = Math.Max(160, panel.ClientSize.Width - textBox.Left);
        };
        return panel;
    }

    private static Control CreateDetailButtonRow(
        Func<bool> add,
        Func<bool> update,
        Action delete)
    {
        var buttons = new FlowLayoutPanel
        {
            Height = 38,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };
        var addButton = new Button { Text = Localizer.Get("ButtonAdd"), Width = 90, Height = 30 };
        var updateButton = new Button { Text = Localizer.Get("ButtonUpdate"), Width = 90, Height = 30 };
        var deleteButton = new Button { Text = Localizer.Get("ButtonDelete"), Width = 90, Height = 30 };
        addButton.Click += (_, _) => add();
        updateButton.Click += (_, _) => update();
        deleteButton.Click += (_, _) => delete();
        buttons.Controls.Add(addButton);
        buttons.Controls.Add(updateButton);
        buttons.Controls.Add(deleteButton);
        return buttons;
    }

    private static Control CreateStringListEditorPanel(
        StringListDetailEditor editor,
        string valueLabel,
        string helpText,
        Padding? padding = null)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 370,
            ColumnCount = 1,
            RowCount = 6,
            Padding = padding ?? new Padding(0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = valueLabel,
            Font = new Font(Control.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label, 0, 0);

        var helpLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = helpText,
            ForeColor = Color.FromArgb(75, 85, 99),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(helpLabel, 0, 1);

        editor.ListBox.Dock = DockStyle.Fill;
        panel.Controls.Add(editor.ListBox, 0, 2);

        var inputRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 0)
        };
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputRow.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = valueLabel,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        editor.TextBox.Dock = DockStyle.Fill;
        inputRow.Controls.Add(editor.TextBox, 1, 0);
        panel.Controls.Add(inputRow, 0, 3);

        var buttons = CreateDetailButtonRow(
            () => editor.Add(),
            () => editor.Update(),
            editor.Delete);
        buttons.Dock = DockStyle.Fill;
        panel.Controls.Add(buttons, 0, 4);

        editor.StatusLabel.Dock = DockStyle.Fill;
        editor.StatusLabel.ForeColor = Color.FromArgb(128, 23, 23);
        editor.StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(editor.StatusLabel, 0, 5);
        return panel;
    }

    private void ResizeDetailsControls()
    {
        var width = Math.Max(240, _detailsScrollHost.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
        _detailsStack.Width = width;
        foreach (Control control in _detailsStack.Controls)
        {
            control.Width = Math.Max(180, width - control.Margin.Horizontal);
        }
    }

    private bool CommitPendingDetailEdit()
    {
        if (_ruleList.SelectedItem is not RenameCorrectionRule rule)
        {
            return true;
        }

        return rule.Kind switch
        {
            RenameCorrectionRuleKind.BuiltInRenameDictionary => CommitPendingDictionaryEdit(),
            RenameCorrectionRuleKind.BuiltInObfuscatedHangulCandidate => CommitPendingCommonPhraseEdit(),
            RenameCorrectionRuleKind.BuiltInBracketMetadataExtraction => _knownTagsEditor.CommitPending(),
            RenameCorrectionRuleKind.BuiltInAuthorExtraction => _authorPrefixesEditor.CommitPending(),
            RenameCorrectionRuleKind.BuiltInEpisodeExtraction => _episodePrefixesEditor.CommitPending() &&
                _episodeUnitsEditor.CommitPending(),
            RenameCorrectionRuleKind.BuiltInTitleCleanup => _titleNoiseWordsEditor.CommitPending(),
            _ => true
        };
    }

    private void LoadSelectedDictionaryEntry()
    {
        if (_dictionaryEntryList.SelectedItem is not RenameDictionaryEntry entry)
        {
            _dictionarySourceBox.Text = "";
            _dictionaryReplacementBox.Text = "";
            ClearDetailStatuses();
            return;
        }

        _dictionarySourceBox.Text = entry.Source;
        _dictionaryReplacementBox.Text = entry.Replacement;
        ClearDetailStatuses();
    }

    private bool CommitPendingDictionaryEdit()
    {
        if (_dictionaryEntryList.SelectedIndex < 0)
        {
            return string.IsNullOrWhiteSpace(_dictionarySourceBox.Text) &&
                string.IsNullOrWhiteSpace(_dictionaryReplacementBox.Text) || AddDictionaryEntry();
        }

        var current = _dictionaryEntries[_dictionaryEntryList.SelectedIndex];
        return string.Equals(current.Source, _dictionarySourceBox.Text.Trim(), StringComparison.Ordinal) &&
            string.Equals(current.Replacement, _dictionaryReplacementBox.Text.Trim(), StringComparison.Ordinal) ||
            UpdateDictionaryEntry();
    }

    private bool AddDictionaryEntry()
    {
        var entry = CreateDictionaryEntryFromFields();
        if (entry is null)
        {
            ShowDictionaryStatus(Localizer.Get("EditorValueRequiredMessage"));
            return false;
        }

        if (HasDuplicateDictionarySource(entry.Source, exceptIndex: -1))
        {
            ShowDictionaryStatus(Localizer.Get("EditorDuplicateValueMessage"));
            return false;
        }

        _dictionaryEntries.Add(entry);
        _dictionaryEntryList.SelectedItem = entry;
        ClearDetailStatuses();
        return true;
    }

    private bool UpdateDictionaryEntry()
    {
        if (_dictionaryEntryList.SelectedIndex < 0)
        {
            return AddDictionaryEntry();
        }

        var entry = CreateDictionaryEntryFromFields();
        if (entry is null)
        {
            ShowDictionaryStatus(Localizer.Get("EditorValueRequiredMessage"));
            return false;
        }

        if (HasDuplicateDictionarySource(entry.Source, _dictionaryEntryList.SelectedIndex))
        {
            ShowDictionaryStatus(Localizer.Get("EditorDuplicateValueMessage"));
            return false;
        }

        var index = _dictionaryEntryList.SelectedIndex;
        _dictionaryEntries[index] = entry;
        _dictionaryEntryList.SelectedIndex = index;
        ClearDetailStatuses();
        return true;
    }

    private void DeleteDictionaryEntry()
    {
        if (_dictionaryEntryList.SelectedIndex >= 0)
        {
            _dictionaryEntries.RemoveAt(_dictionaryEntryList.SelectedIndex);
            _dictionarySourceBox.Text = "";
            _dictionaryReplacementBox.Text = "";
            ClearDetailStatuses();
        }
    }

    private RenameDictionaryEntry? CreateDictionaryEntryFromFields()
    {
        var source = _dictionarySourceBox.Text.Trim();
        if (source.Length == 0)
        {
            return null;
        }

        return new RenameDictionaryEntry
        {
            Source = source,
            Replacement = _dictionaryReplacementBox.Text.Trim()
        };
    }

    private bool HasDuplicateDictionarySource(string source, int exceptIndex)
    {
        return _dictionaryEntries
            .Where((_, index) => index != exceptIndex)
            .Any(entry => string.Equals(entry.Source, source, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadSelectedCommonPhrase()
    {
        _commonPhraseBox.Text = _commonPhraseList.SelectedItem as string ?? "";
        ClearDetailStatuses();
    }

    private bool CommitPendingCommonPhraseEdit()
    {
        var value = _commonPhraseBox.Text.Trim();
        if (_commonPhraseList.SelectedIndex < 0)
        {
            return value.Length == 0 || AddCommonPhrase();
        }

        var current = _commonPhrases[_commonPhraseList.SelectedIndex];
        return string.Equals(current, value, StringComparison.Ordinal) || UpdateCommonPhrase();
    }

    private bool AddCommonPhrase()
    {
        var value = _commonPhraseBox.Text.Trim();
        if (value.Length == 0)
        {
            ShowCommonPhraseStatus(Localizer.Get("EditorValueRequiredMessage"));
            return false;
        }

        if (_commonPhrases.Any(phrase => string.Equals(phrase, value, StringComparison.OrdinalIgnoreCase)))
        {
            ShowCommonPhraseStatus(Localizer.Get("EditorDuplicateValueMessage"));
            return false;
        }

        _commonPhrases.Add(value);
        _commonPhraseList.SelectedItem = value;
        ClearDetailStatuses();
        return true;
    }

    private bool UpdateCommonPhrase()
    {
        if (_commonPhraseList.SelectedIndex < 0)
        {
            return AddCommonPhrase();
        }

        var value = _commonPhraseBox.Text.Trim();
        if (value.Length == 0)
        {
            ShowCommonPhraseStatus(Localizer.Get("EditorValueRequiredMessage"));
            return false;
        }

        if (_commonPhrases.Where((_, index) => index != _commonPhraseList.SelectedIndex)
            .Any(phrase => string.Equals(phrase, value, StringComparison.OrdinalIgnoreCase)))
        {
            ShowCommonPhraseStatus(Localizer.Get("EditorDuplicateValueMessage"));
            return false;
        }

        var index = _commonPhraseList.SelectedIndex;
        _commonPhrases[index] = value;
        _commonPhraseList.SelectedIndex = index;
        ClearDetailStatuses();
        return true;
    }

    private void DeleteCommonPhrase()
    {
        if (_commonPhraseList.SelectedIndex >= 0)
        {
            _commonPhrases.RemoveAt(_commonPhraseList.SelectedIndex);
            _commonPhraseBox.Text = "";
            ClearDetailStatuses();
        }
    }

    private void ShowDictionaryStatus(string message)
    {
        _dictionaryStatusLabel.Text = message;
    }

    private void ShowCommonPhraseStatus(string message)
    {
        _commonPhraseStatusLabel.Text = message;
    }

    private void ClearDetailStatuses()
    {
        _dictionaryStatusLabel.Text = "";
        _commonPhraseStatusLabel.Text = "";
        _knownTagsEditor.ClearStatus();
        _authorPrefixesEditor.ClearStatus();
        _episodePrefixesEditor.ClearStatus();
        _episodeUnitsEditor.ClearStatus();
        _titleNoiseWordsEditor.ClearStatus();
    }

    private bool CanMove(RenameCorrectionRule? rule, int direction)
    {
        if (rule is null)
        {
            return false;
        }

        var stageRules = _rules.Where(item => item.Stage == rule.Stage).OrderBy(item => item.Order).ToList();
        var index = stageRules.FindIndex(item => string.Equals(item.Id, rule.Id, StringComparison.OrdinalIgnoreCase));
        var targetIndex = index + direction;
        return index >= 0 && targetIndex >= 0 && targetIndex < stageRules.Count;
    }

    private int GetNextOrder(RenameCorrectionRuleStage stage)
    {
        return _rules.Where(rule => rule.Stage == stage).Select(static rule => rule.Order).DefaultIfEmpty(0).Max() + 10;
    }

    private static bool RequiresSource(RenameCorrectionRuleKind kind)
    {
        return kind is RenameCorrectionRuleKind.LiteralReplace
            or RenameCorrectionRuleKind.PrefixTrim
            or RenameCorrectionRuleKind.SuffixTrim
            or RenameCorrectionRuleKind.RegexReplace;
    }

    private static T GetComboValue<T>(ComboBox combo, T fallback)
    {
        return combo.SelectedItem is ComboOption<T> option ? option.Value : fallback;
    }

    private static void SelectComboValue<T>(ComboBox combo, T value)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboOption<T> option && EqualityComparer<T>.Default.Equals(option.Value, value))
            {
                combo.SelectedItem = item;
                return;
            }
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

    private sealed class StringListDetailEditor
    {
        private readonly BindingList<string> _values;

        public StringListDetailEditor(BindingList<string> values)
        {
            _values = values;
            ListBox.DataSource = _values;
            ListBox.SelectedIndexChanged += (_, _) => LoadSelected();
            LoadSelected();
        }

        public ListBox ListBox { get; } = new();

        public TextBox TextBox { get; } = new();

        public Label StatusLabel { get; } = new();

        public bool CommitPending()
        {
            var value = TextBox.Text.Trim();
            if (ListBox.SelectedIndex < 0)
            {
                return value.Length == 0 || Add();
            }

            var current = _values[ListBox.SelectedIndex];
            return string.Equals(current, value, StringComparison.Ordinal) || Update();
        }

        public bool Add()
        {
            var value = TextBox.Text.Trim();
            if (value.Length == 0)
            {
                ShowStatus(Localizer.Get("EditorValueRequiredMessage"));
                return false;
            }

            if (_values.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
            {
                ShowStatus(Localizer.Get("EditorDuplicateValueMessage"));
                return false;
            }

            _values.Add(value);
            ListBox.SelectedItem = value;
            ClearStatus();
            return true;
        }

        public bool Update()
        {
            if (ListBox.SelectedIndex < 0)
            {
                return Add();
            }

            var value = TextBox.Text.Trim();
            if (value.Length == 0)
            {
                ShowStatus(Localizer.Get("EditorValueRequiredMessage"));
                return false;
            }

            if (_values.Where((_, index) => index != ListBox.SelectedIndex)
                .Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
            {
                ShowStatus(Localizer.Get("EditorDuplicateValueMessage"));
                return false;
            }

            var index = ListBox.SelectedIndex;
            _values[index] = value;
            ListBox.SelectedIndex = index;
            ClearStatus();
            return true;
        }

        public void Delete()
        {
            if (ListBox.SelectedIndex >= 0)
            {
                _values.RemoveAt(ListBox.SelectedIndex);
                TextBox.Text = "";
                ClearStatus();
            }
        }

        public void ClearStatus()
        {
            StatusLabel.Text = "";
        }

        private void LoadSelected()
        {
            TextBox.Text = ListBox.SelectedItem as string ?? "";
            ClearStatus();
        }

        private void ShowStatus(string message)
        {
            StatusLabel.Text = message;
        }
    }
}
