using System.Windows.Forms;

namespace FileTools;

internal sealed class NameTemplateSettingsDialog : Form
{
    private readonly TextBox _wrapTemplateBox = new();
    private readonly TextBox _unwrapTemplateBox = new();
    private readonly ComboBox _conflictPolicyCombo = new();
    private readonly TextBox _conflictTemplateBox = new();
    private readonly ComboBox _indexStyleCombo = new();
    private readonly Label _previewLabel = new();
    private readonly ToolTip _toolTip = new();

    public NameTemplateSettingsDialog(FileToolsSettings settings)
    {
        Settings = settings.Clone();
        Text = Localizer.Get("DialogNameTemplateSettingsTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 430);
        MinimumSize = new Size(680, 380);
        MinimizeBox = false;
        MaximizeBox = false;

        BuildLayout();
        LoadSettings();
        UpdatePreview();
    }

    public FileToolsSettings Settings { get; private set; }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(CreateTemplateGroup(), 0, 0);
        root.Controls.Add(CreateCollisionGroup(), 0, 1);
        root.Controls.Add(CreatePreviewGroup(), 0, 2);
        root.Controls.Add(CreateButtonPanel(), 0, 3);

        foreach (var textBox in new[] { _wrapTemplateBox, _unwrapTemplateBox, _conflictTemplateBox })
        {
            textBox.TextChanged += (_, _) => UpdatePreview();
        }

        foreach (var combo in new[] { _conflictPolicyCombo, _indexStyleCombo })
        {
            combo.SelectedIndexChanged += (_, _) => UpdatePreview();
        }
    }

    private Control CreateTemplateGroup()
    {
        var group = new GroupBox
        {
            Text = Localizer.Get("GroupFolderNameTemplates"),
            Dock = DockStyle.Top,
            Height = 116,
            Padding = new Padding(10)
        };
        var panel = CreateRowsPanel();
        panel.Controls.Add(CreateTextRow(Localizer.Get("LabelFolderWrapTemplate"), _wrapTemplateBox), 0, 0);
        panel.Controls.Add(CreateTextRow(Localizer.Get("LabelFolderUnwrapTemplate"), _unwrapTemplateBox), 0, 1);
        group.Controls.Add(panel);
        return group;
    }

    private Control CreateCollisionGroup()
    {
        var group = new GroupBox
        {
            Text = Localizer.Get("GroupNameCollisionPolicy"),
            Dock = DockStyle.Top,
            Height = 148,
            Padding = new Padding(10),
            Margin = new Padding(0, 8, 0, 0)
        };
        var panel = CreateRowsPanel();
        panel.Controls.Add(CreateComboRow(Localizer.Get("LabelNameCollisionPolicy"), _conflictPolicyCombo), 0, 0);
        panel.Controls.Add(CreateTextRow(Localizer.Get("LabelConflictNameTemplate"), _conflictTemplateBox), 0, 1);
        panel.Controls.Add(CreateComboRow(Localizer.Get("LabelConflictIndexStyle"), _indexStyleCombo), 0, 2);
        group.Controls.Add(panel);
        return group;
    }

    private Control CreatePreviewGroup()
    {
        var group = new GroupBox
        {
            Text = Localizer.Get("GroupNameTemplatePreview"),
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Margin = new Padding(0, 8, 0, 0)
        };
        _previewLabel.Dock = DockStyle.Fill;
        _previewLabel.AutoSize = false;
        _previewLabel.TextAlign = ContentAlignment.TopLeft;
        group.Controls.Add(_previewLabel);
        return group;
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
        var okButton = new Button
        {
            Text = "OK",
            Width = 94,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 94,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        var resetButton = new Button
        {
            Text = Localizer.Get("ButtonResetDefaults"),
            Width = 130,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        okButton.Click += (_, _) => SaveAndClose();
        resetButton.Click += (_, _) => ResetDefaults();
        panel.Controls.Add(cancelButton);
        panel.Controls.Add(okButton);
        panel.Controls.Add(resetButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return panel;
    }

    private static TableLayoutPanel CreateRowsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(0)
        };
        return panel;
    }

    private static Panel CreateTextRow(string labelText, TextBox textBox)
    {
        var panel = CreateRowPanel();
        var label = CreateRowLabel(labelText);
        textBox.Left = 210;
        textBox.Top = 3;
        textBox.Height = 26;
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(label);
        panel.Controls.Add(textBox);
        panel.Resize += (_, _) => ResizeRow(panel, label, textBox);
        ResizeRow(panel, label, textBox);
        return panel;
    }

    private static Panel CreateComboRow(string labelText, ComboBox combo)
    {
        var panel = CreateRowPanel();
        var label = CreateRowLabel(labelText);
        combo.Left = 210;
        combo.Top = 3;
        combo.Height = 26;
        combo.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(label);
        panel.Controls.Add(combo);
        panel.Resize += (_, _) => ResizeRow(panel, label, combo);
        ResizeRow(panel, label, combo);
        return panel;
    }

    private static Panel CreateRowPanel()
    {
        return new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            Margin = new Padding(0, 0, 0, 6)
        };
    }

    private static Label CreateRowLabel(string text)
    {
        return new Label
        {
            Text = text,
            Left = 0,
            Top = 4,
            Width = 196,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static void ResizeRow(Panel panel, Control label, Control editor)
    {
        var labelWidth = Math.Clamp(panel.ClientSize.Width / 3, 170, 220);
        label.Width = labelWidth;
        editor.Left = labelWidth + 14;
        editor.Width = Math.Max(220, panel.ClientSize.Width - editor.Left);
    }

    private void LoadSettings()
    {
        _wrapTemplateBox.Text = Settings.FolderWrapFolderNameTemplate;
        _unwrapTemplateBox.Text = Settings.FolderUnwrapMismatchFileNameTemplate;
        _conflictTemplateBox.Text = Settings.FolderStructureConflictNameTemplate;

        _conflictPolicyCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _conflictPolicyCombo.DataSource = new[]
        {
            NameCollisionPolicy.Skip,
            NameCollisionPolicy.AutoNumber
        }
            .Select(policy => new ComboOption<NameCollisionPolicy>(NameTemplateText.GetDisplayName(policy), policy))
            .ToArray();
        SelectComboValue(_conflictPolicyCombo, Settings.FolderStructureConflictPolicy);

        _indexStyleCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _indexStyleCombo.DataSource = Enum.GetValues<ConflictIndexStyle>()
            .Select(style => new ComboOption<ConflictIndexStyle>(NameTemplateText.GetDisplayName(style), style))
            .ToArray();
        SelectComboValue(_indexStyleCombo, Settings.FolderStructureConflictIndexStyle);

        _toolTip.SetToolTip(_wrapTemplateBox, Localizer.Get("NameTemplateTokensHelp"));
        _toolTip.SetToolTip(_unwrapTemplateBox, Localizer.Get("NameTemplateTokensHelp"));
        _toolTip.SetToolTip(_conflictTemplateBox, Localizer.Get("ConflictTemplateTokensHelp"));
    }

    private void SaveAndClose()
    {
        var wrapTemplate = NormalizeTemplate(_wrapTemplateBox.Text, NameTemplateDefaults.FolderWrapFolderNameTemplate);
        var unwrapTemplate = NormalizeTemplate(_unwrapTemplateBox.Text, NameTemplateDefaults.FolderUnwrapPrefixFolderNameTemplate);
        var conflictTemplate = NormalizeTemplate(_conflictTemplateBox.Text, NameTemplateDefaults.DefaultConflictNameTemplate);
        var validation = ValidateTemplates(wrapTemplate, unwrapTemplate, conflictTemplate);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            MessageBox.Show(validation, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Settings.FolderWrapFolderNameTemplate = wrapTemplate;
        Settings.FolderUnwrapMismatchFileNameTemplate = unwrapTemplate;
        Settings.FolderStructureConflictNameTemplate = conflictTemplate;
        if (_conflictPolicyCombo.SelectedItem is ComboOption<NameCollisionPolicy> policy)
        {
            Settings.FolderStructureConflictPolicy = policy.Value;
        }

        if (_indexStyleCombo.SelectedItem is ComboOption<ConflictIndexStyle> indexStyle)
        {
            Settings.FolderStructureConflictIndexStyle = indexStyle.Value;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ResetDefaults()
    {
        _wrapTemplateBox.Text = NameTemplateDefaults.FolderWrapFolderNameTemplate;
        _unwrapTemplateBox.Text = NameTemplateDefaults.FolderUnwrapPrefixFolderNameTemplate;
        _conflictTemplateBox.Text = NameTemplateDefaults.DefaultConflictNameTemplate;
        SelectComboValue(_conflictPolicyCombo, NameCollisionPolicy.Skip);
        SelectComboValue(_indexStyleCombo, ConflictIndexStyle.Number);
    }

    private void UpdatePreview()
    {
        var wrapTemplate = NormalizeTemplate(_wrapTemplateBox.Text, NameTemplateDefaults.FolderWrapFolderNameTemplate);
        var unwrapTemplate = NormalizeTemplate(_unwrapTemplateBox.Text, NameTemplateDefaults.FolderUnwrapPrefixFolderNameTemplate);
        var conflictTemplate = NormalizeTemplate(_conflictTemplateBox.Text, NameTemplateDefaults.DefaultConflictNameTemplate);
        var resolver = NameTemplateResolver.CreateDefault(Settings);
        var wrap = resolver.Evaluate(wrapTemplate, NameTemplateContext.FromFile(@"C:\Sample\Book 01.zip"));
        var unwrap = resolver.Evaluate(unwrapTemplate, NameTemplateContext.FromFolderChild(@"C:\Sample\FolderA", "Image01.jpg"));
        var policy = _conflictPolicyCombo.SelectedItem is ComboOption<NameCollisionPolicy> selectedPolicy
            ? selectedPolicy.Value
            : NameCollisionPolicy.Skip;
        var indexStyle = _indexStyleCombo.SelectedItem is ComboOption<ConflictIndexStyle> selectedStyle
            ? selectedStyle.Value
            : ConflictIndexStyle.Number;
        var conflictContext = NameTemplateContext.FromNameParts("Book.zip", "Book", ".zip") with
        {
            Index = 2,
            IndexLabel = ConflictIndexFormatter.Format(2, indexStyle)
        };
        var conflict = resolver.Evaluate(conflictTemplate, conflictContext);
        _previewLabel.Text = string.Join(
            Environment.NewLine,
            Localizer.Format("NameTemplatePreviewWrapFormat", FormatPreview(wrap)),
            Localizer.Format("NameTemplatePreviewUnwrapFormat", FormatPreview(unwrap)),
            Localizer.Format("NameTemplatePreviewConflictFormat", NameTemplateText.GetDisplayName(policy), FormatPreview(conflict)));
    }

    private string ValidateTemplates(string wrapTemplate, string unwrapTemplate, string conflictTemplate)
    {
        var resolver = NameTemplateResolver.CreateDefault(Settings);
        var validations = new[]
        {
            (Localizer.Get("LabelFolderWrapTemplate"), resolver.Evaluate(wrapTemplate, NameTemplateContext.FromFile(@"C:\Sample\Book 01.zip"))),
            (Localizer.Get("LabelFolderUnwrapTemplate"), resolver.Evaluate(unwrapTemplate, NameTemplateContext.FromFolderChild(@"C:\Sample\FolderA", "Image01.jpg"))),
            (Localizer.Get("LabelConflictNameTemplate"), resolver.Evaluate(
                conflictTemplate,
                NameTemplateContext.FromNameParts("Book.zip", "Book", ".zip") with
                {
                    Index = 2,
                    IndexLabel = ConflictIndexFormatter.Format(2, GetSelectedIndexStyle())
                }))
        };

        foreach (var (label, result) in validations)
        {
            if (!result.IsReady)
            {
                return Localizer.Format("NameTemplateValidationFailedFormat", label, result.Reason ?? result.Status.ToString());
            }
        }

        return "";
    }

    private ConflictIndexStyle GetSelectedIndexStyle()
    {
        return _indexStyleCombo.SelectedItem is ComboOption<ConflictIndexStyle> selectedStyle
            ? selectedStyle.Value
            : ConflictIndexStyle.Number;
    }

    private static string NormalizeTemplate(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string FormatPreview(NameTemplateEvaluationResult result)
    {
        return result.IsReady ? WindowsFileNameSafety.MakeSafeFileName(result.Value) : result.Status.ToString();
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
}
