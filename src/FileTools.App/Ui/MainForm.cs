using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class MainForm : Form
{
    private readonly ComboBox _toolCombo = new();
    private readonly ComboBox _folderOperationCombo = new();
    private readonly CheckBox _contextMenuEnabledCheckBox = new();
    private readonly ComboBox _contextMenuLayoutCombo = new();
    private readonly ListBox _pathList = new();
    private readonly TextBox _statusBox = new();
    private readonly ComboBox _templateCombo = new();
    private readonly TextBox _templateIdBox = new();
    private readonly TextBox _templateNameBox = new();
    private readonly TextBox _templateDescriptionBox = new();
    private readonly ComboBox _templateSourceCombo = new();
    private readonly ComboBox _templateTransformCombo = new();
    private readonly ComboBox _templateLanguageCombo = new();
    private readonly TextBox _templateFormatBox = new();
    private readonly TextBox _templateFallbackBox = new();
    private readonly string[] _initialPaths;

    private FileToolsSettings _settings = new();
    private List<AutoRelocationTemplateFile> _templates = [];
    private AutoRelocationTemplateFile? _selectedTemplate;
    private bool _loadingTemplate;

    public MainForm(IEnumerable<string>? initialPaths = null)
    {
        _initialPaths = initialPaths?.ToArray() ?? [];
        Text = "FileTools 설정 및 작업";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 980;
        Height = 700;
        MinimumSize = new Size(860, 580);
        AllowDrop = true;

        BuildLayout();
        Load += (_, _) => LoadState();
        DragEnter += FileDrop_DragEnter;
        DragDrop += FileDrop_DragDrop;
    }

    private void BuildLayout()
    {
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8, 8, 8, 4),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        _toolCombo.Width = 220;
        _toolCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _toolCombo.DataSource = Enum.GetValues<ToolMode>()
            .Select(mode => new ComboOption<ToolMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray();

        var runButton = CreateButton("실행", RunSelectedTool);
        var saveButton = CreateButton("설정 저장", SaveSettingsFromUi);
        var installButton = CreateButton("ContextMenu 설치", InstallContextMenu);
        var uninstallButton = CreateButton("ContextMenu 제거", UninstallContextMenu);

        topPanel.Controls.Add(CreateInlineLabel("작업"));
        topPanel.Controls.Add(_toolCombo);
        topPanel.Controls.Add(runButton);
        topPanel.Controls.Add(saveButton);
        topPanel.Controls.Add(installButton);
        topPanel.Controls.Add(uninstallButton);
        Controls.Add(topPanel);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 410,
            FixedPanel = FixedPanel.Panel1
        };
        Controls.Add(split);

        BuildPathPanel(split.Panel1);
        BuildSettingsPanel(split.Panel2);
    }

    private void BuildPathPanel(Control parent)
    {
        var label = new Label
        {
            Text = "드래그앤드롭 대상",
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(8, 7, 0, 0)
        };
        parent.Controls.Add(label);

        _pathList.Dock = DockStyle.Fill;
        _pathList.HorizontalScrollbar = true;
        _pathList.AllowDrop = true;
        _pathList.SelectionMode = SelectionMode.MultiExtended;
        _pathList.DragEnter += FileDrop_DragEnter;
        _pathList.DragDrop += FileDrop_DragDrop;
        parent.Controls.Add(_pathList);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(8, 6, 8, 6),
            WrapContents = false
        };
        bottom.Controls.Add(CreateButton("파일 추가", AddFiles));
        bottom.Controls.Add(CreateButton("폴더 추가", AddFolder));
        bottom.Controls.Add(CreateButton("선택 제거", RemoveSelectedPaths));
        bottom.Controls.Add(CreateButton("비우기", ClearPaths));
        parent.Controls.Add(bottom);
    }

    private void BuildSettingsPanel(Control parent)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(10)
        };
        parent.Controls.Add(panel);

        var folderGroup = new GroupBox
        {
            Text = "폴더 wrapping / unwrapping",
            Left = 10,
            Top = 8,
            Width = 500,
            Height = 78,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _folderOperationCombo.Left = 16;
        _folderOperationCombo.Top = 30;
        _folderOperationCombo.Width = 450;
        _folderOperationCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _folderOperationCombo.DataSource = Enum.GetValues<FolderStructureOperation>()
            .Select(operation => new ComboOption<FolderStructureOperation>(
                ToolModeText.GetDisplayName(operation),
                operation))
            .ToArray();
        folderGroup.Controls.Add(_folderOperationCombo);
        panel.Controls.Add(folderGroup);

        var contextMenuGroup = new GroupBox
        {
            Text = "ContextMenu",
            Left = 10,
            Top = 96,
            Width = 500,
            Height = 92,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _contextMenuEnabledCheckBox.Text = "Explorer ContextMenu 등록";
        _contextMenuEnabledCheckBox.Left = 16;
        _contextMenuEnabledCheckBox.Top = 28;
        _contextMenuEnabledCheckBox.Width = 220;
        contextMenuGroup.Controls.Add(_contextMenuEnabledCheckBox);

        _contextMenuLayoutCombo.Left = 16;
        _contextMenuLayoutCombo.Top = 56;
        _contextMenuLayoutCombo.Width = 450;
        _contextMenuLayoutCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _contextMenuLayoutCombo.DataSource = Enum.GetValues<ContextMenuLayout>()
            .Select(layout => new ComboOption<ContextMenuLayout>(
                ToolModeText.GetDisplayName(layout),
                layout))
            .ToArray();
        contextMenuGroup.Controls.Add(_contextMenuLayoutCombo);
        panel.Controls.Add(contextMenuGroup);

        var templateGroup = new GroupBox
        {
            Text = "자동 재배치 템플릿",
            Left = 10,
            Top = 198,
            Width = 500,
            Height = 300,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        panel.Controls.Add(templateGroup);

        AddTemplateControls(templateGroup);

        var statusGroup = new GroupBox
        {
            Text = "작업 결과",
            Left = 10,
            Top = 508,
            Width = 500,
            Height = 210,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        _statusBox.Multiline = true;
        _statusBox.ScrollBars = ScrollBars.Both;
        _statusBox.WordWrap = false;
        _statusBox.ReadOnly = true;
        _statusBox.Left = 12;
        _statusBox.Top = 24;
        _statusBox.Width = 468;
        _statusBox.Height = 170;
        _statusBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        statusGroup.Controls.Add(_statusBox);
        panel.Controls.Add(statusGroup);
    }

    private void AddTemplateControls(Control parent)
    {
        parent.Controls.Add(CreateLabel("템플릿", 16, 28));
        _templateCombo.Left = 96;
        _templateCombo.Top = 24;
        _templateCombo.Width = 250;
        _templateCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateCombo.SelectedIndexChanged += (_, _) => LoadSelectedTemplateIntoEditor();
        parent.Controls.Add(_templateCombo);

        parent.Controls.Add(CreateButton("새로", CreateNewTemplate, 354, 23, 56));
        parent.Controls.Add(CreateButton("삭제", DeleteSelectedTemplate, 416, 23, 56));

        parent.Controls.Add(CreateLabel("ID", 16, 62));
        ConfigureTextBox(_templateIdBox, 96, 58, 376, parent);

        parent.Controls.Add(CreateLabel("이름", 16, 94));
        ConfigureTextBox(_templateNameBox, 96, 90, 376, parent);

        parent.Controls.Add(CreateLabel("설명", 16, 126));
        ConfigureTextBox(_templateDescriptionBox, 96, 122, 376, parent);

        parent.Controls.Add(CreateLabel("Source", 16, 158));
        ConfigureCombo(_templateSourceCombo, 96, 154, 130, Enum.GetValues<AutoRelocationValueSource>()
            .Select(value => new ComboOption<AutoRelocationValueSource>(value.ToString(), value))
            .ToArray(), parent);

        parent.Controls.Add(CreateLabel("Transform", 238, 158));
        ConfigureCombo(_templateTransformCombo, 318, 154, 154, Enum.GetValues<AutoRelocationValueTransform>()
            .Select(value => new ComboOption<AutoRelocationValueTransform>(value.ToString(), value))
            .ToArray(), parent);

        parent.Controls.Add(CreateLabel("Language", 16, 190));
        ConfigureCombo(_templateLanguageCombo, 96, 186, 130, Enum.GetValues<AutoRelocationLanguageProfile>()
            .Select(value => new ComboOption<AutoRelocationLanguageProfile>(value.ToString(), value))
            .ToArray(), parent);

        parent.Controls.Add(CreateLabel("Format", 238, 190));
        ConfigureTextBox(_templateFormatBox, 318, 186, 154, parent);

        parent.Controls.Add(CreateLabel("Fallback", 16, 222));
        ConfigureTextBox(_templateFallbackBox, 96, 218, 130, parent);

        parent.Controls.Add(CreateButton("템플릿 저장", SaveTemplate, 318, 246, 154));
    }

    private void LoadState()
    {
        _settings = SettingsStore.Load();
        SelectComboValue(_folderOperationCombo, _settings.FolderStructureOperation);
        _contextMenuEnabledCheckBox.Checked = _settings.RegisterContextMenu;
        SelectComboValue(_contextMenuLayoutCombo, _settings.ContextMenuLayout);
        RefreshTemplates(_settings.AutoRelocationTemplateId);
        AddPaths(_initialPaths);
        _statusBox.Text = "파일 또는 폴더를 왼쪽 목록으로 드래그앤드롭한 뒤 작업을 실행합니다.";
    }

    private void RefreshTemplates(string? preferredId)
    {
        _templates = AutoRelocationTemplateStore.LoadTemplates().ToList();
        _templateCombo.DataSource = _templates
            .Select(template => new ComboOption<string>(
                $"{template.Document.DisplayName} ({template.Document.Id})",
                template.Document.Id))
            .ToArray();

        if (_templates.Count == 0)
        {
            _selectedTemplate = null;
            return;
        }

        var target = _templates.FirstOrDefault(template => string.Equals(
                template.Document.Id,
                AutoRelocationTemplateStore.NormalizeTemplateId(preferredId),
                StringComparison.OrdinalIgnoreCase))
            ?? _templates.FirstOrDefault(template => string.Equals(
                template.Document.Id,
                AutoRelocationTemplateDefaults.DefaultTemplateId,
                StringComparison.OrdinalIgnoreCase))
            ?? _templates[0];

        SelectComboValue(_templateCombo, target.Document.Id);
        LoadTemplateIntoEditor(target);
    }

    private void LoadSelectedTemplateIntoEditor()
    {
        if (_loadingTemplate || _templateCombo.SelectedItem is not ComboOption<string> option)
        {
            return;
        }

        var template = _templates.FirstOrDefault(item => string.Equals(
            item.Document.Id,
            option.Value,
            StringComparison.OrdinalIgnoreCase));
        if (template is not null)
        {
            LoadTemplateIntoEditor(template);
        }
    }

    private void LoadTemplateIntoEditor(AutoRelocationTemplateFile templateFile)
    {
        _loadingTemplate = true;
        try
        {
            _selectedTemplate = templateFile;
            var document = templateFile.Document;
            _templateIdBox.Text = document.Id;
            _templateNameBox.Text = document.DisplayName;
            _templateDescriptionBox.Text = document.Description ?? "";

            var rule = document.PathRules.FirstOrDefault() ?? new AutoRelocationPathRule();
            SelectComboValue(_templateSourceCombo, rule.Source);
            SelectComboValue(_templateTransformCombo, rule.Transform);
            SelectComboValue(_templateLanguageCombo, rule.Language);
            _templateFormatBox.Text = rule.Format;
            _templateFallbackBox.Text = rule.FallbackFolderName;
        }
        finally
        {
            _loadingTemplate = false;
        }
    }

    private void SaveSettingsFromUi()
    {
        _settings.FolderStructureOperation = GetComboValue<FolderStructureOperation>(_folderOperationCombo);
        _settings.RegisterContextMenu = _contextMenuEnabledCheckBox.Checked;
        _settings.ContextMenuLayout = GetComboValue<ContextMenuLayout>(_contextMenuLayoutCombo);
        _settings.AutoRelocationTemplateId = _templateCombo.SelectedItem is ComboOption<string> template
            ? template.Value
            : AutoRelocationTemplateDefaults.DefaultTemplateId;
        SettingsStore.Save(_settings);
        _statusBox.Text = "설정을 저장했습니다.\r\n" + SettingsStore.SettingsPath;
    }

    private void RunSelectedTool()
    {
        SaveSettingsFromUi();
        var paths = _pathList.Items.Cast<string>().ToArray();
        if (paths.Length == 0)
        {
            MessageBox.Show("처리할 파일 또는 폴더를 추가하세요.", FileToolsEnvironment.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var mode = GetComboValue<ToolMode>(_toolCombo);
        var result = new FileToolRunner(_settings).Run(mode, paths);
        var message = result.ToUserMessage(ToolModeText.GetDisplayName(mode));
        _statusBox.Text = message;
        MessageBox.Show(
            message,
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            result.HasErrors ? MessageBoxIcon.Error : MessageBoxIcon.Information);
    }

    private void AddFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Title = "처리할 파일 선택"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddPaths(dialog.FileNames);
        }
    }

    private void AddFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "처리할 폴더 선택",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddPaths([dialog.SelectedPath]);
        }
    }

    private void RemoveSelectedPaths()
    {
        var selected = _pathList.SelectedItems.Cast<object>().ToArray();
        foreach (var item in selected)
        {
            _pathList.Items.Remove(item);
        }
    }

    private void ClearPaths()
    {
        _pathList.Items.Clear();
    }

    private void CreateNewTemplate()
    {
        var id = "Template " + DateTime.Now.ToString("yyyyMMddHHmmss");
        var defaultDocument = AutoRelocationTemplateDefaults.CreateDefaultTemplate();
        var document = new AutoRelocationTemplateDocument
        {
            Id = id,
            DisplayName = "새 템플릿",
            Description = "",
            Prefilters = defaultDocument.Prefilters,
            PathRules = defaultDocument.PathRules
        };

        _selectedTemplate = new AutoRelocationTemplateFile(document, "");
        LoadTemplateIntoEditor(_selectedTemplate);
    }

    private void SaveTemplate()
    {
        try
        {
            var id = AutoRelocationTemplateStore.NormalizeTemplateId(_templateIdBox.Text);
            var rule = new AutoRelocationPathRule
            {
                Source = GetComboValue<AutoRelocationValueSource>(_templateSourceCombo),
                Transform = GetComboValue<AutoRelocationValueTransform>(_templateTransformCombo),
                Language = GetComboValue<AutoRelocationLanguageProfile>(_templateLanguageCombo),
                Format = string.IsNullOrWhiteSpace(_templateFormatBox.Text) ? "{value}" : _templateFormatBox.Text.Trim(),
                FallbackFolderName = string.IsNullOrWhiteSpace(_templateFallbackBox.Text) ? "[ETC]" : _templateFallbackBox.Text.Trim()
            };

            var document = new AutoRelocationTemplateDocument
            {
                Id = id,
                DisplayName = string.IsNullOrWhiteSpace(_templateNameBox.Text) ? id : _templateNameBox.Text.Trim(),
                Description = string.IsNullOrWhiteSpace(_templateDescriptionBox.Text) ? null : _templateDescriptionBox.Text.Trim(),
                Prefilters = _selectedTemplate?.Document.Prefilters ?? [],
                PathRules = [rule]
            };

            var saved = AutoRelocationTemplateStore.SaveTemplate(document);
            _settings.AutoRelocationTemplateId = saved.Document.Id;
            SettingsStore.Save(_settings);
            RefreshTemplates(saved.Document.Id);
            _statusBox.Text = "템플릿을 저장했습니다.\r\n" + saved.FilePath;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteSelectedTemplate()
    {
        if (_selectedTemplate is null)
        {
            return;
        }

        if (string.Equals(_selectedTemplate.Document.Id, AutoRelocationTemplateDefaults.DefaultTemplateId, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Default 템플릿은 삭제할 수 없습니다.", FileToolsEnvironment.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show("선택한 템플릿을 삭제할까요?", FileToolsEnvironment.AppName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        AutoRelocationTemplateStore.DeleteTemplate(_selectedTemplate.Document.Id);
        _settings.AutoRelocationTemplateId = AutoRelocationTemplateDefaults.DefaultTemplateId;
        SettingsStore.Save(_settings);
        RefreshTemplates(_settings.AutoRelocationTemplateId);
    }

    private void InstallContextMenu()
    {
        try
        {
            var exe = Environment.ProcessPath ?? "";
            SaveSettingsFromUi();
            var installedPath = ContextMenuRegistrar.Install(exe, _settings);
            _statusBox.Text = "ContextMenu를 설치했습니다.\r\n" + installedPath;
            MessageBox.Show(_statusBox.Text, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UninstallContextMenu()
    {
        ContextMenuRegistrar.Uninstall();
        _statusBox.Text = "ContextMenu를 제거했습니다.";
        MessageBox.Show(_statusBox.Text, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void FileDrop_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void FileDrop_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddPaths(paths);
        }
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        var existing = _pathList.Items.Cast<string>().ToHashSet(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var path in paths.Where(static path => File.Exists(path) || Directory.Exists(path)))
        {
            var fullPath = Path.GetFullPath(path);
            if (existing.Add(fullPath))
            {
                _pathList.Items.Add(fullPath);
            }
        }
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 116,
            Height = 28
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button CreateButton(string text, Action action, int left, int top, int width)
    {
        var button = CreateButton(text, action);
        button.Left = left;
        button.Top = top;
        button.Width = width;
        return button;
    }

    private static Label CreateInlineLabel(string text)
    {
        return new Label
        {
            Text = text,
            Width = 36,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateLabel(string text, int left, int top)
    {
        return new Label
        {
            Text = text,
            Left = left,
            Top = top,
            Width = 78,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static void ConfigureTextBox(TextBox textBox, int left, int top, int width, Control parent)
    {
        textBox.Left = left;
        textBox.Top = top;
        textBox.Width = width;
        parent.Controls.Add(textBox);
    }

    private static void ConfigureCombo<T>(ComboBox combo, int left, int top, int width, ComboOption<T>[] options, Control parent)
        where T : notnull
    {
        combo.Left = left;
        combo.Top = top;
        combo.Width = width;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DataSource = options;
        parent.Controls.Add(combo);
    }

    private static T GetComboValue<T>(ComboBox combo)
        where T : notnull
    {
        return combo.SelectedItem is ComboOption<T> option
            ? option.Value
            : throw new InvalidOperationException("선택값을 읽을 수 없습니다.");
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
    }

    private sealed record ComboOption<T>(string Text, T Value)
    {
        public override string ToString()
        {
            return Text;
        }
    }
}
