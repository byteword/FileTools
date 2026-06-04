using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class RenameRuleEditorDialog : Form
{
    private readonly BindingList<RenameDictionaryEntry> _dictionaryEntries;
    private readonly BindingList<string> _commonPhrases;
    private readonly BindingList<string> _candidateScoringWords;
    private readonly BindingList<string> _protectedEnglishWords;
    private readonly StringListDetailEditor _candidateScoringWordsEditor;
    private readonly StringListDetailEditor _protectedEnglishWordsEditor;
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
        RenameParserProfileDocument? parserProfile = null,
        RenameCandidateProfileDocument? candidateProfile = null)
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
        var normalizedCandidateProfile = RenameCandidateProfileStore.Normalize(
            candidateProfile ?? RenameCandidateProfileStore.CreateDefaultDocument(dictionary.CommonPhrases));
        _candidateScoringWords = new BindingList<string>(normalizedCandidateProfile.ObfuscatedHangul.ScoringWords.ToList());
        _protectedEnglishWords = new BindingList<string>(normalizedCandidateProfile.ObfuscatedHangul.ProtectedEnglishWords.ToList());
        _candidateScoringWordsEditor = new StringListDetailEditor(_candidateScoringWords);
        _protectedEnglishWordsEditor = new StringListDetailEditor(_protectedEnglishWords);
        var normalizedParserProfile = RenameParserProfileStore.Normalize(parserProfile ?? RenameParserProfileStore.Load());
        _knownTags = new BindingList<string>(normalizedParserProfile.KnownTags.ToList());
        _authorPrefixes = new BindingList<string>(normalizedParserProfile.AuthorPrefixes.ToList());
        _episodePrefixes = new BindingList<string>(normalizedParserProfile.EpisodePrefixes.ToList());
        _episodeUnits = new BindingList<string>(normalizedParserProfile.EpisodeUnits.ToList());
        _titleNoiseWords = new BindingList<string>(normalizedParserProfile.TitleNoiseWords.ToList());
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

    public RenameCandidateProfileDocument CandidateProfile => RenameCandidateProfileStore.Normalize(new RenameCandidateProfileDocument
    {
        ObfuscatedHangul = new ObfuscatedHangulCandidateProfile
        {
            ScoringWords = _candidateScoringWords.ToList(),
            ProtectedEnglishWords = _protectedEnglishWords.ToList()
        }
    });

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
        if (!CommitPendingDetailEdits())
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

    private bool CommitPendingDetailEdits()
    {
        return CommitPendingDictionaryEdit() &&
            CommitPendingCommonPhraseEdit() &&
            _candidateScoringWordsEditor.CommitPending() &&
            _protectedEnglishWordsEditor.CommitPending() &&
            _knownTagsEditor.CommitPending() &&
            _authorPrefixesEditor.CommitPending() &&
            _episodePrefixesEditor.CommitPending() &&
            _episodeUnitsEditor.CommitPending() &&
            _titleNoiseWordsEditor.CommitPending();
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
        _candidateScoringWordsEditor.ClearStatus();
        _protectedEnglishWordsEditor.ClearStatus();
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
