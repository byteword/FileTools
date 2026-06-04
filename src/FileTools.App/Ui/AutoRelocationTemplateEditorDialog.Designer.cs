using System.Drawing;
using System.Windows.Forms;

#nullable enable

namespace FileTools;

internal sealed partial class AutoRelocationTemplateEditorDialog
{
    private const int EditorMinimumControlWidth = 520;
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
    private readonly ToolTip _toolTip = new();

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
        panel.Resize += (_, _) => ResizeEditorPanel(panel);
        panel.ControlAdded += (_, _) => ResizeEditorPanel(panel);
        ResizeEditorPanel(panel);

        return panel;
    }

    private Control BuildPathRulesGroup()
    {
        var group = new GroupBox
        {
            Text = Localizer.Get("GroupPathRules"),
            Width = 720,
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

    private void WireToolTipUpdates()
    {
        _idBox.TextChanged += (_, _) => UpdateTemplateFieldToolTips();
        _nameBox.TextChanged += (_, _) => UpdateTemplateFieldToolTips();
        _descriptionBox.TextChanged += (_, _) => UpdateTemplateFieldToolTips();
        _pathFormatBox.TextChanged += (_, _) => UpdateTemplateFieldToolTips();
        _pathFallbackBox.TextChanged += (_, _) => UpdateTemplateFieldToolTips();
        _prefilterValueBox.TextChanged += (_, _) => UpdateTemplateFieldToolTips();
        _prefilterTargetBox.TextChanged += (_, _) => UpdateTemplateFieldToolTips();
    }

    private static GroupBox CreateGroup(string text, params Control[] controls)
    {
        var group = new GroupBox
        {
            Text = text,
            Width = 720,
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

        group.Resize += (_, _) =>
        {
            foreach (var control in controls)
            {
                control.Width = Math.Max(120, group.ClientSize.Width - 24);
            }
        };
        return group;
    }

    private static Control CreateTextRow(string labelText, TextBox textBox)
    {
        var panel = new Panel { Width = 680, Height = 32 };
        var label = new Label
        {
            Text = labelText,
            Left = 0,
            Top = 5,
            Width = 170,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label);
        textBox.Left = 180;
        textBox.Top = 3;
        textBox.Width = 500;
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(textBox);
        panel.Resize += (_, _) => ResizeLabeledRow(panel, label, textBox, minInputWidth: 180);
        return panel;
    }

    private static Control CreateComboRow(string labelText, ComboBox combo)
    {
        var panel = new Panel { Width = 680, Height = 32 };
        var label = new Label
        {
            Text = labelText,
            Left = 0,
            Top = 5,
            Width = 170,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label);
        combo.Left = 180;
        combo.Top = 3;
        combo.Width = 500;
        combo.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(combo);
        panel.Resize += (_, _) => ResizeLabeledRow(panel, label, combo, minInputWidth: 180);
        return panel;
    }

    private static Control CreateCompactTextRow(string labelText, TextBox textBox)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Height = 32 };
        var label = new Label
        {
            Text = labelText,
            Left = 0,
            Top = 5,
            Width = 110,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label);
        textBox.Left = 120;
        textBox.Top = 3;
        textBox.Width = 280;
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(textBox);
        panel.Resize += (_, _) => ResizeLabeledRow(panel, label, textBox, minInputWidth: 120);
        return panel;
    }

    private static Control CreateCompactComboRow(string labelText, ComboBox combo)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Height = 32 };
        var label = new Label
        {
            Text = labelText,
            Left = 0,
            Top = 5,
            Width = 110,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label);
        combo.Left = 120;
        combo.Top = 3;
        combo.Width = 280;
        combo.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(combo);
        panel.Resize += (_, _) => ResizeLabeledRow(panel, label, combo, minInputWidth: 120);
        return panel;
    }

    private static Control CreateCheckRow(CheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.Width = 700;
        checkBox.Height = 28;
        return checkBox;
    }

    private static void ResizeLabeledRow(Panel panel, Label label, Control input, int minInputWidth)
    {
        var labelWidth = Math.Clamp(panel.ClientSize.Width / 3, 100, 180);
        label.Width = labelWidth;
        input.Left = labelWidth + 10;
        input.Width = Math.Max(minInputWidth, panel.ClientSize.Width - input.Left);
    }

    private static void ResizeEditorPanel(FlowLayoutPanel panel)
    {
        var width = Math.Max(
            EditorMinimumControlWidth,
            panel.ClientSize.Width - panel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 8);
        foreach (Control control in panel.Controls)
        {
            if (control is not GroupBox)
            {
                continue;
            }

            control.Width = Math.Max(120, width - control.Margin.Horizontal);
        }
    }

    private void UpdateTemplateFieldToolTips()
    {
        _toolTip.SetToolTip(_idBox, _idBox.Text);
        _toolTip.SetToolTip(_nameBox, _nameBox.Text);
        _toolTip.SetToolTip(_descriptionBox, _descriptionBox.Text);
        _toolTip.SetToolTip(_pathFormatBox, _pathFormatBox.Text);
        _toolTip.SetToolTip(_pathFallbackBox, _pathFallbackBox.Text);
        _toolTip.SetToolTip(_prefilterValueBox, _prefilterValueBox.Text);
        _toolTip.SetToolTip(_prefilterTargetBox, _prefilterTargetBox.Text);
    }

    private static void BindEnumCombo<T>(ComboBox combo, T selectedValue)
        where T : struct, Enum
    {
        BindEnumCombo(combo, Enum.GetValues<T>(), selectedValue);
    }
}
