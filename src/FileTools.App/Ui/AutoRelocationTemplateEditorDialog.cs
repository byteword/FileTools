using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class AutoRelocationTemplateEditorDialog : Form
{
    private readonly ListBox _templateList = new();
    private readonly TextBox _idBox = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _descriptionBox = new();
    private readonly ListBox _pathRuleList = new();
    private readonly ComboBox _pathSourceCombo = new();
    private readonly ComboBox _pathTransformCombo = new();
    private readonly ComboBox _pathLanguageCombo = new();
    private readonly TextBox _pathFormatBox = new();
    private readonly TextBox _pathFallbackBox = new();
    private readonly CheckBox _prefilterEnabledCheckBox = new();
    private readonly ComboBox _prefilterSourceCombo = new();
    private readonly ComboBox _prefilterOperatorCombo = new();
    private readonly TextBox _prefilterValueBox = new();
    private readonly ComboBox _prefilterActionCombo = new();
    private readonly TextBox _prefilterTargetBox = new();
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
        BindCombos();
        LoadTemplateList(AutoRelocationTemplateDefaults.DefaultTemplateId);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 2,
            RowCount = 2
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(BuildTemplateListPanel(), 0, 0);
        root.Controls.Add(BuildEditorPanel(), 1, 0);

        var dialogButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var closeButton = new Button { Text = Localizer.Get("ButtonClose"), DialogResult = DialogResult.OK, Width = 90 };
        dialogButtons.Controls.Add(closeButton);
        root.SetColumnSpan(dialogButtons, 2);
        root.Controls.Add(dialogButtons, 0, 1);

        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    private Control BuildTemplateListPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 0, 8, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        _templateList.Dock = DockStyle.Fill;
        _templateList.DisplayMember = nameof(TemplateListItem.DisplayText);
        _templateList.SelectedIndexChanged += (_, _) => LoadSelectedTemplate();
        panel.Controls.Add(_templateList, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false
        };
        var newButton = new Button { Text = Localizer.Get("ButtonNew"), Width = 80, Height = 30 };
        var deleteButton = new Button { Text = Localizer.Get("ButtonDelete"), Width = 80, Height = 30 };
        newButton.Click += (_, _) => CreateNewTemplate();
        deleteButton.Click += (_, _) => DeleteSelectedTemplate();
        buttons.Controls.Add(newButton);
        buttons.Controls.Add(deleteButton);
        panel.Controls.Add(buttons, 0, 1);

        return panel;
    }

    private Control BuildEditorPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        panel.Controls.Add(CreateGroup(Localizer.Get("GroupTemplateGeneral"),
            CreateTextRow(Localizer.Get("LabelId"), _idBox),
            CreateTextRow(Localizer.Get("LabelName"), _nameBox),
            CreateTextRow(Localizer.Get("LabelDescription"), _descriptionBox)));

        panel.Controls.Add(BuildPathRulesGroup());

        panel.Controls.Add(CreateGroup(Localizer.Get("GroupPrefilter"),
            CreateCheckRow(_prefilterEnabledCheckBox, Localizer.Get("CheckEnablePrefilter")),
            CreateComboRow(Localizer.Get("LabelSource"), _prefilterSourceCombo),
            CreateComboRow(Localizer.Get("LabelOperator"), _prefilterOperatorCombo),
            CreateTextRow(Localizer.Get("LabelValue"), _prefilterValueBox),
            CreateComboRow(Localizer.Get("LabelAction"), _prefilterActionCombo),
            CreateTextRow(Localizer.Get("LabelTargetFolder"), _prefilterTargetBox)));

        var saveButton = new Button
        {
            Text = Localizer.Get("ButtonSaveTemplate"),
            Width = 160,
            Height = 30
        };
        saveButton.Click += (_, _) => SaveTemplate();
        panel.Controls.Add(saveButton);

        return panel;
    }

    private Control BuildPathRulesGroup()
    {
        var group = new GroupBox
        {
            Text = Localizer.Get("GroupPathRules"),
            Width = 760,
            Height = 252,
            Padding = new Padding(12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        group.Controls.Add(layout);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        layout.Controls.Add(left, 0, 0);

        _pathRuleList.Dock = DockStyle.Fill;
        _pathRuleList.DisplayMember = nameof(PathRuleListItem.DisplayText);
        _pathRuleList.SelectedIndexChanged += (_, _) => SelectPathRule(_pathRuleList.SelectedIndex);
        left.Controls.Add(_pathRuleList, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = true
        };
        var addButton = new Button { Text = Localizer.Get("ButtonAdd"), Width = 62, Height = 28 };
        var deleteButton = new Button { Text = Localizer.Get("ButtonDelete"), Width = 62, Height = 28 };
        var upButton = new Button { Text = Localizer.Get("ButtonMoveUp"), Width = 62, Height = 28 };
        var downButton = new Button { Text = Localizer.Get("ButtonMoveDown"), Width = 62, Height = 28 };
        addButton.Click += (_, _) => AddPathRule();
        deleteButton.Click += (_, _) => DeleteSelectedPathRule();
        upButton.Click += (_, _) => MoveSelectedPathRule(-1);
        downButton.Click += (_, _) => MoveSelectedPathRule(1);
        buttons.Controls.Add(addButton);
        buttons.Controls.Add(deleteButton);
        buttons.Controls.Add(upButton);
        buttons.Controls.Add(downButton);
        left.Controls.Add(buttons, 0, 1);

        var detail = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(10, 0, 0, 0)
        };
        for (var i = 0; i < 5; i++)
        {
            detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        }

        detail.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        detail.Controls.Add(CreateCompactComboRow(Localizer.Get("LabelSource"), _pathSourceCombo), 0, 0);
        detail.Controls.Add(CreateCompactComboRow(Localizer.Get("LabelTransform"), _pathTransformCombo), 0, 1);
        detail.Controls.Add(CreateCompactComboRow(Localizer.Get("LabelLanguage"), _pathLanguageCombo), 0, 2);
        detail.Controls.Add(CreateCompactTextRow(Localizer.Get("LabelFormat"), _pathFormatBox), 0, 3);
        detail.Controls.Add(CreateCompactTextRow(Localizer.Get("LabelFallback"), _pathFallbackBox), 0, 4);

        var updateButton = new Button
        {
            Text = Localizer.Get("ButtonUpdate"),
            Width = 110,
            Height = 28
        };
        updateButton.Click += (_, _) => UpdateSelectedPathRule();
        detail.Controls.Add(updateButton, 0, 5);
        layout.Controls.Add(detail, 1, 0);

        return group;
    }

    private void BindCombos()
    {
        BindEnumCombo(_pathSourceCombo, AutoRelocationTemplateDefaults.FileDerivedValueSources, AutoRelocationValueSource.Title);
        BindEnumCombo(_pathTransformCombo, AutoRelocationValueTransform.InitialBucket);
        BindEnumCombo(_pathLanguageCombo, AutoRelocationLanguageProfile.KoreanEnglish);
        BindEnumCombo(_prefilterSourceCombo, AutoRelocationTemplateDefaults.FileDerivedValueSources, AutoRelocationValueSource.FileName);
        BindEnumCombo(_prefilterOperatorCombo, AutoRelocationFilterOperator.Contains);
        BindEnumCombo(_prefilterActionCombo, AutoRelocationPrefilterAction.ReviewOnly);
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

        var prefilter = document.Prefilters.FirstOrDefault();
        _prefilterEnabledCheckBox.Checked = prefilter?.Enabled ?? false;
        SelectComboValue(_prefilterSourceCombo, prefilter?.Source ?? AutoRelocationValueSource.FileName);
        SelectComboValue(_prefilterOperatorCombo, prefilter?.Operator ?? AutoRelocationFilterOperator.Contains);
        _prefilterValueBox.Text = prefilter?.Value ?? "";
        SelectComboValue(_prefilterActionCombo, prefilter?.Action ?? AutoRelocationPrefilterAction.ReviewOnly);
        _prefilterTargetBox.Text = prefilter?.TargetFolderName ?? "";
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

    private static GroupBox CreateGroup(string text, params Control[] controls)
    {
        var group = new GroupBox
        {
            Text = text,
            Width = 760,
            Height = 32 + controls.Length * 36,
            Padding = new Padding(12)
        };

        var top = 24;
        foreach (var control in controls)
        {
            control.Left = 12;
            control.Top = top;
            group.Controls.Add(control);
            top += 36;
        }

        return group;
    }

    private static Control CreateTextRow(string labelText, TextBox textBox)
    {
        var panel = new Panel { Width = 720, Height = 32 };
        panel.Controls.Add(new Label
        {
            Text = labelText,
            Left = 0,
            Top = 5,
            Width = 170,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        });
        textBox.Left = 180;
        textBox.Top = 3;
        textBox.Width = 500;
        panel.Controls.Add(textBox);
        return panel;
    }

    private static Control CreateComboRow(string labelText, ComboBox combo)
    {
        var panel = new Panel { Width = 720, Height = 32 };
        panel.Controls.Add(new Label
        {
            Text = labelText,
            Left = 0,
            Top = 5,
            Width = 170,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        });
        combo.Left = 180;
        combo.Top = 3;
        combo.Width = 500;
        panel.Controls.Add(combo);
        return panel;
    }

    private static Control CreateCompactTextRow(string labelText, TextBox textBox)
    {
        var panel = new Panel { Width = 430, Height = 32 };
        panel.Controls.Add(new Label
        {
            Text = labelText,
            Left = 0,
            Top = 5,
            Width = 110,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        });
        textBox.Left = 120;
        textBox.Top = 3;
        textBox.Width = 280;
        panel.Controls.Add(textBox);
        return panel;
    }

    private static Control CreateCompactComboRow(string labelText, ComboBox combo)
    {
        var panel = new Panel { Width = 430, Height = 32 };
        panel.Controls.Add(new Label
        {
            Text = labelText,
            Left = 0,
            Top = 5,
            Width = 110,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        });
        combo.Left = 120;
        combo.Top = 3;
        combo.Width = 280;
        panel.Controls.Add(combo);
        return panel;
    }

    private static Control CreateCheckRow(CheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.Width = 700;
        checkBox.Height = 28;
        return checkBox;
    }

    private static void BindEnumCombo<T>(ComboBox combo, T selectedValue)
        where T : struct, Enum
    {
        BindEnumCombo(combo, Enum.GetValues<T>(), selectedValue);
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
