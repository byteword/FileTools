using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class SettingsForm : Form
{
    private readonly CheckBox _registerContextMenuCheckBox = new();
    private readonly CheckBox _contextMenuOpenCheckBox = new();
    private readonly CheckBox _contextMenuRenameCheckBox = new();
    private readonly CheckBox _contextMenuFolderWrapCheckBox = new();
    private readonly CheckBox _contextMenuFolderUnwrapSameNameCheckBox = new();
    private readonly CheckBox _contextMenuFolderUnwrapSingleFileCheckBox = new();
    private readonly CheckBox _contextMenuFolderMoveInnerFilesCheckBox = new();
    private readonly CheckBox _contextMenuRelocationCurrentCheckBox = new();
    private readonly CheckBox _contextMenuRelocationChooseTargetCheckBox = new();
    private readonly CheckBox _contextMenuArchiveMergeGroupByArchiveNameCheckBox = new();
    private readonly CheckBox _contextMenuArchiveMergePreserveInternalPathsCheckBox = new();
    private readonly ComboBox _contextMenuLayoutCombo = new();
    private readonly ComboBox _defaultFolderOperationCombo = new();
    private readonly ComboBox _folderMismatchCombo = new();
    private readonly ComboBox _defaultTemplateCombo = new();
    private readonly ComboBox _renameReviewModeCombo = new();
    private readonly ComboBox _archiveMergeLayoutCombo = new();
    private readonly ComboBox _archiveMergeCollisionCombo = new();
    private readonly ComboBox _archiveMergeDuplicateCombo = new();
    private readonly ComboBox _archiveMergeFailureCombo = new();
    private readonly ComboBox _archiveMergeOutputNameCombo = new();
    private readonly ComboBox _archiveMergeCompressionCombo = new();
    private readonly CheckBox _renameDictionaryCheckBox = new();
    private readonly CheckBox _archiveMergeDeleteOriginalsCheckBox = new();
    private readonly Label _statusTitleLabel = new();
    private readonly Label _statusDetailLabel = new();
    private readonly Label _statusHintLabel = new();
    private readonly FlowLayoutPanel _settingsStack = new();
    private readonly List<CollapsibleSettingsGroup> _groups = [];
    private CollapsibleSettingsGroup? _contextMenuGroup;
    private CollapsibleSettingsGroup? _renameGroup;
    private CollapsibleSettingsGroup? _folderGroup;
    private CollapsibleSettingsGroup? _relocationGroup;
    private CollapsibleSettingsGroup? _archiveMergeGroup;

    public SettingsForm(FileToolsSettings settings)
    {
        Settings = settings.Clone();
        Text = Localizer.Get("SettingsFormTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 720);
        MinimumSize = new Size(820, 640);

        BuildLayout();
        LoadSettings();
        WireUiStateUpdates();
        UpdateUiState();
    }

    public FileToolsSettings Settings { get; private set; }

    private void LoadSettings()
    {
        _registerContextMenuCheckBox.Checked = Settings.RegisterContextMenu;
        _contextMenuOpenCheckBox.Checked = Settings.ContextMenuOpenApp;
        _contextMenuRenameCheckBox.Checked = Settings.ContextMenuFileNameCorrection;
        _contextMenuFolderWrapCheckBox.Checked = Settings.ContextMenuFolderWrapFiles;
        _contextMenuFolderUnwrapSameNameCheckBox.Checked = Settings.ContextMenuFolderUnwrapSameNameSingleFile;
        _contextMenuFolderUnwrapSingleFileCheckBox.Checked = Settings.ContextMenuFolderUnwrapSingleFile;
        _contextMenuFolderMoveInnerFilesCheckBox.Checked = Settings.ContextMenuFolderMoveInnerFilesUp;
        _contextMenuRelocationCurrentCheckBox.Checked = Settings.ContextMenuAutoRelocationCurrentFolder;
        _contextMenuRelocationChooseTargetCheckBox.Checked = Settings.ContextMenuAutoRelocationChooseTarget;
        _contextMenuArchiveMergeGroupByArchiveNameCheckBox.Checked = Settings.ContextMenuArchiveMergeGroupByArchiveName;
        _contextMenuArchiveMergePreserveInternalPathsCheckBox.Checked = Settings.ContextMenuArchiveMergePreserveInternalPaths;
        _renameDictionaryCheckBox.Checked = Settings.RenameUseDictionary;
        _archiveMergeDeleteOriginalsCheckBox.Checked = Settings.ArchiveMergeDeleteOriginals;

        ConfigureCombo(_renameReviewModeCombo, Enum.GetValues<RenameReviewMode>()
            .Select(mode => new ComboOption<RenameReviewMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray());
        SelectComboValue(_renameReviewModeCombo, Settings.RenameReviewMode);

        ConfigureCombo(_contextMenuLayoutCombo, Enum.GetValues<ContextMenuLayout>()
            .Select(layout => new ComboOption<ContextMenuLayout>(ToolModeText.GetDisplayName(layout), layout))
            .ToArray());
        SelectComboValue(_contextMenuLayoutCombo, Settings.ContextMenuLayout);

        ConfigureCombo(_defaultFolderOperationCombo, Enum.GetValues<FolderStructureOperation>()
            .Select(operation => new ComboOption<FolderStructureOperation>(ToolModeText.GetDisplayName(operation), operation))
            .ToArray());
        SelectComboValue(_defaultFolderOperationCombo, Settings.FolderStructureOperation);

        ConfigureCombo(_folderMismatchCombo, Enum.GetValues<FolderUnwrapNameMismatchMode>()
            .Select(mode => new ComboOption<FolderUnwrapNameMismatchMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray());
        SelectComboValue(_folderMismatchCombo, Settings.FolderUnwrapNameMismatchMode);

        ConfigureCombo(_archiveMergeLayoutCombo, Enum.GetValues<ArchiveMergeLayout>()
            .Select(value => new ComboOption<ArchiveMergeLayout>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_archiveMergeLayoutCombo, Settings.ArchiveMergeLayout);

        ConfigureCombo(_archiveMergeCollisionCombo, Enum.GetValues<ArchiveMergeCollisionPolicy>()
            .Select(value => new ComboOption<ArchiveMergeCollisionPolicy>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_archiveMergeCollisionCombo, Settings.ArchiveMergeCollisionPolicy);

        ConfigureCombo(_archiveMergeDuplicateCombo, Enum.GetValues<ArchiveMergeDuplicatePolicy>()
            .Select(value => new ComboOption<ArchiveMergeDuplicatePolicy>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_archiveMergeDuplicateCombo, Settings.ArchiveMergeDuplicatePolicy);

        ConfigureCombo(_archiveMergeFailureCombo, Enum.GetValues<ArchiveMergeFailurePolicy>()
            .Select(value => new ComboOption<ArchiveMergeFailurePolicy>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_archiveMergeFailureCombo, Settings.ArchiveMergeFailurePolicy);

        ConfigureCombo(_archiveMergeOutputNameCombo, Enum.GetValues<ArchiveMergeOutputNamePolicy>()
            .Select(value => new ComboOption<ArchiveMergeOutputNamePolicy>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_archiveMergeOutputNameCombo, Settings.ArchiveMergeOutputNamePolicy);

        ConfigureCombo(_archiveMergeCompressionCombo, Enum.GetValues<ArchiveMergeCompressionLevel>()
            .Select(value => new ComboOption<ArchiveMergeCompressionLevel>(ArchiveMergeText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_archiveMergeCompressionCombo, Settings.ArchiveMergeCompressionLevel);

        RefreshTemplateCombo(Settings.AutoRelocationTemplateId);
    }

    private static void ConfigureCombo<T>(ComboBox combo, ComboOption<T>[] options)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DataSource = options;
    }

    private void WireUiStateUpdates()
    {
        foreach (var checkBox in new[]
        {
            _registerContextMenuCheckBox,
            _contextMenuOpenCheckBox,
            _contextMenuRenameCheckBox,
            _contextMenuFolderWrapCheckBox,
            _contextMenuFolderUnwrapSameNameCheckBox,
            _contextMenuFolderUnwrapSingleFileCheckBox,
            _contextMenuFolderMoveInnerFilesCheckBox,
            _contextMenuRelocationCurrentCheckBox,
            _contextMenuRelocationChooseTargetCheckBox,
            _contextMenuArchiveMergeGroupByArchiveNameCheckBox,
            _contextMenuArchiveMergePreserveInternalPathsCheckBox,
            _renameDictionaryCheckBox,
            _archiveMergeDeleteOriginalsCheckBox
        })
        {
            checkBox.CheckedChanged += (_, _) => UpdateUiState();
        }

        foreach (var combo in new[]
        {
            _renameReviewModeCombo,
            _contextMenuLayoutCombo,
            _defaultFolderOperationCombo,
            _folderMismatchCombo,
            _defaultTemplateCombo,
            _archiveMergeLayoutCombo,
            _archiveMergeCollisionCombo,
            _archiveMergeDuplicateCombo,
            _archiveMergeFailureCombo,
            _archiveMergeOutputNameCombo,
            _archiveMergeCompressionCombo
        })
        {
            combo.SelectedIndexChanged += (_, _) => UpdateUiState();
        }
    }

    private void UpdateUiState()
    {
        var contextStatus = _registerContextMenuCheckBox.Checked
            ? Localizer.Get("SettingsContextMenuRegistered")
            : Localizer.Get("SettingsContextMenuNotRegistered");
        var commandCount = GetEnabledContextMenuCommandCount();
        _statusTitleLabel.Text = Localizer.Get("SettingsStatusTitle");
        _statusDetailLabel.Text = _registerContextMenuCheckBox.Checked
            ? Localizer.Format("SettingsStatusRegisteredFormat", commandCount)
            : Localizer.Format("SettingsStatusNotRegisteredFormat", commandCount);
        _statusHintLabel.Text = Localizer.Format("SettingsStatusApplyHintFormat", contextStatus);

        foreach (var group in _groups)
        {
            group.RefreshSummary();
        }
    }

    private string GetContextMenuSummary()
    {
        var commandCount = GetEnabledContextMenuCommandCount();
        return _registerContextMenuCheckBox.Checked
            ? Localizer.Format("SettingsSummaryRegisteredEnabledCountFormat", commandCount)
            : Localizer.Format("SettingsSummaryNotRegisteredEnabledCountFormat", commandCount);
    }

    private string GetRenameSummary()
    {
        var dictionaryState = _renameDictionaryCheckBox.Checked
            ? Localizer.Get("SettingsSummaryDictionaryOn")
            : Localizer.Get("SettingsSummaryDictionaryOff");
        return Localizer.Format("SettingsSummaryPairFormat", GetComboText(_renameReviewModeCombo), dictionaryState);
    }

    private string GetFolderSummary()
    {
        return Localizer.Format(
            "SettingsSummaryPairFormat",
            GetComboText(_defaultFolderOperationCombo),
            GetComboText(_folderMismatchCombo));
    }

    private string GetRelocationSummary()
    {
        return TrimSummary(GetComboText(_defaultTemplateCombo), 64);
    }

    private string GetArchiveMergeSummary()
    {
        return Localizer.Format(
            "SettingsSummaryPairFormat",
            GetComboText(_archiveMergeLayoutCombo),
            GetComboText(_archiveMergeCompressionCombo));
    }

    private int GetEnabledContextMenuCommandCount()
    {
        return new[]
        {
            _contextMenuOpenCheckBox,
            _contextMenuRenameCheckBox,
            _contextMenuFolderWrapCheckBox,
            _contextMenuFolderUnwrapSameNameCheckBox,
            _contextMenuFolderUnwrapSingleFileCheckBox,
            _contextMenuFolderMoveInnerFilesCheckBox,
            _contextMenuRelocationCurrentCheckBox,
            _contextMenuRelocationChooseTargetCheckBox,
            _contextMenuArchiveMergeGroupByArchiveNameCheckBox,
            _contextMenuArchiveMergePreserveInternalPathsCheckBox
        }.Count(static checkBox => checkBox.Checked);
    }

    private void SaveAndClose()
    {
        try
        {
            SaveSettingsFromUi();
            SettingsStore.Save(Settings);
            SyncContextMenuRegistration();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSettingsFromUi()
    {
        Settings.RegisterContextMenu = _registerContextMenuCheckBox.Checked;
        Settings.ContextMenuOpenApp = _contextMenuOpenCheckBox.Checked;
        Settings.ContextMenuFileNameCorrection = _contextMenuRenameCheckBox.Checked;
        Settings.ContextMenuFolderStructure = true;
        Settings.ContextMenuFolderWrapFiles = _contextMenuFolderWrapCheckBox.Checked;
        Settings.ContextMenuFolderUnwrapSameNameSingleFile = _contextMenuFolderUnwrapSameNameCheckBox.Checked;
        Settings.ContextMenuFolderUnwrapSingleFile = _contextMenuFolderUnwrapSingleFileCheckBox.Checked;
        Settings.ContextMenuFolderMoveInnerFilesUp = _contextMenuFolderMoveInnerFilesCheckBox.Checked;
        Settings.ContextMenuAutoRelocation = true;
        Settings.ContextMenuAutoRelocationCurrentFolder = _contextMenuRelocationCurrentCheckBox.Checked;
        Settings.ContextMenuAutoRelocationChooseTarget = _contextMenuRelocationChooseTargetCheckBox.Checked;
        Settings.ContextMenuArchiveMergeGroupByArchiveName = _contextMenuArchiveMergeGroupByArchiveNameCheckBox.Checked;
        Settings.ContextMenuArchiveMergePreserveInternalPaths = _contextMenuArchiveMergePreserveInternalPathsCheckBox.Checked;
        Settings.RenameUseDictionary = _renameDictionaryCheckBox.Checked;
        Settings.ArchiveMergeDeleteOriginals = _archiveMergeDeleteOriginalsCheckBox.Checked;

        if (_renameReviewModeCombo.SelectedItem is ComboOption<RenameReviewMode> renameReviewMode)
        {
            Settings.RenameReviewMode = renameReviewMode.Value;
        }

        if (_contextMenuLayoutCombo.SelectedItem is ComboOption<ContextMenuLayout> layout)
        {
            Settings.ContextMenuLayout = layout.Value;
        }

        if (_defaultFolderOperationCombo.SelectedItem is ComboOption<FolderStructureOperation> operation)
        {
            Settings.FolderStructureOperation = operation.Value;
        }

        if (_folderMismatchCombo.SelectedItem is ComboOption<FolderUnwrapNameMismatchMode> mismatchMode)
        {
            Settings.FolderUnwrapNameMismatchMode = mismatchMode.Value;
        }

        if (_defaultTemplateCombo.SelectedItem is ComboOption<string> template)
        {
            Settings.AutoRelocationTemplateId = template.Value;
        }

        if (_archiveMergeLayoutCombo.SelectedItem is ComboOption<ArchiveMergeLayout> archiveLayout)
        {
            Settings.ArchiveMergeLayout = archiveLayout.Value;
        }

        if (_archiveMergeCollisionCombo.SelectedItem is ComboOption<ArchiveMergeCollisionPolicy> archiveCollision)
        {
            Settings.ArchiveMergeCollisionPolicy = archiveCollision.Value;
        }

        if (_archiveMergeDuplicateCombo.SelectedItem is ComboOption<ArchiveMergeDuplicatePolicy> archiveDuplicate)
        {
            Settings.ArchiveMergeDuplicatePolicy = archiveDuplicate.Value;
        }

        if (_archiveMergeFailureCombo.SelectedItem is ComboOption<ArchiveMergeFailurePolicy> archiveFailure)
        {
            Settings.ArchiveMergeFailurePolicy = archiveFailure.Value;
        }

        if (_archiveMergeOutputNameCombo.SelectedItem is ComboOption<ArchiveMergeOutputNamePolicy> archiveOutputName)
        {
            Settings.ArchiveMergeOutputNamePolicy = archiveOutputName.Value;
        }

        if (_archiveMergeCompressionCombo.SelectedItem is ComboOption<ArchiveMergeCompressionLevel> archiveCompression)
        {
            Settings.ArchiveMergeCompressionLevel = archiveCompression.Value;
        }
    }

    private void SyncContextMenuRegistration()
    {
        if (Settings.RegisterContextMenu)
        {
            ContextMenuRegistrar.Install(Environment.ProcessPath ?? "", Settings);
            return;
        }

        ContextMenuRegistrar.Uninstall();
    }

    private void InstallContextMenu()
    {
        try
        {
            SaveSettingsFromUi();
            Settings.RegisterContextMenu = true;
            _registerContextMenuCheckBox.Checked = true;
            SettingsStore.Save(Settings);
            var installedPath = ContextMenuRegistrar.Install(Environment.ProcessPath ?? "", Settings);
            UpdateUiState();
            MessageBox.Show(
                Localizer.Format("ContextMenuInstalledFormat", installedPath),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UninstallContextMenu()
    {
        ContextMenuRegistrar.Uninstall();
        Settings.RegisterContextMenu = false;
        _registerContextMenuCheckBox.Checked = false;
        SettingsStore.Save(Settings);
        UpdateUiState();
        MessageBox.Show(
            Localizer.Get("ContextMenuRemoved"),
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void RegisterWindows11NativeContextMenu()
    {
        if (!ConfirmWindows11NativeContextMenu("Windows11ContextMenuRegisterWarning"))
        {
            return;
        }

        try
        {
            RegisterInstalledWindows11NativeContextMenu();
            MessageBox.Show(
                Localizer.Get("Windows11ContextMenuRegistered"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UnregisterWindows11NativeContextMenu()
    {
        if (!ConfirmWindows11NativeContextMenu("Windows11ContextMenuUnregisterWarning"))
        {
            return;
        }

        try
        {
            Windows11NativeContextMenuRegistrar.Uninstall();
            MessageBox.Show(
                Localizer.Get("Windows11ContextMenuUnregistered"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool ConfirmWindows11NativeContextMenu(string messageKey)
    {
        return MessageBox.Show(
            Localizer.Get(messageKey),
            Localizer.Get("Windows11ContextMenuWarningTitle"),
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.OK;
    }

    private static void RegisterInstalledWindows11NativeContextMenu()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var missingFiles = Windows11NativeContextMenuRegistrar.GetMissingSupportFiles(baseDirectory);
        if (missingFiles.Length > 0)
        {
            throw new FileNotFoundException(Localizer.Format(
                "Windows11ContextMenuFilesMissing",
                string.Join(Environment.NewLine, missingFiles)));
        }

        Windows11NativeContextMenuRegistrar.Install(baseDirectory);
    }

    private void OpenRenameRuleEditor()
    {
        var document = RenameRuleStore.Load();
        var dictionary = RenameDictionaryStore.Load();
        var parserProfile = RenameParserProfileStore.Load();
        var candidateProfile = RenameCandidateProfileStore.Load(dictionary.CommonPhrases);
        using var dialog = new RenameRuleEditorDialog(document.Rules, dictionary, parserProfile, candidateProfile);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            document.Rules = dialog.Rules.ToList();
            RenameRuleStore.Save(document);
            RenameDictionaryStore.Save(dialog.RenameDictionary);
            RenameParserProfileStore.Save(dialog.ParserProfile);
            RenameCandidateProfileStore.Save(dialog.CandidateProfile);
        }
    }

    private void OpenTemplateEditor()
    {
        var selectedTemplateId = _defaultTemplateCombo.SelectedItem is ComboOption<string> selectedTemplate
            ? selectedTemplate.Value
            : Settings.AutoRelocationTemplateId;
        using var dialog = new AutoRelocationTemplateEditorDialog();
        dialog.ShowDialog(this);
        RefreshTemplateCombo(selectedTemplateId);
        UpdateUiState();
    }

    private void OpenNameTemplateSettings()
    {
        SaveSettingsFromUi();
        using var dialog = new NameTemplateSettingsDialog(Settings);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Settings = dialog.Settings.Clone();
            LoadSettings();
            UpdateUiState();
        }
    }

    private void OpenFileKindClassificationEditor()
    {
        using var dialog = new FileKindClassificationEditorDialog(Settings.FileKindExtensionRules);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Settings.FileKindExtensionRules = dialog.Rules.ToList();
            UpdateUiState();
        }
    }

    private void RefreshTemplateCombo(string? selectedTemplateId)
    {
        ConfigureCombo(_defaultTemplateCombo, AutoRelocationTemplateStore.LoadTemplates()
            .Select(template => new ComboOption<string>(
                $"{template.Document.DisplayName} ({template.Document.Id})",
                template.Document.Id))
            .ToArray());
        SelectComboValue(_defaultTemplateCombo, string.IsNullOrWhiteSpace(selectedTemplateId)
            ? AutoRelocationTemplateDefaults.DefaultTemplateId
            : selectedTemplateId);
    }

    private static string GetComboText(ComboBox combo)
    {
        return combo.SelectedItem?.ToString() ?? "";
    }

    private static string TrimSummary(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..Math.Max(0, maxLength - 3)] + "...";
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
