using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class SettingsForm : Form
{
    private readonly CheckBox _registerContextMenuCheckBox = new();
    private readonly CheckBox _contextMenuOpenCheckBox = new();
    private readonly CheckBox _contextMenuRenameCheckBox = new();
    private readonly CheckBox _contextMenuFileCompareCheckBox = new();
    private readonly CheckBox _contextMenuFolderWrapCheckBox = new();
    private readonly CheckBox _contextMenuFolderUnwrapSameNameCheckBox = new();
    private readonly CheckBox _contextMenuFolderUnwrapSingleFileCheckBox = new();
    private readonly CheckBox _contextMenuFolderMoveInnerFilesCheckBox = new();
    private readonly CheckBox _contextMenuFolderMergeSelectedTargetsCheckBox = new();
    private readonly CheckBox _contextMenuRelocationCurrentCheckBox = new();
    private readonly CheckBox _contextMenuRelocationChooseTargetCheckBox = new();
    private readonly CheckBox _contextMenuArchiveMergeGroupByArchiveNameCheckBox = new();
    private readonly CheckBox _contextMenuArchiveMergePreserveInternalPathsCheckBox = new();
    private readonly ComboBox _contextMenuLayoutCombo = new();
    private readonly ComboBox _actionToolbarSizeCombo = new();
    private readonly ComboBox _defaultFolderOperationCombo = new();
    private readonly ComboBox _folderMismatchCombo = new();
    private readonly ComboBox _defaultTemplateCombo = new();
    private readonly ComboBox _renameReviewModeCombo = new();
    private readonly ComboBox _renamePluginLanguageCombo = new();
    private readonly ComboBox _archiveMergeLayoutCombo = new();
    private readonly ComboBox _archiveMergeCollisionCombo = new();
    private readonly ComboBox _archiveMergeDuplicateCombo = new();
    private readonly ComboBox _archiveMergeFailureCombo = new();
    private readonly ComboBox _archiveMergeOutputNameCombo = new();
    private readonly ComboBox _archiveMergeCompressionCombo = new();
    private readonly CheckBox _fileCompareNameCheckBox = new();
    private readonly ComboBox _fileCompareNameModeCombo = new();
    private readonly ComboBox _fileCompareCommonNameThresholdModeCombo = new();
    private readonly TextBox _fileCompareCommonNameThresholdBox = new();
    private readonly CheckBox _fileCompareCreatedTimeCheckBox = new();
    private readonly CheckBox _fileCompareModifiedTimeCheckBox = new();
    private readonly CheckBox _fileCompareSizeCheckBox = new();
    private readonly CheckBox _fileCompareContentCheckBox = new();
    private readonly ComboBox _fileCompareContentModeCombo = new();
    private readonly ComboBox _fileCompareRangeModeCombo = new();
    private readonly TextBox _fileCompareRangeOffsetBox = new();
    private readonly TextBox _fileCompareRangeBytesBox = new();
    private readonly ComboBox _fileCompareRangeUnitCombo = new();
    private readonly CheckBox _fileCompareExtractArchivesCheckBox = new();
    private readonly ComboBox _fileCompareArchiveOrderCombo = new();
    private readonly ComboBox _fileCompareArchiveLimitModeCombo = new();
    private readonly TextBox _fileCompareArchiveLimitCountBox = new();
    private readonly CheckBox _fileCompareArchiveSameRelativePathOnlyCheckBox = new();
    private readonly CheckBox _fileCompareEarlyExitCheckBox = new();
    private readonly CheckBox _fileCompareHashCacheCheckBox = new();
    private readonly TextBox _fileComparePrefilterPercentBox = new();
    private readonly CheckBox _renameDictionaryCheckBox = new();
    private readonly CheckBox _renamePluginCheckBox = new();
    private readonly CheckBox _renamePatternLearningCheckBox = new();
    private readonly TextBox _renamePatternFeedbackLimitBox = new();
    private readonly CheckedListBox _renamePluginList = new();
    private readonly Button _renamePluginSettingsButton = new();
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
    private CollapsibleSettingsGroup? _fileCompareGroup;
    private FileCompareRangeUnit _fileCompareCurrentRangeUnit = FileCompareRangeUnit.Bytes;
    private bool _updatingFileCompareRangeUnit;
    private IReadOnlyList<LoadedNameCorrectionPlugin> _renamePlugins = [];

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
        _contextMenuFileCompareCheckBox.Checked = Settings.ContextMenuFileCompare;
        _contextMenuFolderWrapCheckBox.Checked = Settings.ContextMenuFolderWrapFiles;
        _contextMenuFolderUnwrapSameNameCheckBox.Checked = Settings.ContextMenuFolderUnwrapSameNameSingleFile;
        _contextMenuFolderUnwrapSingleFileCheckBox.Checked = Settings.ContextMenuFolderUnwrapSingleFile;
        _contextMenuFolderMoveInnerFilesCheckBox.Checked = Settings.ContextMenuFolderMoveInnerFilesUp;
        _contextMenuFolderMergeSelectedTargetsCheckBox.Checked = Settings.ContextMenuFolderMergeSelectedTargets;
        _contextMenuRelocationCurrentCheckBox.Checked = Settings.ContextMenuAutoRelocationCurrentFolder;
        _contextMenuRelocationChooseTargetCheckBox.Checked = Settings.ContextMenuAutoRelocationChooseTarget;
        _contextMenuArchiveMergeGroupByArchiveNameCheckBox.Checked = Settings.ContextMenuArchiveMergeGroupByArchiveName;
        _contextMenuArchiveMergePreserveInternalPathsCheckBox.Checked = Settings.ContextMenuArchiveMergePreserveInternalPaths;
        _renameDictionaryCheckBox.Checked = Settings.RenameUseDictionary;
        _renamePluginCheckBox.Checked = Settings.RenameCorrectionPlugins.Enabled;
        _renamePatternLearningCheckBox.Checked = Settings.RenamePatternLearningEnabled;
        _renamePatternFeedbackLimitBox.Text = Settings.RenamePatternFeedbackLimit.ToString(CultureInfo.CurrentCulture);
        _archiveMergeDeleteOriginalsCheckBox.Checked = Settings.ArchiveMergeDeleteOriginals;
        _fileCompareNameCheckBox.Checked = Settings.FileCompareOptions.CompareFileName;
        _fileCompareCreatedTimeCheckBox.Checked = Settings.FileCompareOptions.CompareCreatedTime;
        _fileCompareModifiedTimeCheckBox.Checked = Settings.FileCompareOptions.CompareModifiedTime;
        _fileCompareSizeCheckBox.Checked = Settings.FileCompareOptions.CompareFileSize;
        _fileCompareContentCheckBox.Checked = Settings.FileCompareOptions.CompareContent;
        _fileCompareExtractArchivesCheckBox.Checked = Settings.FileCompareOptions.ArchiveMode == FileCompareArchiveMode.ExtractEntries;
        _fileCompareArchiveSameRelativePathOnlyCheckBox.Checked = Settings.FileCompareOptions.ArchiveCompareSameRelativePathOnly;
        _fileCompareEarlyExitCheckBox.Checked = Settings.FileCompareOptions.EnableEarlyExit;
        _fileCompareHashCacheCheckBox.Checked = Settings.FileCompareOptions.UseHashCache;
        _fileCompareCurrentRangeUnit = Settings.FileCompareOptions.RangeUnit;
        _fileCompareRangeOffsetBox.Text = FileCompareText
            .ConvertBytesToRangeValue(Settings.FileCompareOptions.RangeOffsetBytes, _fileCompareCurrentRangeUnit)
            .ToString(CultureInfo.CurrentCulture);
        _fileCompareRangeBytesBox.Text = FileCompareText
            .ConvertBytesToRangeValue(Settings.FileCompareOptions.RangeBytes, _fileCompareCurrentRangeUnit)
            .ToString(CultureInfo.CurrentCulture);
        _fileCompareCommonNameThresholdBox.Text =
            Settings.FileCompareOptions.CommonNameThresholdMode == FileCompareCommonNameThresholdMode.Percent
                ? (Settings.FileCompareOptions.CommonNameMinimumPercent * 100).ToString("0.##", CultureInfo.CurrentCulture)
                : Settings.FileCompareOptions.CommonNameMinimumCharacters.ToString(CultureInfo.CurrentCulture);
        _fileCompareArchiveLimitCountBox.Text = Settings.FileCompareOptions.ArchiveEntryLimitCount.ToString(CultureInfo.CurrentCulture);
        _fileComparePrefilterPercentBox.Text = (Settings.FileCompareOptions.ByteToBytePrefilterRatio * 100).ToString("0.##", CultureInfo.CurrentCulture);

        ConfigureCombo(_renameReviewModeCombo, Enum.GetValues<RenameReviewMode>()
            .Select(mode => new ComboOption<RenameReviewMode>(ToolModeText.GetDisplayName(mode), mode))
            .ToArray());
        SelectComboValue(_renameReviewModeCombo, Settings.RenameReviewMode);

        ConfigureCombo(_renamePluginLanguageCombo, RenameCorrectionPluginDefaults.SupportedLanguages
            .Select(language => new ComboOption<string>(language, language))
            .ToArray());
        SelectComboValue(_renamePluginLanguageCombo, Settings.RenameCorrectionPlugins.Language);
        LoadRenamePlugins();

        ConfigureCombo(_contextMenuLayoutCombo, Enum.GetValues<ContextMenuLayout>()
            .Select(layout => new ComboOption<ContextMenuLayout>(ToolModeText.GetDisplayName(layout), layout))
            .ToArray());
        SelectComboValue(_contextMenuLayoutCombo, Settings.ContextMenuLayout);

        ConfigureCombo(_actionToolbarSizeCombo, Enum.GetValues<ActionToolbarSize>()
            .Select(size => new ComboOption<ActionToolbarSize>(ToolModeText.GetDisplayName(size), size))
            .ToArray());
        SelectComboValue(_actionToolbarSizeCombo, Settings.ActionToolbarSize);

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

        ConfigureCombo(_fileCompareNameModeCombo, Enum.GetValues<FileCompareNameMatchMode>()
            .Select(value => new ComboOption<FileCompareNameMatchMode>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_fileCompareNameModeCombo, Settings.FileCompareOptions.NameMatchMode);

        ConfigureCombo(_fileCompareCommonNameThresholdModeCombo, Enum.GetValues<FileCompareCommonNameThresholdMode>()
            .Select(value => new ComboOption<FileCompareCommonNameThresholdMode>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_fileCompareCommonNameThresholdModeCombo, Settings.FileCompareOptions.CommonNameThresholdMode);

        ConfigureCombo(_fileCompareContentModeCombo, Enum.GetValues<FileCompareContentMode>()
            .Select(value => new ComboOption<FileCompareContentMode>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_fileCompareContentModeCombo, Settings.FileCompareOptions.ContentMode);

        ConfigureCombo(_fileCompareRangeModeCombo, Enum.GetValues<FileCompareRangeMode>()
            .Select(value => new ComboOption<FileCompareRangeMode>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_fileCompareRangeModeCombo, Settings.FileCompareOptions.RangeMode);

        ConfigureCombo(_fileCompareRangeUnitCombo, Enum.GetValues<FileCompareRangeUnit>()
            .Select(value => new ComboOption<FileCompareRangeUnit>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_fileCompareRangeUnitCombo, _fileCompareCurrentRangeUnit);

        ConfigureCombo(_fileCompareArchiveOrderCombo, Enum.GetValues<FileCompareArchiveEntryOrder>()
            .Select(value => new ComboOption<FileCompareArchiveEntryOrder>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_fileCompareArchiveOrderCombo, Settings.FileCompareOptions.ArchiveEntryOrder);

        ConfigureCombo(_fileCompareArchiveLimitModeCombo, Enum.GetValues<FileCompareArchiveEntryLimitMode>()
            .Select(value => new ComboOption<FileCompareArchiveEntryLimitMode>(FileCompareText.GetDisplayName(value), value))
            .ToArray());
        SelectComboValue(_fileCompareArchiveLimitModeCombo, Settings.FileCompareOptions.ArchiveEntryLimitMode);

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
            _contextMenuFileCompareCheckBox,
            _contextMenuFolderWrapCheckBox,
            _contextMenuFolderUnwrapSameNameCheckBox,
            _contextMenuFolderUnwrapSingleFileCheckBox,
            _contextMenuFolderMoveInnerFilesCheckBox,
            _contextMenuFolderMergeSelectedTargetsCheckBox,
            _contextMenuRelocationCurrentCheckBox,
            _contextMenuRelocationChooseTargetCheckBox,
            _contextMenuArchiveMergeGroupByArchiveNameCheckBox,
            _contextMenuArchiveMergePreserveInternalPathsCheckBox,
            _renameDictionaryCheckBox,
            _renamePluginCheckBox,
            _renamePatternLearningCheckBox,
            _archiveMergeDeleteOriginalsCheckBox,
            _fileCompareNameCheckBox,
            _fileCompareCreatedTimeCheckBox,
            _fileCompareModifiedTimeCheckBox,
            _fileCompareSizeCheckBox,
            _fileCompareContentCheckBox,
            _fileCompareExtractArchivesCheckBox,
            _fileCompareArchiveSameRelativePathOnlyCheckBox,
            _fileCompareEarlyExitCheckBox,
            _fileCompareHashCacheCheckBox
        })
        {
            checkBox.CheckedChanged += (_, _) => UpdateUiState();
        }

        foreach (var combo in new[]
        {
            _renameReviewModeCombo,
            _renamePluginLanguageCombo,
            _contextMenuLayoutCombo,
            _actionToolbarSizeCombo,
            _defaultFolderOperationCombo,
            _folderMismatchCombo,
            _defaultTemplateCombo,
            _archiveMergeLayoutCombo,
            _archiveMergeCollisionCombo,
            _archiveMergeDuplicateCombo,
            _archiveMergeFailureCombo,
            _archiveMergeOutputNameCombo,
            _archiveMergeCompressionCombo,
            _fileCompareNameModeCombo,
            _fileCompareCommonNameThresholdModeCombo,
            _fileCompareContentModeCombo,
            _fileCompareRangeModeCombo,
            _fileCompareRangeUnitCombo,
            _fileCompareArchiveOrderCombo,
            _fileCompareArchiveLimitModeCombo
        })
        {
            combo.SelectedIndexChanged += (_, _) => UpdateUiState();
        }

        _fileCompareRangeUnitCombo.SelectedIndexChanged += (_, _) => ChangeFileCompareRangeUnitFromUi();
        _fileCompareCommonNameThresholdBox.TextChanged += (_, _) => UpdateUiState();
        _fileCompareRangeOffsetBox.TextChanged += (_, _) => UpdateUiState();
        _fileCompareRangeBytesBox.TextChanged += (_, _) => UpdateUiState();
        _fileCompareArchiveLimitCountBox.TextChanged += (_, _) => UpdateUiState();
        _fileComparePrefilterPercentBox.TextChanged += (_, _) => UpdateUiState();
        _renamePatternFeedbackLimitBox.TextChanged += (_, _) => UpdateUiState();
        _renamePluginList.ItemCheck += (_, _) => BeginInvoke((MethodInvoker)UpdateUiState);
        _renamePluginList.SelectedIndexChanged += (_, _) => UpdateUiState();
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

        UpdateFileCompareControlState();
        UpdateRenamePluginControlState();
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
        var pluginState = _renamePluginCheckBox.Checked
            ? Localizer.Format("RenamePluginSummaryEnabledFormat", GetCheckedRenamePluginCount())
            : Localizer.Get("RenamePluginSummaryOff");
        var learningState = _renamePatternLearningCheckBox.Checked
            ? Localizer.Format("RenamePatternLearningSummaryOnFormat", _renamePatternFeedbackLimitBox.Text)
            : Localizer.Get("RenamePatternLearningSummaryOff");
        return Localizer.Format(
            "SettingsSummaryQuadFormat",
            GetComboText(_renameReviewModeCombo),
            dictionaryState,
            pluginState,
            learningState);
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

    private string GetFileCompareSummary()
    {
        var content = _fileCompareContentCheckBox.Checked
            ? GetComboText(_fileCompareContentModeCombo)
            : Localizer.Get("FileCompareSummaryContentOff");
        return Localizer.Format(
            "SettingsSummaryPairFormat",
            GetComboText(_fileCompareNameModeCombo),
            content);
    }

    private void LoadRenamePlugins()
    {
        _renamePlugins = NameCorrectionPluginCatalog.Discover();
        _renamePluginList.Items.Clear();
        foreach (var plugin in _renamePlugins)
        {
            var configuration = Settings.RenameCorrectionPlugins.Plugins.FirstOrDefault(item =>
                string.Equals(item.PluginId, plugin.Descriptor.Id, StringComparison.OrdinalIgnoreCase));
            _renamePluginList.Items.Add(new RenamePluginListItem(plugin), configuration?.Enabled == true);
        }

        if (_renamePluginList.Items.Count > 0)
        {
            _renamePluginList.SelectedIndex = 0;
        }
    }

    private int GetCheckedRenamePluginCount()
    {
        return _renamePluginList.CheckedItems.Count;
    }

    private void SaveRenamePluginSettingsFromUi()
    {
        Settings.RenameCorrectionPlugins = RenameCorrectionPluginDefaults.Normalize(Settings.RenameCorrectionPlugins);
        for (var index = 0; index < _renamePluginList.Items.Count; index++)
        {
            if (_renamePluginList.Items[index] is not RenamePluginListItem item)
            {
                continue;
            }

            var configuration = RenameCorrectionPluginDefaults.GetOrCreatePlugin(
                Settings.RenameCorrectionPlugins,
                item.Plugin.Descriptor.Id);
            configuration.Enabled = _renamePluginList.GetItemChecked(index);
            configuration.Settings = new Dictionary<string, string>(
                NameCorrectionPluginHost.BuildSettings(item.Plugin, configuration),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    private void UpdateRenamePluginControlState()
    {
        var enabled = _renamePluginCheckBox.Checked;
        _renamePluginLanguageCombo.Enabled = enabled;
        _renamePluginList.Enabled = enabled && _renamePluginList.Items.Count > 0;
        _renamePluginSettingsButton.Enabled = enabled && _renamePluginList.SelectedItem is RenamePluginListItem;
        _renamePatternFeedbackLimitBox.Enabled = _renamePatternLearningCheckBox.Checked;
    }

    private int GetEnabledContextMenuCommandCount()
    {
        return new[]
        {
            _contextMenuOpenCheckBox,
            _contextMenuRenameCheckBox,
            _contextMenuFileCompareCheckBox,
            _contextMenuFolderWrapCheckBox,
            _contextMenuFolderUnwrapSameNameCheckBox,
            _contextMenuFolderUnwrapSingleFileCheckBox,
            _contextMenuFolderMoveInnerFilesCheckBox,
            _contextMenuFolderMergeSelectedTargetsCheckBox,
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
        Settings.ContextMenuFileCompare = _contextMenuFileCompareCheckBox.Checked;
        Settings.ContextMenuFolderStructure = true;
        Settings.ContextMenuFolderWrapFiles = _contextMenuFolderWrapCheckBox.Checked;
        Settings.ContextMenuFolderUnwrapSameNameSingleFile = _contextMenuFolderUnwrapSameNameCheckBox.Checked;
        Settings.ContextMenuFolderUnwrapSingleFile = _contextMenuFolderUnwrapSingleFileCheckBox.Checked;
        Settings.ContextMenuFolderMoveInnerFilesUp = _contextMenuFolderMoveInnerFilesCheckBox.Checked;
        Settings.ContextMenuFolderMergeSelectedTargets = _contextMenuFolderMergeSelectedTargetsCheckBox.Checked;
        Settings.ContextMenuAutoRelocation = true;
        Settings.ContextMenuAutoRelocationCurrentFolder = _contextMenuRelocationCurrentCheckBox.Checked;
        Settings.ContextMenuAutoRelocationChooseTarget = _contextMenuRelocationChooseTargetCheckBox.Checked;
        Settings.ContextMenuArchiveMergeGroupByArchiveName = _contextMenuArchiveMergeGroupByArchiveNameCheckBox.Checked;
        Settings.ContextMenuArchiveMergePreserveInternalPaths = _contextMenuArchiveMergePreserveInternalPathsCheckBox.Checked;
        Settings.RenameUseDictionary = _renameDictionaryCheckBox.Checked;
        Settings.RenameCorrectionPlugins.Enabled = _renamePluginCheckBox.Checked;
        Settings.RenamePatternLearningEnabled = _renamePatternLearningCheckBox.Checked;
        Settings.RenamePatternFeedbackLimit = (int)ParseLongText(
            _renamePatternFeedbackLimitBox,
            Settings.RenamePatternFeedbackLimit,
            FileNamePatternFeedbackStore.MinimumFeedbackLimit,
            int.MaxValue);
        Settings.RenameCorrectionPlugins.Language = GetSelectedComboValue(
            _renamePluginLanguageCombo,
            RenameCorrectionPluginDefaults.DefaultLanguage);
        SaveRenamePluginSettingsFromUi();
        Settings.ArchiveMergeDeleteOriginals = _archiveMergeDeleteOriginalsCheckBox.Checked;
        Settings.FileCompareOptions.CompareFileName = _fileCompareNameCheckBox.Checked;
        Settings.FileCompareOptions.CompareCreatedTime = _fileCompareCreatedTimeCheckBox.Checked;
        Settings.FileCompareOptions.CompareModifiedTime = _fileCompareModifiedTimeCheckBox.Checked;
        Settings.FileCompareOptions.CompareFileSize = _fileCompareSizeCheckBox.Checked;
        Settings.FileCompareOptions.CompareContent = _fileCompareContentCheckBox.Checked;
        Settings.FileCompareOptions.ArchiveMode = _fileCompareExtractArchivesCheckBox.Checked
            ? FileCompareArchiveMode.ExtractEntries
            : FileCompareArchiveMode.AsFile;
        Settings.FileCompareOptions.EnableEarlyExit = _fileCompareEarlyExitCheckBox.Checked;
        Settings.FileCompareOptions.UseHashCache = _fileCompareHashCacheCheckBox.Checked;
        Settings.FileCompareOptions.RangeUnit = GetSelectedComboValue(_fileCompareRangeUnitCombo, _fileCompareCurrentRangeUnit);
        Settings.FileCompareOptions.RangeOffsetBytes = ParseUnitLongText(
            _fileCompareRangeOffsetBox,
            Settings.FileCompareOptions.RangeUnit,
            0,
            long.MaxValue);
        Settings.FileCompareOptions.RangeBytes = ParseUnitLongText(
            _fileCompareRangeBytesBox,
            Settings.FileCompareOptions.RangeUnit,
            1,
            long.MaxValue);
        Settings.FileCompareOptions.CommonNameThresholdMode = GetSelectedComboValue(
            _fileCompareCommonNameThresholdModeCombo,
            Settings.FileCompareOptions.CommonNameThresholdMode);
        if (Settings.FileCompareOptions.CommonNameThresholdMode == FileCompareCommonNameThresholdMode.Percent)
        {
            Settings.FileCompareOptions.CommonNameMinimumPercent = ParsePercentText(
                _fileCompareCommonNameThresholdBox,
                Settings.FileCompareOptions.CommonNameMinimumPercent);
        }
        else
        {
            Settings.FileCompareOptions.CommonNameMinimumCharacters = (int)ParseLongText(
                _fileCompareCommonNameThresholdBox,
                Settings.FileCompareOptions.CommonNameMinimumCharacters,
                1,
                int.MaxValue);
        }

        Settings.FileCompareOptions.ByteToBytePrefilterRatio = ParsePercentText(_fileComparePrefilterPercentBox, Settings.FileCompareOptions.ByteToBytePrefilterRatio);

        if (_renameReviewModeCombo.SelectedItem is ComboOption<RenameReviewMode> renameReviewMode)
        {
            Settings.RenameReviewMode = renameReviewMode.Value;
        }

        if (_contextMenuLayoutCombo.SelectedItem is ComboOption<ContextMenuLayout> layout)
        {
            Settings.ContextMenuLayout = layout.Value;
        }

        if (_actionToolbarSizeCombo.SelectedItem is ComboOption<ActionToolbarSize> toolbarSize)
        {
            Settings.ActionToolbarSize = toolbarSize.Value;
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

        if (_fileCompareNameModeCombo.SelectedItem is ComboOption<FileCompareNameMatchMode> nameMode)
        {
            Settings.FileCompareOptions.NameMatchMode = nameMode.Value;
        }

        if (_fileCompareContentModeCombo.SelectedItem is ComboOption<FileCompareContentMode> contentMode)
        {
            Settings.FileCompareOptions.ContentMode = contentMode.Value;
        }

        if (_fileCompareRangeModeCombo.SelectedItem is ComboOption<FileCompareRangeMode> rangeMode)
        {
            Settings.FileCompareOptions.RangeMode = rangeMode.Value;
        }

        if (_fileCompareArchiveOrderCombo.SelectedItem is ComboOption<FileCompareArchiveEntryOrder> archiveOrder)
        {
            Settings.FileCompareOptions.ArchiveEntryOrder = archiveOrder.Value;
        }

        if (_fileCompareArchiveLimitModeCombo.SelectedItem is ComboOption<FileCompareArchiveEntryLimitMode> archiveLimitMode)
        {
            Settings.FileCompareOptions.ArchiveEntryLimitMode = archiveLimitMode.Value;
        }

        Settings.FileCompareOptions.ArchiveEntryLimitCount = (int)ParseLongText(
            _fileCompareArchiveLimitCountBox,
            Settings.FileCompareOptions.ArchiveEntryLimitCount,
            1,
            int.MaxValue);
        Settings.FileCompareOptions.ArchiveCompareSameRelativePathOnly =
            _fileCompareArchiveSameRelativePathOnlyCheckBox.Checked;
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

    private void OpenRenamePluginSettings()
    {
        if (_renamePluginList.SelectedItem is not RenamePluginListItem item)
        {
            return;
        }

        var configuration = RenameCorrectionPluginDefaults.GetOrCreatePlugin(
            Settings.RenameCorrectionPlugins,
            item.Plugin.Descriptor.Id);
        using var dialog = new NameCorrectionPluginSettingsDialog(
            item.Plugin,
            NameCorrectionPluginHost.BuildSettings(item.Plugin, configuration));
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            configuration.Settings = new Dictionary<string, string>(dialog.Settings, StringComparer.OrdinalIgnoreCase);
            UpdateUiState();
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

    private void UpdateFileCompareControlState()
    {
        _fileCompareNameModeCombo.Enabled = _fileCompareNameCheckBox.Checked;
        var commonNameEnabled = _fileCompareNameCheckBox.Checked &&
                                _fileCompareNameModeCombo.SelectedItem is ComboOption<FileCompareNameMatchMode>
                                {
                                    Value: FileCompareNameMatchMode.CommonName
                                };
        _fileCompareCommonNameThresholdModeCombo.Enabled = commonNameEnabled;
        _fileCompareCommonNameThresholdBox.Enabled = commonNameEnabled;
        var contentEnabled = _fileCompareContentCheckBox.Checked;
        _fileCompareContentModeCombo.Enabled = contentEnabled;
        _fileCompareRangeModeCombo.Enabled = contentEnabled;
        var rangeEnabled = contentEnabled &&
                           _fileCompareRangeModeCombo.SelectedItem is not ComboOption<FileCompareRangeMode>
                           {
                               Value: FileCompareRangeMode.Full
                           };
        _fileCompareRangeOffsetBox.Enabled = rangeEnabled &&
                                             _fileCompareRangeModeCombo.SelectedItem is ComboOption<FileCompareRangeMode>
                                             {
                                                 Value: FileCompareRangeMode.MiddleBytes
                                             };
        _fileCompareRangeBytesBox.Enabled = rangeEnabled;
        _fileCompareRangeUnitCombo.Enabled = rangeEnabled;
        _fileCompareExtractArchivesCheckBox.Enabled = contentEnabled;
        var archiveEnabled = contentEnabled && _fileCompareExtractArchivesCheckBox.Checked;
        _fileCompareArchiveOrderCombo.Enabled = archiveEnabled;
        _fileCompareArchiveLimitModeCombo.Enabled = archiveEnabled;
        _fileCompareArchiveLimitCountBox.Enabled = archiveEnabled &&
                                                   _fileCompareArchiveLimitModeCombo.SelectedItem is ComboOption<FileCompareArchiveEntryLimitMode>
                                                   {
                                                       Value: FileCompareArchiveEntryLimitMode.FirstN
                                                   };
        _fileCompareArchiveSameRelativePathOnlyCheckBox.Enabled = archiveEnabled;
        _fileComparePrefilterPercentBox.Enabled = contentEnabled &&
                                                  _fileCompareContentModeCombo.SelectedItem is ComboOption<FileCompareContentMode>
                                                  {
                                                      Value: FileCompareContentMode.ByteToByte
                                                  };
    }

    private void ChangeFileCompareRangeUnitFromUi()
    {
        if (_updatingFileCompareRangeUnit ||
            _fileCompareRangeUnitCombo.SelectedItem is not ComboOption<FileCompareRangeUnit> option)
        {
            return;
        }

        var newUnit = option.Value;
        if (newUnit == _fileCompareCurrentRangeUnit)
        {
            return;
        }

        try
        {
            _updatingFileCompareRangeUnit = true;
            ConvertUnitTextBox(_fileCompareRangeOffsetBox, _fileCompareCurrentRangeUnit, newUnit, minimum: 0);
            ConvertUnitTextBox(_fileCompareRangeBytesBox, _fileCompareCurrentRangeUnit, newUnit, minimum: 1);
            _fileCompareCurrentRangeUnit = newUnit;
        }
        catch
        {
            SelectComboValue(_fileCompareRangeUnitCombo, _fileCompareCurrentRangeUnit);
        }
        finally
        {
            _updatingFileCompareRangeUnit = false;
        }
    }

    private static long ParseLongText(TextBox textBox, long fallback, long minimum, long maximum)
    {
        _ = fallback;
        if (!long.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value))
        {
            throw new InvalidOperationException(Localizer.Format("SettingsInvalidNumberFormat", textBox.Text));
        }

        return Math.Clamp(value, minimum, maximum);
    }

    private static long ParseUnitLongText(TextBox textBox, FileCompareRangeUnit unit, long minimum, long maximum)
    {
        var value = ParseLongText(textBox, minimum, minimum, maximum);
        return Math.Clamp(FileCompareText.ConvertRangeValueToBytes(value, unit), minimum, maximum);
    }

    private static void ConvertUnitTextBox(
        TextBox textBox,
        FileCompareRangeUnit oldUnit,
        FileCompareRangeUnit newUnit,
        long minimum)
    {
        var currentValue = ParseLongText(textBox, minimum, minimum, long.MaxValue);
        var bytes = FileCompareText.ConvertRangeValueToBytes(currentValue, oldUnit);
        var newValue = Math.Max(minimum, FileCompareText.ConvertBytesToRangeValue(bytes, newUnit));
        textBox.Text = newValue.ToString(CultureInfo.CurrentCulture);
    }

    private static double ParsePercentText(TextBox textBox, double fallback)
    {
        _ = fallback;
        if (!double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            throw new InvalidOperationException(Localizer.Format("SettingsInvalidNumberFormat", textBox.Text));
        }

        return Math.Clamp(value / 100, 0, 1);
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

    private static T GetSelectedComboValue<T>(ComboBox combo, T fallback)
    {
        return combo.SelectedItem is ComboOption<T> option ? option.Value : fallback;
    }

    private sealed record RenamePluginListItem(LoadedNameCorrectionPlugin Plugin)
    {
        public override string ToString()
        {
            var descriptor = Plugin.Descriptor;
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(descriptor.License))
            {
                details.Add(descriptor.License);
            }

            if (descriptor.SupportedLanguages.Count > 0)
            {
                details.Add(string.Join(", ", descriptor.SupportedLanguages));
            }

            return details.Count == 0
                ? descriptor.DisplayName
                : $"{descriptor.DisplayName} ({string.Join(" - ", details)})";
        }
    }
}
