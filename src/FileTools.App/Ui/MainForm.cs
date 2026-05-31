using System.Windows.Forms;

namespace FileTools;

internal sealed partial class MainForm : Form
{
    private readonly string[] _initialPaths;
    private FileToolsSettings _settings = new();
    private List<AutoRelocationTemplateFile> _templates = [];
    private AutoRelocationTemplateFile? _selectedTemplate;
    private bool _loadingTemplate;

    public MainForm(IEnumerable<string>? initialPaths = null)
    {
        _initialPaths = initialPaths?.ToArray() ?? [];
        InitializeComponent();
        InitializeRuntimeBindings();
        ApplyLocalization();
    }

    private void InitializeRuntimeBindings()
    {
        Load += (_, _) => LoadState();
        DragEnter += FileDrop_DragEnter;
        DragDrop += FileDrop_DragDrop;

        _runButton.Click += (_, _) => RunSelectedTool();
        _saveSettingsButton.Click += (_, _) => SaveSettingsFromUi();
        _installContextMenuButton.Click += (_, _) => InstallContextMenu();
        _uninstallContextMenuButton.Click += (_, _) => UninstallContextMenu();
        _addFilesButton.Click += (_, _) => AddFiles();
        _addFolderButton.Click += (_, _) => AddFolder();
        _removeSelectedButton.Click += (_, _) => RemoveSelectedPaths();
        _clearButton.Click += (_, _) => ClearPaths();
        _newTemplateButton.Click += (_, _) => CreateNewTemplate();
        _deleteTemplateButton.Click += (_, _) => DeleteSelectedTemplate();
        _saveTemplateButton.Click += (_, _) => SaveTemplate();
        _templateCombo.SelectedIndexChanged += (_, _) => LoadSelectedTemplateIntoEditor();

        _pathList.DragEnter += FileDrop_DragEnter;
        _pathList.DragDrop += FileDrop_DragDrop;

        ResetOptionSources();
    }

    private void ResetOptionSources()
    {
        var selectedTool = TryGetComboValue<ToolMode>(_toolCombo);
        var selectedFolderOperation = TryGetComboValue<FolderStructureOperation>(_folderOperationCombo);
        var selectedContextMenuLayout = TryGetComboValue<ContextMenuLayout>(_contextMenuLayoutCombo);
        var selectedSource = TryGetComboValue<AutoRelocationValueSource>(_templateSourceCombo);
        var selectedTransform = TryGetComboValue<AutoRelocationValueTransform>(_templateTransformCombo);
        var selectedLanguage = TryGetComboValue<AutoRelocationLanguageProfile>(_templateLanguageCombo);

        _toolCombo.DataSource = Enum.GetValues<ToolMode>()
            .Select(mode => new ComboOption<ToolMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray();
        _folderOperationCombo.DataSource = Enum.GetValues<FolderStructureOperation>()
            .Select(operation => new ComboOption<FolderStructureOperation>(
                ToolModeText.GetDisplayName(operation),
                operation))
            .ToArray();
        _contextMenuLayoutCombo.DataSource = Enum.GetValues<ContextMenuLayout>()
            .Select(layout => new ComboOption<ContextMenuLayout>(
                ToolModeText.GetDisplayName(layout),
                layout))
            .ToArray();
        _templateSourceCombo.DataSource = Enum.GetValues<AutoRelocationValueSource>()
            .Select(value => new ComboOption<AutoRelocationValueSource>(value.ToString(), value))
            .ToArray();
        _templateTransformCombo.DataSource = Enum.GetValues<AutoRelocationValueTransform>()
            .Select(value => new ComboOption<AutoRelocationValueTransform>(value.ToString(), value))
            .ToArray();
        _templateLanguageCombo.DataSource = Enum.GetValues<AutoRelocationLanguageProfile>()
            .Select(value => new ComboOption<AutoRelocationLanguageProfile>(value.ToString(), value))
            .ToArray();

        SelectComboValue(_toolCombo, selectedTool ?? ToolMode.FileNameCorrection);
        SelectComboValue(_folderOperationCombo, selectedFolderOperation ?? FolderStructureOperation.Auto);
        SelectComboValue(_contextMenuLayoutCombo, selectedContextMenuLayout ?? ContextMenuLayout.Grouped);
        SelectComboValue(_templateSourceCombo, selectedSource ?? AutoRelocationValueSource.Title);
        SelectComboValue(_templateTransformCombo, selectedTransform ?? AutoRelocationValueTransform.InitialBucket);
        SelectComboValue(_templateLanguageCombo, selectedLanguage ?? AutoRelocationLanguageProfile.KoreanEnglish);
    }

    private void ApplyLocalization()
    {
        Text = Localizer.Get("MainFormTitle");
        _taskLabel.Text = Localizer.Get("LabelTask");
        _runButton.Text = Localizer.Get("ButtonRun");
        _saveSettingsButton.Text = Localizer.Get("ButtonSaveSettings");
        _installContextMenuButton.Text = Localizer.Get("ButtonInstallContextMenu");
        _uninstallContextMenuButton.Text = Localizer.Get("ButtonUninstallContextMenu");
        _dropTargetsLabel.Text = Localizer.Get("GroupDropTargets");
        _addFilesButton.Text = Localizer.Get("ButtonAddFiles");
        _addFolderButton.Text = Localizer.Get("ButtonAddFolder");
        _removeSelectedButton.Text = Localizer.Get("ButtonRemoveSelected");
        _clearButton.Text = Localizer.Get("ButtonClear");
        _folderGroup.Text = Localizer.Get("GroupFolderStructure");
        _contextMenuGroup.Text = Localizer.Get("GroupContextMenu");
        _contextMenuEnabledCheckBox.Text = Localizer.Get("CheckRegisterContextMenu");
        _templateGroup.Text = Localizer.Get("GroupAutoRelocationTemplates");
        _statusGroup.Text = Localizer.Get("GroupOperationResult");
        _templateLabel.Text = Localizer.Get("LabelTemplate");
        _newTemplateButton.Text = Localizer.Get("ButtonNew");
        _deleteTemplateButton.Text = Localizer.Get("ButtonDelete");
        _idLabel.Text = Localizer.Get("LabelId");
        _nameLabel.Text = Localizer.Get("LabelName");
        _descriptionLabel.Text = Localizer.Get("LabelDescription");
        _sourceLabel.Text = Localizer.Get("LabelSource");
        _transformLabel.Text = Localizer.Get("LabelTransform");
        _languageLabel.Text = Localizer.Get("LabelLanguage");
        _formatLabel.Text = Localizer.Get("LabelFormat");
        _fallbackLabel.Text = Localizer.Get("LabelFallback");
        _saveTemplateButton.Text = Localizer.Get("ButtonSaveTemplate");
    }

    private void LoadState()
    {
        _settings = SettingsStore.Load();
        SelectComboValue(_folderOperationCombo, _settings.FolderStructureOperation);
        _contextMenuEnabledCheckBox.Checked = _settings.RegisterContextMenu;
        SelectComboValue(_contextMenuLayoutCombo, _settings.ContextMenuLayout);
        RefreshTemplates(_settings.AutoRelocationTemplateId);
        AddPaths(_initialPaths);
        _statusBox.Text = Localizer.Get("InitialStatus");
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
        _statusBox.Text = Localizer.Format("SettingsSavedFormat", SettingsStore.SettingsPath);
    }

    private void RunSelectedTool()
    {
        SaveSettingsFromUi();
        var paths = _pathList.Items.Cast<string>().ToArray();
        if (paths.Length == 0)
        {
            MessageBox.Show(
                Localizer.Get("NoTargetsMessage"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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
            Title = Localizer.Get("OpenFilesDialogTitle")
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
            Description = Localizer.Get("OpenFolderDialogDescription"),
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
            DisplayName = Localizer.Get("NewTemplateDisplayName"),
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
            _statusBox.Text = Localizer.Format("TemplateSavedFormat", saved.FilePath);
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
            _statusBox.Text = Localizer.Format("ContextMenuInstalledFormat", installedPath);
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
        _statusBox.Text = Localizer.Get("ContextMenuRemoved");
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

    private static T GetComboValue<T>(ComboBox combo)
        where T : notnull
    {
        return combo.SelectedItem is ComboOption<T> option
            ? option.Value
            : throw new InvalidOperationException(Localizer.Get("InvalidSelectionValue"));
    }

    private static T? TryGetComboValue<T>(ComboBox combo)
        where T : struct
    {
        return combo.SelectedItem is ComboOption<T> option ? option.Value : null;
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
