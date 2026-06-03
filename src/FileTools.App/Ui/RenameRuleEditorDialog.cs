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
    private readonly Label _statusLabel = new();
    private readonly Button _updateButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _moveUpButton = new();
    private readonly Button _moveDownButton = new();

    private List<RenameCorrectionRule> _rules;
    private bool _loading;

    public RenameRuleEditorDialog(IEnumerable<RenameCorrectionRule> rules)
    {
        _rules = RenameRuleStore.NormalizeRules(rules)
            .Select(static rule => rule.Clone())
            .ToList();

        Text = Localizer.Get("DialogRenameRulesTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 980;
        Height = 620;
        MinimumSize = new Size(840, 520);
        MinimizeBox = false;

        BuildLayout();
        BindCombos();
        RefreshList(_rules.FirstOrDefault());
    }

    public IReadOnlyList<RenameCorrectionRule> Rules => RenameRuleStore.NormalizeRules(_rules);

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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        root.Controls.Add(BuildRuleList(), 0, 0);
        root.Controls.Add(BuildEditor(), 1, 0);
        root.Controls.Add(BuildFooter(), 0, 1);
        root.SetColumnSpan(root.GetControlFromPosition(0, 1)!, 2);
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
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

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
            WrapContents = true
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

    private Control BuildEditor()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 9; row++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, row == 7 ? 86 : 38));
        }
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

    private Control BuildFooter()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        var okButton = new Button { Text = "OK", Width = 90, Height = 30 };
        var cancelButton = new Button { Text = Localizer.Get("ButtonCancel"), DialogResult = DialogResult.Cancel, Width = 90, Height = 30 };
        okButton.Click += (_, _) => Confirm();
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return buttons;
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
}
