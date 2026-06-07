using FileTools.Correction;

namespace FileTools;

internal sealed class NameCorrectionPluginSettingsDialog : Form
{
    private readonly LoadedNameCorrectionPlugin _plugin;
    private readonly Dictionary<string, Func<string>> _valueReaders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _settings;

    public IReadOnlyDictionary<string, string> Settings => _settings;

    public NameCorrectionPluginSettingsDialog(
        LoadedNameCorrectionPlugin plugin,
        IReadOnlyDictionary<string, string> settings)
    {
        _plugin = plugin;
        _settings = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);

        Text = Localizer.Format("RenamePluginSettingsDialogTitleFormat", plugin.Descriptor.DisplayName);
        StartPosition = FormStartPosition.CenterParent;
        Width = 720;
        Height = 420;
        MinimumSize = new Size(620, 340);
        MinimizeBox = false;
        MaximizeBox = false;

        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        root.Controls.Add(CreateHeader(), 0, 0);
        root.Controls.Add(CreateSettingsPanel(), 0, 1);
        root.Controls.Add(CreateButtons(), 0, 2);
    }

    private Control CreateHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(12, 8, 12, 8)
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(203, 213, 225));
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        var title = new Label
        {
            Text = _plugin.Descriptor.DisplayName,
            AutoSize = false,
            Left = 12,
            Top = 7,
            Height = 24,
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
        };
        var detail = new Label
        {
            Text = CreateDescriptorLine(),
            AutoSize = false,
            Left = 12,
            Top = 32,
            Height = 20,
            ForeColor = Color.FromArgb(100, 116, 139),
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
        };
        panel.Controls.Add(title);
        panel.Controls.Add(detail);
        panel.Resize += (_, _) =>
        {
            title.Width = Math.Max(120, panel.ClientSize.Width - 24);
            detail.Width = title.Width;
        };
        return panel;
    }

    private string CreateDescriptorLine()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_plugin.Descriptor.License))
        {
            parts.Add(_plugin.Descriptor.License);
        }

        if (_plugin.Descriptor.SupportedLanguages.Count > 0)
        {
            parts.Add(string.Join(", ", _plugin.Descriptor.SupportedLanguages));
        }

        if (!string.IsNullOrWhiteSpace(_plugin.Descriptor.Description))
        {
            parts.Add(_plugin.Descriptor.Description);
        }

        return string.Join(" - ", parts);
    }

    private Control CreateSettingsPanel()
    {
        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 12, 0, 8)
        };
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        scrollHost.Controls.Add(stack);

        foreach (var definition in _plugin.SettingDefinitions)
        {
            stack.Controls.Add(CreateSettingRow(definition));
        }

        if (_plugin.SettingDefinitions.Count == 0)
        {
            stack.Controls.Add(new Label
            {
                Text = Localizer.Get("RenamePluginNoSettings"),
                AutoSize = false,
                Height = 36,
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleLeft
            });
        }

        scrollHost.Resize += (_, _) =>
        {
            stack.Width = Math.Max(420, scrollHost.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
            foreach (var control in stack.Controls.OfType<Control>())
            {
                control.Width = stack.Width;
            }
        };

        return scrollHost;
    }

    private Control CreateSettingRow(NameCorrectionSettingDefinition definition)
    {
        return definition.Kind switch
        {
            NameCorrectionSettingKind.Boolean => CreateBooleanRow(definition),
            NameCorrectionSettingKind.Select => CreateSelectRow(definition),
            NameCorrectionSettingKind.FilePath => CreateFilePathRow(definition),
            _ => CreateTextRow(definition)
        };
    }

    private Control CreateBooleanRow(NameCorrectionSettingDefinition definition)
    {
        var checkBox = new CheckBox
        {
            Text = definition.DisplayName,
            Checked = ParseBool(GetValue(definition), ParseBool(definition.DefaultValue, false)),
            Height = 30,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 8)
        };
        _valueReaders[definition.Key] = () => checkBox.Checked ? "true" : "false";
        return checkBox;
    }

    private Control CreateSelectRow(NameCorrectionSettingDefinition definition)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        combo.DataSource = definition.Options
            .Select(static option => new ComboOption<string>(option.DisplayName, option.Value))
            .ToArray();
        SelectComboValue(combo, GetValue(definition));
        _valueReaders[definition.Key] = () => combo.SelectedItem is ComboOption<string> option
            ? option.Value
            : definition.DefaultValue;
        return CreateInputRow(definition, combo);
    }

    private Control CreateFilePathRow(NameCorrectionSettingDefinition definition)
    {
        var textBox = new TextBox { Text = GetValue(definition) };
        var button = new Button
        {
            Text = Localizer.Get("ButtonBrowse"),
            Width = 94,
            Height = 26,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        button.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                CheckFileExists = false,
                FileName = textBox.Text
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textBox.Text = dialog.FileName;
            }
        };

        _valueReaders[definition.Key] = () => textBox.Text.Trim();
        return CreateInputRow(definition, textBox, button);
    }

    private Control CreateTextRow(NameCorrectionSettingDefinition definition)
    {
        var textBox = new TextBox { Text = GetValue(definition) };
        _valueReaders[definition.Key] = () => textBox.Text.Trim();
        return CreateInputRow(definition, textBox);
    }

    private Control CreateInputRow(
        NameCorrectionSettingDefinition definition,
        Control input,
        Control? trailing = null)
    {
        var panel = new Panel
        {
            Height = string.IsNullOrWhiteSpace(definition.Description) ? 38 : 64,
            Margin = new Padding(0, 0, 0, 8)
        };
        var label = new Label
        {
            Text = definition.DisplayName,
            Left = 0,
            Top = 3,
            Width = 190,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft
        };
        input.Left = 204;
        input.Top = 1;
        input.Height = 26;
        input.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(label);
        panel.Controls.Add(input);

        if (trailing is not null)
        {
            panel.Controls.Add(trailing);
        }

        Label? help = null;
        if (!string.IsNullOrWhiteSpace(definition.Description))
        {
            help = new Label
            {
                Text = definition.Description,
                AutoSize = false,
                Left = 204,
                Top = 31,
                Height = 32,
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(help);
        }

        void ResizeRow()
        {
            var labelWidth = Math.Clamp(panel.ClientSize.Width / 3, 150, 220);
            label.Width = labelWidth;
            input.Left = labelWidth + 14;
            var rightReserved = trailing is null ? 0 : trailing.Width + 8;
            input.Width = Math.Max(180, panel.ClientSize.Width - input.Left - rightReserved);
            if (trailing is not null)
            {
                trailing.Left = input.Right + 8;
                trailing.Top = 0;
            }

            if (help is not null)
            {
                help.Left = input.Left;
                help.Width = Math.Max(180, panel.ClientSize.Width - help.Left);
            }
        }

        panel.Resize += (_, _) => ResizeRow();
        ResizeRow();
        return panel;
    }

    private Control CreateButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        var ok = new Button { Text = "OK", Width = 94, Height = 30 };
        var cancel = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            Width = 94,
            Height = 30,
            DialogResult = DialogResult.Cancel
        };
        ok.Click += (_, _) => SaveAndClose();
        panel.Controls.Add(cancel);
        panel.Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;
        return panel;
    }

    private void SaveAndClose()
    {
        foreach (var definition in _plugin.SettingDefinitions)
        {
            var value = _valueReaders.TryGetValue(definition.Key, out var reader)
                ? reader()
                : definition.DefaultValue;
            if (definition.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                MessageBox.Show(
                    Localizer.Format("RenamePluginRequiredSettingFormat", definition.DisplayName),
                    FileToolsEnvironment.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _settings[definition.Key] = value;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private string GetValue(NameCorrectionSettingDefinition definition)
    {
        return _settings.TryGetValue(definition.Key, out var value) ? value : definition.DefaultValue;
    }

    private static bool ParseBool(string value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static void SelectComboValue(ComboBox combo, string value)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboOption<string> option &&
                string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
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
