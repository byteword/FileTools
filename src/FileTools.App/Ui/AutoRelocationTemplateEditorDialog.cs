using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class AutoRelocationTemplateEditorDialog : Form
{

    private readonly List<AutoRelocationPathRule> _pathRules = [];

    private bool _loading;
    private bool _loadingPathRule;
    private int _currentPathRuleIndex = -1;
    private string? _loadedTemplateId;
    private AutoRelocationTemplateDocument? _loadedDocument;

    public AutoRelocationTemplateEditorDialog()
    {
        Text = Localizer.Get("DialogTemplateEditorTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 1080;
        Height = 700;
        MinimumSize = new Size(920, 600);

        BuildLayout();
        WireToolTipUpdates();
        BindCombos();
        LoadTemplateList(AutoRelocationTemplateDefaults.DefaultTemplateId);
    }

    private void LoadTemplateList(string? selectedTemplateId)
    {
        var items = AutoRelocationTemplateStore.LoadTemplates()
            .Select(static template => new TemplateListItem(template))
            .ToList();

        _loading = true;
        _templateList.DataSource = null;
        _templateList.DataSource = items;
        _loading = false;

        if (items.Count == 0)
        {
            return;
        }

        var selected = items.FirstOrDefault(item => string.Equals(
            item.File.Document.Id,
            selectedTemplateId,
            StringComparison.OrdinalIgnoreCase)) ?? items[0];
        _templateList.SelectedItem = selected;
        LoadDocument(selected.File.Document);
    }

    private void LoadSelectedTemplate()
    {
        if (_loading || _templateList.SelectedItem is not TemplateListItem item)
        {
            return;
        }

        LoadDocument(item.File.Document);
    }

    private void LoadDocument(AutoRelocationTemplateDocument document)
    {
        _loadedTemplateId = document.Id;
        _loadedDocument = document;
        _idBox.Text = document.Id;
        _nameBox.Text = document.DisplayName;
        _descriptionBox.Text = document.Description ?? "";

        _pathRules.Clear();
        _pathRules.AddRange(document.PathRules.Count == 0
            ? [CreateDefaultPathRule()]
            : document.PathRules.Select(ClonePathRule));
        RefreshPathRuleList(0);
        UpdateTemplateFieldToolTips();

        var prefilter = document.Prefilters.FirstOrDefault();
        _prefilterEnabledCheckBox.Checked = prefilter?.Enabled ?? false;
        SelectComboValue(_prefilterSourceCombo, prefilter?.Source ?? AutoRelocationValueSource.FileName);
        SelectComboValue(_prefilterOperatorCombo, prefilter?.Operator ?? AutoRelocationFilterOperator.Contains);
        _prefilterValueBox.Text = prefilter?.Value ?? "";
        SelectComboValue(_prefilterActionCombo, prefilter?.Action ?? AutoRelocationPrefilterAction.ReviewOnly);
        _prefilterTargetBox.Text = prefilter?.TargetFolderName ?? "";
        UpdateTemplateFieldToolTips();
    }

    private void SelectPathRule(int nextIndex)
    {
        if (_loadingPathRule)
        {
            return;
        }

        SaveCurrentPathRule();
        LoadPathRuleFields(nextIndex);
    }

    private void LoadPathRuleFields(int index)
    {
        _loadingPathRule = true;
        _currentPathRuleIndex = index >= 0 && index < _pathRules.Count ? index : -1;

        var rule = _currentPathRuleIndex >= 0
            ? _pathRules[_currentPathRuleIndex]
            : CreateDefaultPathRule();
        SelectComboValue(_pathSourceCombo, rule.Source);
        SelectComboValue(_pathTransformCombo, rule.Transform);
        SelectComboValue(_pathLanguageCombo, rule.Language);
        _pathFormatBox.Text = rule.Format;
        _pathFallbackBox.Text = rule.FallbackFolderName;
        _loadingPathRule = false;
    }

    private void SaveCurrentPathRule()
    {
        if (_loadingPathRule || _currentPathRuleIndex < 0 || _currentPathRuleIndex >= _pathRules.Count)
        {
            return;
        }

        _pathRules[_currentPathRuleIndex] = CreatePathRuleFromFields();
    }

    private void RefreshPathRuleList(int selectedIndex)
    {
        _loadingPathRule = true;
        _pathRuleList.DataSource = null;
        _pathRuleList.DataSource = _pathRules
            .Select((rule, index) => new PathRuleListItem(index, rule))
            .ToArray();
        _loadingPathRule = false;

        if (_pathRules.Count == 0)
        {
            LoadPathRuleFields(-1);
            return;
        }

        var index = Math.Clamp(selectedIndex, 0, _pathRules.Count - 1);
        _pathRuleList.SelectedIndex = index;
        LoadPathRuleFields(index);
    }

    private void AddPathRule()
    {
        SaveCurrentPathRule();
        _pathRules.Add(CreateDefaultPathRule());
        RefreshPathRuleList(_pathRules.Count - 1);
    }

    private void DeleteSelectedPathRule()
    {
        if (_currentPathRuleIndex < 0 || _currentPathRuleIndex >= _pathRules.Count)
        {
            return;
        }

        _pathRules.RemoveAt(_currentPathRuleIndex);
        if (_pathRules.Count == 0)
        {
            _pathRules.Add(CreateDefaultPathRule());
        }

        RefreshPathRuleList(Math.Min(_currentPathRuleIndex, _pathRules.Count - 1));
    }

    private void MoveSelectedPathRule(int direction)
    {
        if (_currentPathRuleIndex < 0 || _currentPathRuleIndex >= _pathRules.Count)
        {
            return;
        }

        SaveCurrentPathRule();
        var nextIndex = _currentPathRuleIndex + direction;
        if (nextIndex < 0 || nextIndex >= _pathRules.Count)
        {
            return;
        }

        (_pathRules[_currentPathRuleIndex], _pathRules[nextIndex]) = (_pathRules[nextIndex], _pathRules[_currentPathRuleIndex]);
        RefreshPathRuleList(nextIndex);
    }

    private void UpdateSelectedPathRule()
    {
        SaveCurrentPathRule();
        RefreshPathRuleList(_currentPathRuleIndex);
    }

    private void CreateNewTemplate()
    {
        var existingIds = AutoRelocationTemplateStore.LoadTemplates()
            .Select(static template => template.Document.Id)
            .ToArray();
        var displayName = Localizer.Get("NewTemplateDisplayName");
        var id = AutoRelocationTemplateStore.CreateUniqueTemplateId(displayName, existingIds);
        _templateList.ClearSelected();
        LoadDocument(new AutoRelocationTemplateDocument
        {
            Id = id,
            DisplayName = displayName,
            PathRules = [CreateDefaultPathRule()]
        });
        _loadedTemplateId = null;
        _loadedDocument = null;
    }

    private void DeleteSelectedTemplate()
    {
        if (_templateList.SelectedItem is not TemplateListItem item)
        {
            return;
        }

        var templateId = item.File.Document.Id;
        if (string.Equals(templateId, AutoRelocationTemplateDefaults.DefaultTemplateId, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                Localizer.Get("DefaultTemplateCannotDelete"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(
                Localizer.Get("DeleteTemplateQuestion"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        AutoRelocationTemplateStore.DeleteTemplate(templateId);
        LoadTemplateList(AutoRelocationTemplateDefaults.DefaultTemplateId);
    }

    private void SaveTemplate()
    {
        try
        {
            var document = CreateDocumentFromFields();
            var saved = AutoRelocationTemplateStore.SaveTemplate(document, _loadedTemplateId);
            LoadTemplateList(saved.Document.Id);
            MessageBox.Show(
                Localizer.Format("TemplateSavedFormat", saved.FilePath),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private AutoRelocationTemplateDocument CreateDocumentFromFields()
    {
        SaveCurrentPathRule();
        var id = AutoRelocationTemplateStore.NormalizeTemplateId(_idBox.Text);
        var pathRules = _pathRules.Count == 0
            ? [CreateDefaultPathRule()]
            : _pathRules.Select(ClonePathRule).ToList();

        var prefilters = new List<AutoRelocationPrefilterRule>();
        if (_prefilterEnabledCheckBox.Checked)
        {
            prefilters.Add(new AutoRelocationPrefilterRule
            {
                Enabled = true,
                Source = GetComboValue(_prefilterSourceCombo, AutoRelocationValueSource.FileName),
                Operator = GetComboValue(_prefilterOperatorCombo, AutoRelocationFilterOperator.Contains),
                Value = _prefilterValueBox.Text.Trim(),
                Action = GetComboValue(_prefilterActionCombo, AutoRelocationPrefilterAction.ReviewOnly),
                TargetFolderName = string.IsNullOrWhiteSpace(_prefilterTargetBox.Text)
                    ? null
                    : _prefilterTargetBox.Text.Trim()
            });
        }

        if (_loadedDocument is not null)
        {
            prefilters.AddRange(_loadedDocument.Prefilters.Skip(1));
        }

        return new AutoRelocationTemplateDocument
        {
            Id = id,
            DisplayName = string.IsNullOrWhiteSpace(_nameBox.Text) ? id : _nameBox.Text.Trim(),
            Description = string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text.Trim(),
            PathRules = pathRules,
            Prefilters = prefilters
        };
    }

    private AutoRelocationPathRule CreatePathRuleFromFields()
    {
        return new AutoRelocationPathRule
        {
            Source = GetComboValue(_pathSourceCombo, AutoRelocationValueSource.Title),
            Transform = GetComboValue(_pathTransformCombo, AutoRelocationValueTransform.InitialBucket),
            Language = GetComboValue(_pathLanguageCombo, AutoRelocationLanguageProfile.KoreanEnglish),
            Format = string.IsNullOrWhiteSpace(_pathFormatBox.Text) ? "{value}" : _pathFormatBox.Text.Trim(),
            FallbackFolderName = string.IsNullOrWhiteSpace(_pathFallbackBox.Text) ? "[ETC]" : _pathFallbackBox.Text.Trim()
        };
    }

    private static AutoRelocationPathRule CreateDefaultPathRule()
    {
        return ClonePathRule(AutoRelocationTemplateDefaults.CreateDefaultTemplate().PathRules[0]);
    }

    private static AutoRelocationPathRule ClonePathRule(AutoRelocationPathRule rule)
    {
        return new AutoRelocationPathRule
        {
            Enabled = rule.Enabled,
            Source = AutoRelocationTemplateDefaults.NormalizeValueSource(rule.Source),
            Transform = rule.Transform,
            Language = rule.Language,
            Format = string.IsNullOrWhiteSpace(rule.Format) ? "{value}" : rule.Format.Trim(),
            FallbackFolderName = string.IsNullOrWhiteSpace(rule.FallbackFolderName) ? "[ETC]" : rule.FallbackFolderName.Trim(),
            Options = rule.Options
        };
    }

    private static void BindEnumCombo<T>(ComboBox combo, IEnumerable<T> values, T selectedValue)
        where T : struct, Enum
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DataSource = values
            .Select(static value => new ComboOption<T>(value.ToString(), value))
            .ToArray();
        SelectComboValue(combo, selectedValue);
    }

    private static T GetComboValue<T>(ComboBox combo, T fallback)
        where T : notnull
    {
        return combo.SelectedItem is ComboOption<T> option ? option.Value : fallback;
    }

    private static void SelectComboValue<T>(ComboBox combo, T value)
        where T : notnull
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboOption<T> option && EqualityComparer<T>.Default.Equals(option.Value, value))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private sealed record TemplateListItem(AutoRelocationTemplateFile File)
    {
        public string DisplayText => $"{File.Document.DisplayName} ({File.Document.Id})";
    }

    private sealed record PathRuleListItem(int Index, AutoRelocationPathRule Rule)
    {
        public string DisplayText =>
            $"{Index + 1}. {Rule.Source} / {Rule.Transform} / {Rule.Format}";
    }
}
