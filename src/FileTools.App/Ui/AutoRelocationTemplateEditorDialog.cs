using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class AutoRelocationTemplateEditorDialog : Form
{
    private readonly ListBox _templateList = new();
    private readonly TextBox _idBox = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _descriptionBox = new();
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

    private bool _loading;
    private string? _loadedTemplateId;
    private AutoRelocationTemplateDocument? _loadedDocument;

    public AutoRelocationTemplateEditorDialog()
    {
        Text = Localizer.Get("DialogTemplateEditorTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 840;
        Height = 620;
        MinimumSize = new Size(760, 520);

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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
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

        panel.Controls.Add(CreateGroup(Localizer.Get("GroupPathRule"),
            CreateComboRow(Localizer.Get("LabelSource"), _pathSourceCombo),
            CreateComboRow(Localizer.Get("LabelTransform"), _pathTransformCombo),
            CreateComboRow(Localizer.Get("LabelLanguage"), _pathLanguageCombo),
            CreateTextRow(Localizer.Get("LabelFormat"), _pathFormatBox),
            CreateTextRow(Localizer.Get("LabelFallback"), _pathFallbackBox)));

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

    private void BindCombos()
    {
        BindEnumCombo(_pathSourceCombo, AutoRelocationValueSource.Title);
        BindEnumCombo(_pathTransformCombo, AutoRelocationValueTransform.InitialBucket);
        BindEnumCombo(_pathLanguageCombo, AutoRelocationLanguageProfile.KoreanEnglish);
        BindEnumCombo(_prefilterSourceCombo, AutoRelocationValueSource.Tags);
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

        var pathRule = document.PathRules.FirstOrDefault() ??
            AutoRelocationTemplateDefaults.CreateDefaultTemplate().PathRules[0];
        SelectComboValue(_pathSourceCombo, pathRule.Source);
        SelectComboValue(_pathTransformCombo, pathRule.Transform);
        SelectComboValue(_pathLanguageCombo, pathRule.Language);
        _pathFormatBox.Text = pathRule.Format;
        _pathFallbackBox.Text = pathRule.FallbackFolderName;

        var prefilter = document.Prefilters.FirstOrDefault();
        _prefilterEnabledCheckBox.Checked = prefilter?.Enabled ?? false;
        SelectComboValue(_prefilterSourceCombo, prefilter?.Source ?? AutoRelocationValueSource.Tags);
        SelectComboValue(_prefilterOperatorCombo, prefilter?.Operator ?? AutoRelocationFilterOperator.Contains);
        _prefilterValueBox.Text = prefilter?.Value ?? "";
        SelectComboValue(_prefilterActionCombo, prefilter?.Action ?? AutoRelocationPrefilterAction.ReviewOnly);
        _prefilterTargetBox.Text = prefilter?.TargetFolderName ?? "";
    }

    private void CreateNewTemplate()
    {
        var existingIds = AutoRelocationTemplateStore.LoadTemplates()
            .Select(static template => template.Document.Id)
            .ToArray();
        var displayName = Localizer.Get("NewTemplateDisplayName");
        var id = AutoRelocationTemplateStore.CreateUniqueTemplateId(displayName, existingIds);
        var defaultRule = AutoRelocationTemplateDefaults.CreateDefaultTemplate().PathRules[0];
        _templateList.ClearSelected();
        LoadDocument(new AutoRelocationTemplateDocument
        {
            Id = id,
            DisplayName = displayName,
            PathRules = [defaultRule]
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
        var id = AutoRelocationTemplateStore.NormalizeTemplateId(_idBox.Text);
        var pathRule = new AutoRelocationPathRule
        {
            Source = GetComboValue(_pathSourceCombo, AutoRelocationValueSource.Title),
            Transform = GetComboValue(_pathTransformCombo, AutoRelocationValueTransform.InitialBucket),
            Language = GetComboValue(_pathLanguageCombo, AutoRelocationLanguageProfile.KoreanEnglish),
            Format = string.IsNullOrWhiteSpace(_pathFormatBox.Text) ? "{value}" : _pathFormatBox.Text.Trim(),
            FallbackFolderName = string.IsNullOrWhiteSpace(_pathFallbackBox.Text) ? "[ETC]" : _pathFallbackBox.Text.Trim()
        };

        var pathRules = new List<AutoRelocationPathRule> { pathRule };
        if (_loadedDocument is not null)
        {
            pathRules.AddRange(_loadedDocument.PathRules.Skip(1));
        }

        var prefilters = new List<AutoRelocationPrefilterRule>();
        if (_prefilterEnabledCheckBox.Checked)
        {
            prefilters.Add(new AutoRelocationPrefilterRule
            {
                Enabled = true,
                Source = GetComboValue(_prefilterSourceCombo, AutoRelocationValueSource.Tags),
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

    private static GroupBox CreateGroup(string text, params Control[] controls)
    {
        var group = new GroupBox
        {
            Text = text,
            Width = 540,
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
        var panel = new Panel { Width = 500, Height = 32 };
        panel.Controls.Add(new Label
        {
            Text = labelText,
            Left = 0,
            Top = 5,
            Width = 150,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        });
        textBox.Left = 160;
        textBox.Top = 3;
        textBox.Width = 310;
        panel.Controls.Add(textBox);
        return panel;
    }

    private static Control CreateComboRow(string labelText, ComboBox combo)
    {
        var panel = new Panel { Width = 500, Height = 32 };
        panel.Controls.Add(new Label
        {
            Text = labelText,
            Left = 0,
            Top = 5,
            Width = 150,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        });
        combo.Left = 160;
        combo.Top = 3;
        combo.Width = 310;
        panel.Controls.Add(combo);
        return panel;
    }

    private static Control CreateCheckRow(CheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.Width = 470;
        checkBox.Height = 28;
        return checkBox;
    }

    private static void BindEnumCombo<T>(ComboBox combo, T selectedValue)
        where T : struct, Enum
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DataSource = Enum.GetValues<T>()
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
}
