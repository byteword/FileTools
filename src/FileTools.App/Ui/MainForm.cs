using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace FileTools;

public sealed partial class MainForm : Form
{
    private const string TargetIconColumnName = "TargetIcon";
    private const string TargetNameColumnName = "TargetName";
    private const string TargetLocationColumnName = "TargetLocation";
    private const string TargetActionsColumnName = "TargetActions";
    private const string PlanOrderColumnName = "PlanOrder";
    private const string PlanActionColumnName = "PlanAction";
    private const string PlanPreviewColumnName = "PlanPreview";
    private const int ExecutionPanelDefaultHeight = 96;
    private const int ExecutionPanelDecisionHeight = 220;

    private readonly string[] _initialPaths;
    private readonly MainFormStartupAction _startupAction;
    private readonly BindingList<WorkTargetPlan> _targets = [];
    private FileToolsSettings _settings = new();
    private CancellationTokenSource? _executionCancellation;
    private FileCompareProgressState? _fileCompareProgressState;
    private FileCompareProgressDialog? _fileCompareProgressDialog;
    private bool _updatingTargetGridSelection;

    public MainForm()
        : this(null, MainFormStartupAction.None)
    {
    }

    public MainForm(IEnumerable<string>? initialPaths)
        : this(initialPaths, MainFormStartupAction.None)
    {
    }

    internal MainForm(IEnumerable<string>? initialPaths, MainFormStartupAction startupAction)
    {
        _initialPaths = initialPaths?.ToArray() ?? [];
        _startupAction = startupAction;
        InitializeComponent();
        InitializeRuntimeBindings();
        ApplyLocalization();
    }

    private void InitializeRuntimeBindings()
    {
        Load += (_, _) =>
        {
            if (!IsDesignerHosted())
            {
                LoadState();
            }
        };

        DragEnter += FileDrop_DragEnter;
        DragDrop += FileDrop_DragDrop;
        ConfigureTargetGrid();
        ConfigurePlanGrid();
        ApplyCommandImages();

        _targetGrid.SelectionChanged += (_, _) =>
        {
            if (!_updatingTargetGridSelection)
            {
                RefreshPlanList();
                UpdateCommandStates();
            }
        };
        _targetGrid.DragEnter += FileDrop_DragEnter;
        _targetGrid.DragDrop += FileDrop_DragDrop;
        _planGrid.CellDoubleClick += (_, _) => EditSelectedStep();
        _planGrid.SelectionChanged += (_, _) => UpdateCommandStates();

        _addFilesMenuItem.Click += (_, _) => AddFiles();
        _addFolderMenuItem.Click += (_, _) => AddFolder();
        _removeTargetMenuItem.Click += (_, _) => RemoveSelectedTarget();
        _mergeSelectedMenuItem.Click += (_, _) => MergeSelectedTargets();
        _clearTargetsMenuItem.Click += (_, _) => ClearTargets();
        _addRenameMenuItem.Click += (_, _) => AddRenameSteps();
        _addWrapMenuItem.Click += (_, _) => AddStep(CreateWrapStep());
        _addDefaultUnwrapMenuItem.Click += (_, _) => AddStep(CreateDefaultUnwrapStep());
        _addSameNameUnwrapMenuItem.Click += (_, _) => AddStep(CreateUnwrapStep(
            FolderStructureOperation.UnwrapSameNameSingleFile,
            _settings.FolderUnwrapNameMismatchMode));
        _addKeepNameUnwrapMenuItem.Click += (_, _) => AddStep(CreateUnwrapStep(
            FolderStructureOperation.UnwrapSingleFileFolder,
            FolderUnwrapNameMismatchMode.KeepFileName));
        _addUseFolderNameUnwrapMenuItem.Click += (_, _) => AddStep(CreateUnwrapStep(
            FolderStructureOperation.UnwrapSingleFileFolder,
            FolderUnwrapNameMismatchMode.UseFolderName));
        _addPrefixFolderNameUnwrapMenuItem.Click += (_, _) => AddStep(CreateUnwrapStep(
            FolderStructureOperation.UnwrapSingleFileFolder,
            FolderUnwrapNameMismatchMode.PrefixFolderName));
        _addMoveInnerFilesUpMenuItem.Click += (_, _) => AddStep(CreateUnwrapStep(
            FolderStructureOperation.MoveInnerFilesUp,
            _settings.FolderUnwrapNameMismatchMode));
        _addArchiveMergeGroupMenuItem.Click += (_, _) => AddArchiveMergeStep(ArchiveMergeLayout.GroupByArchiveName);
        _addArchiveMergePreserveMenuItem.Click += (_, _) => AddArchiveMergeStep(ArchiveMergeLayout.PreserveInternalPaths);
        _compareSelectedMenuItem.Click += (_, _) => OpenFileCompareDialog();
        _showCompareProgressMenuItem.Click += (_, _) => ShowFileCompareProgressDialog();
        _addRelocationMenuItem.Click += (_, _) => AddStep(CreateAutoRelocationStep());
        _removeStepMenuItem.Click += (_, _) => RemoveSelectedStep();
        _clearStepsMenuItem.Click += (_, _) => ClearSelectedTargetSteps();
        _runStopMenuItem.Click += (_, _) => RunOrStopPlan();
        _openSettingsMenuItem.Click += (_, _) => OpenSettings();

        _addTargetToolButton.ButtonClick += (_, _) => AddFiles();
        _addFilesTargetMenuItem.Click += (_, _) => AddFiles();
        _addFolderTargetMenuItem.Click += (_, _) => AddFolder();
        _removeTargetToolButton.Click += (_, _) => RemoveSelectedTarget();
        _moveTargetUpToolButton.Click += (_, _) => MoveSelectedTargets(-1);
        _moveTargetDownToolButton.Click += (_, _) => MoveSelectedTargets(1);
        _mergeSelectedToolButton.Click += (_, _) => MergeSelectedTargets();
        _clearTargetsToolButton.Click += (_, _) => ClearTargets();

        _addRenameToolButton.Click += (_, _) => AddRenameSteps();
        _addWrapToolButton.Click += (_, _) => AddStep(CreateWrapStep());
        _addUnwrapToolButton.ButtonClick += (_, _) => AddStep(CreateDefaultUnwrapStep());
        _addDefaultUnwrapToolItem.Click += (_, _) => AddStep(CreateDefaultUnwrapStep());
        _addSameNameUnwrapToolItem.Click += (_, _) => AddStep(CreateUnwrapStep(
            FolderStructureOperation.UnwrapSameNameSingleFile,
            _settings.FolderUnwrapNameMismatchMode));
        _addKeepNameUnwrapToolItem.Click += (_, _) => AddStep(CreateUnwrapStep(
            FolderStructureOperation.UnwrapSingleFileFolder,
            FolderUnwrapNameMismatchMode.KeepFileName));
        _addUseFolderNameUnwrapToolItem.Click += (_, _) => AddStep(CreateUnwrapStep(
            FolderStructureOperation.UnwrapSingleFileFolder,
            FolderUnwrapNameMismatchMode.UseFolderName));
        _addPrefixFolderNameUnwrapToolItem.Click += (_, _) => AddStep(CreateUnwrapStep(
            FolderStructureOperation.UnwrapSingleFileFolder,
            FolderUnwrapNameMismatchMode.PrefixFolderName));
        _addMoveInnerFilesUpToolItem.Click += (_, _) => AddStep(CreateUnwrapStep(
            FolderStructureOperation.MoveInnerFilesUp,
            _settings.FolderUnwrapNameMismatchMode));
        _addArchiveMergeToolButton.ButtonClick += (_, _) => AddArchiveMergeStep(ArchiveMergeLayout.GroupByArchiveName);
        _addArchiveMergeGroupToolItem.Click += (_, _) => AddArchiveMergeStep(ArchiveMergeLayout.GroupByArchiveName);
        _addArchiveMergePreserveToolItem.Click += (_, _) => AddArchiveMergeStep(ArchiveMergeLayout.PreserveInternalPaths);
        _compareSelectedToolButton.Click += (_, _) => OpenFileCompareDialog();
        _showCompareProgressToolButton.Click += (_, _) => ShowFileCompareProgressDialog();
        _addRelocationToolButton.Click += (_, _) => AddStep(CreateAutoRelocationStep());
        _removeStepToolButton.Click += (_, _) => RemoveSelectedStep();
        _clearStepsToolButton.Click += (_, _) => ClearSelectedTargetSteps();
        _runStopButton.Click += (_, _) => RunOrStopPlan();
        _archiveMergeDecisionPanel.DecisionAdded += (_, e) =>
            AppendLog(Localizer.Format("ArchiveMergeDecisionAddedFormat", e.Title));
        _archiveMergeDecisionPanel.PendingCountChanged += (_, _) => UpdateArchiveMergeDecisionPanelVisibility();
    }

    private void ApplyLocalization()
    {
        Text = Localizer.Get("MainFormTitle");
        _targetsGroup.Text = Localizer.Get("GroupDropTargets");
        _planGroup.Text = Localizer.Get("GroupWorkPlan");
        _planScopeLabel.Text = Localizer.Get("PlanScopeNoSelection");

        _fileMenuItem.Text = Localizer.Get("MenuFile");
        _taskMenuItem.Text = Localizer.Get("MenuTasks");
        _settingsMenuItem.Text = Localizer.Get("MenuSettings");
        _addFilesMenuItem.Text = Localizer.Get("ButtonAddFiles");
        _addFolderMenuItem.Text = Localizer.Get("ButtonAddFolder");
        _removeTargetMenuItem.Text = Localizer.Get("ButtonRemoveSelected");
        _mergeSelectedMenuItem.Text = Localizer.Get("ButtonMergeSelectedTargets");
        _clearTargetsMenuItem.Text = Localizer.Get("ButtonClear");
        _addRenameMenuItem.Text = Localizer.Get("ButtonAddRenameStep");
        _addWrapMenuItem.Text = Localizer.Get("ButtonAddWrapStep");
        _addDefaultUnwrapMenuItem.Text = Localizer.Get("MenuUnwrapDefault");
        _addSameNameUnwrapMenuItem.Text = ToolModeText.GetDisplayName(FolderStructureOperation.UnwrapSameNameSingleFile);
        _addKeepNameUnwrapMenuItem.Text = FormatUnwrapSingleMenuText(FolderUnwrapNameMismatchMode.KeepFileName);
        _addUseFolderNameUnwrapMenuItem.Text = FormatUnwrapSingleMenuText(FolderUnwrapNameMismatchMode.UseFolderName);
        _addPrefixFolderNameUnwrapMenuItem.Text = FormatUnwrapSingleMenuText(FolderUnwrapNameMismatchMode.PrefixFolderName);
        _addMoveInnerFilesUpMenuItem.Text = ToolModeText.GetDisplayName(FolderStructureOperation.MoveInnerFilesUp);
        _addArchiveMergeGroupMenuItem.Text = Localizer.Get("ContextCommandArchiveMergeGroupByArchiveName");
        _addArchiveMergePreserveMenuItem.Text = Localizer.Get("ContextCommandArchiveMergePreserveInternalPaths");
        _compareSelectedMenuItem.Text = Localizer.Get("ButtonCompareSelected");
        _showCompareProgressMenuItem.Text = Localizer.Get("ButtonShowCompareProgress");
        _addRelocationMenuItem.Text = Localizer.Get("ButtonAddRelocationStep");
        _removeStepMenuItem.Text = Localizer.Get("ButtonRemoveStep");
        _clearStepsMenuItem.Text = Localizer.Get("ButtonClearSteps");
        _openSettingsMenuItem.Text = Localizer.Get("ButtonSettings");

        _addTargetToolButton.Text = Localizer.Get("ButtonAdd");
        _addTargetToolButton.ToolTipText = Localizer.Get("ToolTipAddTarget");
        _addFilesTargetMenuItem.Text = Localizer.Get("ButtonAddFiles");
        _addFolderTargetMenuItem.Text = Localizer.Get("ButtonAddFolder");
        _removeTargetToolButton.Text = Localizer.Get("ButtonRemoveSelected");
        _removeTargetToolButton.ToolTipText = Localizer.Get("ToolTipRemoveTarget");
        _moveTargetUpToolButton.Text = Localizer.Get("ButtonMoveUp");
        _moveTargetUpToolButton.ToolTipText = Localizer.Get("ToolTipMoveTargetUp");
        _moveTargetDownToolButton.Text = Localizer.Get("ButtonMoveDown");
        _moveTargetDownToolButton.ToolTipText = Localizer.Get("ToolTipMoveTargetDown");
        _mergeSelectedToolButton.Text = Localizer.Get("ButtonMergeSelectedTargets");
        _mergeSelectedToolButton.ToolTipText = Localizer.Get("ToolTipMergeSelectedTargets");
        _clearTargetsToolButton.Text = Localizer.Get("ButtonClear");
        _clearTargetsToolButton.ToolTipText = Localizer.Get("ToolTipClearTargets");

        _addRenameToolButton.Text = Localizer.Get("ButtonAddRenameStep");
        _addRenameToolButton.ToolTipText = Localizer.Get("ToolTipAddRename");
        _addWrapToolButton.Text = Localizer.Get("ButtonAddWrapStep");
        _addWrapToolButton.ToolTipText = Localizer.Get("ToolTipAddWrap");
        _addUnwrapToolButton.Text = Localizer.Get("ButtonAddUnwrapStep");
        _addUnwrapToolButton.ToolTipText = Localizer.Get("ToolTipAddUnwrap");
        _addDefaultUnwrapToolItem.Text = Localizer.Get("MenuUnwrapDefault");
        _addSameNameUnwrapToolItem.Text = ToolModeText.GetDisplayName(FolderStructureOperation.UnwrapSameNameSingleFile);
        _addKeepNameUnwrapToolItem.Text = FormatUnwrapSingleMenuText(FolderUnwrapNameMismatchMode.KeepFileName);
        _addUseFolderNameUnwrapToolItem.Text = FormatUnwrapSingleMenuText(FolderUnwrapNameMismatchMode.UseFolderName);
        _addPrefixFolderNameUnwrapToolItem.Text = FormatUnwrapSingleMenuText(FolderUnwrapNameMismatchMode.PrefixFolderName);
        _addMoveInnerFilesUpToolItem.Text = ToolModeText.GetDisplayName(FolderStructureOperation.MoveInnerFilesUp);
        _addArchiveMergeToolButton.Text = Localizer.Get("ButtonAddArchiveMergeStep");
        _addArchiveMergeToolButton.ToolTipText = Localizer.Get("ToolTipAddArchiveMerge");
        _addArchiveMergeGroupToolItem.Text = Localizer.Get("ContextCommandArchiveMergeGroupByArchiveName");
        _addArchiveMergePreserveToolItem.Text = Localizer.Get("ContextCommandArchiveMergePreserveInternalPaths");
        _compareSelectedToolButton.Text = Localizer.Get("ButtonCompareSelected");
        _compareSelectedToolButton.ToolTipText = Localizer.Get("ToolTipCompareSelected");
        _showCompareProgressToolButton.Text = Localizer.Get("ButtonShowCompareProgress");
        _showCompareProgressToolButton.ToolTipText = Localizer.Get("ToolTipShowCompareProgress");
        _addRelocationToolButton.Text = Localizer.Get("ButtonAddRelocationStep");
        _addRelocationToolButton.ToolTipText = Localizer.Get("ToolTipAddRelocation");
        _removeStepToolButton.Text = Localizer.Get("ButtonRemoveStep");
        _removeStepToolButton.ToolTipText = Localizer.Get("ToolTipRemoveStep");
        _clearStepsToolButton.Text = Localizer.Get("ButtonClearSteps");
        _clearStepsToolButton.ToolTipText = Localizer.Get("ToolTipClearSteps");

        ApplyTargetGridLocalization();
        ApplyPlanGridLocalization();
        _logBox.Text = Localizer.Get("LogReady");
        UpdatePlanScopeHeader(GetSelectedTarget());
        UpdateCommandStates();
    }

    private void ApplyCommandImages()
    {
        _addFilesMenuItem.Image = UiIconFactory.Add;
        _addFolderMenuItem.Image = UiIconFactory.FolderAdd;
        _removeTargetMenuItem.Image = UiIconFactory.Remove;
        _mergeSelectedMenuItem.Image = UiIconFactory.FolderAdd;
        _clearTargetsMenuItem.Image = UiIconFactory.Clear;
        _addRenameMenuItem.Image = UiIconFactory.Rename;
        _addWrapMenuItem.Image = UiIconFactory.Wrap;
        _addDefaultUnwrapMenuItem.Image = UiIconFactory.Unwrap;
        _addSameNameUnwrapMenuItem.Image = UiIconFactory.Unwrap;
        _addKeepNameUnwrapMenuItem.Image = UiIconFactory.Unwrap;
        _addUseFolderNameUnwrapMenuItem.Image = UiIconFactory.Unwrap;
        _addPrefixFolderNameUnwrapMenuItem.Image = UiIconFactory.Unwrap;
        _addMoveInnerFilesUpMenuItem.Image = UiIconFactory.MoveUp;
        _addArchiveMergeGroupMenuItem.Image = UiIconFactory.ArchiveMerge;
        _addArchiveMergePreserveMenuItem.Image = UiIconFactory.ArchiveMerge;
        _compareSelectedMenuItem.Image = UiIconFactory.Compare;
        _showCompareProgressMenuItem.Image = UiIconFactory.Compare;
        _addRelocationMenuItem.Image = UiIconFactory.Relocate;
        _removeStepMenuItem.Image = UiIconFactory.RemoveStep;
        _clearStepsMenuItem.Image = UiIconFactory.Clear;
        _openSettingsMenuItem.Image = UiIconFactory.Settings;

        _addTargetToolButton.Image = UiIconFactory.Add;
        _addFilesTargetMenuItem.Image = UiIconFactory.Add;
        _addFolderTargetMenuItem.Image = UiIconFactory.FolderAdd;
        _removeTargetToolButton.Image = UiIconFactory.Remove;
        _moveTargetUpToolButton.Image = UiIconFactory.MoveUp;
        _moveTargetDownToolButton.Image = UiIconFactory.MoveDown;
        _mergeSelectedToolButton.Image = UiIconFactory.FolderAdd;
        _clearTargetsToolButton.Image = UiIconFactory.Clear;

        _addRenameToolButton.Image = UiIconFactory.Rename;
        _addWrapToolButton.Image = UiIconFactory.Wrap;
        _addUnwrapToolButton.Image = UiIconFactory.Unwrap;
        _addDefaultUnwrapToolItem.Image = UiIconFactory.Unwrap;
        _addSameNameUnwrapToolItem.Image = UiIconFactory.Unwrap;
        _addKeepNameUnwrapToolItem.Image = UiIconFactory.Unwrap;
        _addUseFolderNameUnwrapToolItem.Image = UiIconFactory.Unwrap;
        _addPrefixFolderNameUnwrapToolItem.Image = UiIconFactory.Unwrap;
        _addMoveInnerFilesUpToolItem.Image = UiIconFactory.MoveUp;
        _addArchiveMergeToolButton.Image = UiIconFactory.ArchiveMerge;
        _addArchiveMergeGroupToolItem.Image = UiIconFactory.ArchiveMerge;
        _addArchiveMergePreserveToolItem.Image = UiIconFactory.ArchiveMerge;
        _compareSelectedToolButton.Image = UiIconFactory.Compare;
        _showCompareProgressToolButton.Image = UiIconFactory.Compare;
        _addRelocationToolButton.Image = UiIconFactory.Relocate;
        _removeStepToolButton.Image = UiIconFactory.RemoveStep;
        _clearStepsToolButton.Image = UiIconFactory.Clear;
    }

    private void LoadState()
    {
        _settings = SettingsStore.Load();
        AddPaths(_initialPaths);
        ClearLog();
        AppendLog(Localizer.Get("LogReady"));
        UpdateCommandStates();
        QueueStartupAction();
    }

    private void QueueStartupAction()
    {
        if (_startupAction == MainFormStartupAction.OpenFileCompare)
        {
            BeginInvoke((MethodInvoker)OpenFileCompareDialog);
        }
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

    private void RemoveSelectedTarget()
    {
        var targets = GetSelectedTargets().ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        foreach (var target in targets)
        {
            _targets.Remove(target);
        }

        RefreshTargetGrid();
        RefreshPlanList();
        UpdateCommandStates();
    }

    private void MoveSelectedTargets(int direction)
    {
        var selectedTargets = GetSelectedTargets().ToArray();
        if (selectedTargets.Length == 0 || !CanMoveSelectedTargets(direction))
        {
            return;
        }

        var selectedSet = selectedTargets.ToHashSet();
        var indexes = selectedTargets
            .Select(target => _targets.IndexOf(target))
            .Where(static index => index >= 0)
            .Order()
            .ToArray();

        if (direction < 0)
        {
            foreach (var index in indexes)
            {
                if (index <= 0 || selectedSet.Contains(_targets[index - 1]))
                {
                    continue;
                }

                var target = _targets[index];
                _targets.RemoveAt(index);
                _targets.Insert(index - 1, target);
            }
        }
        else
        {
            foreach (var index in indexes.Reverse())
            {
                if (index >= _targets.Count - 1 || selectedSet.Contains(_targets[index + 1]))
                {
                    continue;
                }

                var target = _targets[index];
                _targets.RemoveAt(index);
                _targets.Insert(index + 1, target);
            }
        }

        RefreshTargetGrid();
        SelectTargetRows(selectedTargets);
        UpdateCommandStates();
    }

    private void MergeSelectedTargets()
    {
        var selectedTargets = GetSelectedTargets()
            .Where(static target => File.Exists(target.Path) || Directory.Exists(target.Path))
            .ToArray();
        if (selectedTargets.Length < 2)
        {
            MessageBox.Show(
                Localizer.Get("FolderMergeNeedsMultipleTargets"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (selectedTargets.Any(static target => target.Steps.Count > 0))
        {
            MessageBox.Show(
                Localizer.Get("FolderMergePlannedStepsMessage"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var targetFolder = FolderMergeOperations.PreviewTargetFolderPath(
            selectedTargets.Select(static target => target.Path),
            _settings);
        if (string.IsNullOrWhiteSpace(targetFolder))
        {
            MessageBox.Show(
                Localizer.Get("PlanPreviewUnavailable"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var confirmation = MessageBox.Show(
            Localizer.Format("FolderMergeConfirmFormat", selectedTargets.Length, targetFolder),
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.OK)
        {
            return;
        }

        var mergeResult = FolderMergeOperations.MergeIntoFolder(
            selectedTargets.Select(static target => target.Path),
            _settings);
        var result = mergeResult.OperationResult;
        foreach (var message in result.Messages)
        {
            AppendLog(message);
        }

        foreach (var error in result.Errors)
        {
            AppendLog(Localizer.Format("LogErrorFormat", error));
        }

        AppendLog(Localizer.Format(
            "FolderMergeCompletedFormat",
            mergeResult.TargetFolderPath ?? "",
            result.AppliedCount,
            result.SkippedCount,
            result.Errors.Count));

        foreach (var target in selectedTargets.Where(static target => !File.Exists(target.Path) && !Directory.Exists(target.Path)))
        {
            _targets.Remove(target);
        }

        if (!string.IsNullOrWhiteSpace(mergeResult.TargetFolderPath) && Directory.Exists(mergeResult.TargetFolderPath))
        {
            AddPaths([mergeResult.TargetFolderPath]);
        }
        else
        {
            RefreshTargetGrid();
            RefreshPlanList();
            UpdateCommandStates();
        }

        if (result.HasErrors)
        {
            MessageBox.Show(
                result.ToUserMessage(Localizer.Get("ButtonMergeSelectedTargets")),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ClearTargets()
    {
        _targets.Clear();
        _targetGrid.Rows.Clear();
        RefreshPlanList();
        UpdateCommandStates();
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _settings = form.Settings;
            SettingsStore.Save(_settings);
            AppendLog(Localizer.Format("SettingsSavedFormat", SettingsStore.SettingsPath));
        }
    }

    private void OpenFileCompareDialog()
    {
        if (_executionCancellation is not null)
        {
            return;
        }

        var initialPaths = GetSelectedTargets()
            .Where(IsExistingTarget)
            .Select(static target => target.Path)
            .ToArray();
        using var dialog = new FileCompareRequestDialog(initialPaths, _settings.FileCompareOptions);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _ = CompareTargetsAsync(dialog.SelectedPaths, dialog.Options);
    }

    private async Task CompareTargetsAsync(IReadOnlyList<string> paths, FileCompareOptions options)
    {
        _fileCompareProgressDialog?.CloseForSessionEnd();
        _fileCompareProgressDialog = null;
        var progressState = new FileCompareProgressState(paths.Count);
        _fileCompareProgressState = progressState;
        _executionCancellation = progressState.Cancellation;
        ClearLog();
        AppendLog(Localizer.Format("FileCompareStartingFormat", paths.Count));
        UpdateCommandStates();
        ShowFileCompareProgressDialog();

        try
        {
            var comparePaths = paths.ToArray();
            var compareOptions = options.Clone();
            var progress = new Progress<FileCompareProgress>(progressState.Report);
            var report = await Task.Run(() => FileCompareOperations.Compare(
                comparePaths,
                compareOptions,
                progress,
                progressState.Cancellation.Token));
            progressState.Complete(report);

            AppendLog(Localizer.Format(
                "FileCompareCompletedFormat",
                report.Targets.Count,
                report.Pairs.Count,
                report.HashCacheHits,
                report.HashCacheMisses));
            using var resultDialog = new FileCompareResultDialog(
                report,
                compareOptions,
                AddCompareResultTargets,
                AddDuplicateDeleteStepsFromCompare);
            resultDialog.ShowDialog(this);
        }
        catch (OperationCanceledException)
        {
            progressState.MarkCancelled();
            AppendLog(Localizer.Get("LogExecutionStopped"));
        }
        catch (Exception ex)
        {
            progressState.MarkFailed(ex);
            AppendLog(Localizer.Format("LogExecutionFailedFormat", ex.Message));
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (ReferenceEquals(_executionCancellation, progressState.Cancellation))
            {
                _executionCancellation = null;
            }

            UpdateCommandStates();
        }
    }

    private void AddCompareResultTargets(IReadOnlyList<string> paths)
    {
        AddPaths(paths);
        AppendLog(Localizer.Format("FileCompareResultTargetsAddedFormat", paths.Count));
    }

    private void AddDuplicateDeleteStepsFromCompare(IReadOnlyList<string> paths)
    {
        AddPaths(paths);
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var pathSet = paths.ToHashSet(comparer);
        var affected = _targets
            .Where(target => pathSet.Contains(target.Path))
            .Where(static target => File.Exists(target.Path))
            .ToArray();
        foreach (var target in affected)
        {
            if (target.Steps.Any(static step => step.Kind == WorkPlanStepKind.DuplicateDelete))
            {
                continue;
            }

            target.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.DuplicateDelete });
        }

        RefreshTargetGridRows();
        RefreshPlanList();
        UpdateCommandStates();
        AppendLog(Localizer.Format("DuplicateDeleteStepsAddedFormat", affected.Length));
    }

    private void ShowFileCompareProgressDialog()
    {
        if (_fileCompareProgressState is null)
        {
            return;
        }

        if (_fileCompareProgressDialog is null || _fileCompareProgressDialog.IsDisposed)
        {
            _fileCompareProgressDialog = new FileCompareProgressDialog(_fileCompareProgressState);
        }

        _fileCompareProgressDialog.Show(this);
        _fileCompareProgressDialog.Activate();
    }

    private void AddStep(WorkPlanStep? step)
    {
        if (step is null)
        {
            return;
        }

        var targets = GetSelectedTargets().ToArray();
        if (targets.Length == 0)
        {
            MessageBox.Show(
                Localizer.Get("NoSelectedTargetMessage"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        foreach (var target in targets)
        {
            target.Steps.Add(ReferenceEquals(target, targets[0]) ? step : step.Clone());
        }

        RefreshTargetGridRows();
        RefreshPlanList();
        var displayedTarget = GetSelectedTarget();
        if (displayedTarget?.Steps.Count > 0)
        {
            SelectPlanRow(displayedTarget.Steps.Count - 1);
        }

        UpdateCommandStates();
    }

    private void AddRenameSteps()
    {
        var targets = GetSelectedTargets().ToArray();
        if (targets.Length == 0)
        {
            MessageBox.Show(
                Localizer.Get("NoSelectedTargetMessage"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var selectedNames = RenameReviewDialog.EditPlanSteps(this, targets.Select(static target => target.Path), _settings);
        if (selectedNames is null)
        {
            return;
        }

        foreach (var target in targets)
        {
            if (!selectedNames.TryGetValue(target.Path, out var fileName))
            {
                continue;
            }

            target.Steps.Add(new WorkPlanStep
            {
                Kind = WorkPlanStepKind.FileNameCorrection,
                ManualRenameFileName = fileName
            });
        }

        RefreshTargetGridRows();
        RefreshPlanList();
        var displayedTarget = GetSelectedTarget();
        if (displayedTarget?.Steps.Count > 0)
        {
            SelectPlanRow(displayedTarget.Steps.Count - 1);
        }

        UpdateCommandStates();
    }

    private static WorkPlanStep CreateWrapStep()
    {
        return new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FolderWrap,
            FolderOperation = FolderStructureOperation.WrapFiles
        };
    }

    private WorkPlanStep CreateDefaultUnwrapStep()
    {
        var operation = _settings.FolderStructureOperation switch
        {
            FolderStructureOperation.UnwrapSameNameSingleFile => FolderStructureOperation.UnwrapSameNameSingleFile,
            FolderStructureOperation.UnwrapSingleFileFolder => FolderStructureOperation.UnwrapSingleFileFolder,
            FolderStructureOperation.MoveInnerFilesUp => FolderStructureOperation.MoveInnerFilesUp,
            _ => FolderStructureOperation.UnwrapSameNameSingleFile
        };
        return CreateUnwrapStep(operation, _settings.FolderUnwrapNameMismatchMode);
    }

    private static WorkPlanStep CreateUnwrapStep(
        FolderStructureOperation operation,
        FolderUnwrapNameMismatchMode mismatchMode)
    {
        return new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FolderUnwrap,
            FolderOperation = operation,
            FolderUnwrapNameMismatchMode = mismatchMode
        };
    }

    private WorkPlanStep? CreateAutoRelocationStep()
    {
        var step = new WorkPlanStep
        {
            Kind = WorkPlanStepKind.AutoRelocation,
            AutoRelocationTemplateId = _settings.AutoRelocationTemplateId
        };
        return EditStep(step) ? step : null;
    }

    private void AddArchiveMergeStep(ArchiveMergeLayout layout)
    {
        var targets = GetSelectedTargets().ToArray();
        if (targets.Length < 2 ||
            !targets.All(static target => ArchiveMergeOperations.IsSupportedArchivePath(target.Path)))
        {
            MessageBox.Show(
                Localizer.Get("ArchiveMergeNeedsMultipleArchives"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (targets.Any(static target => target.Steps.Count > 0))
        {
            MessageBox.Show(
                Localizer.Get("ArchiveMergePlannedStepsMessage"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var options = ArchiveMergeOperations.CreateDefaultOptions(
            targets.Select(static target => target.Path),
            _settings,
            layout);
        if (options is null)
        {
            MessageBox.Show(
                Localizer.Get("ArchiveMergeNeedsMultipleArchives"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var step = new WorkPlanStep
        {
            Kind = WorkPlanStepKind.ArchiveMerge,
            ArchiveMergeOptions = options
        };
        foreach (var target in targets)
        {
            target.Steps.Add(step);
        }

        RefreshTargetGridRows();
        RefreshPlanList();
        SelectPlanRow(GetSelectedTarget()?.Steps.IndexOf(step) ?? -1);
        UpdateCommandStates();
    }

    private void EditSelectedStep()
    {
        var step = GetSelectedStep();
        if (step is null)
        {
            return;
        }

        if (EditStep(step))
        {
            RefreshTargetGridRows();
            RefreshPlanList();
            UpdateCommandStates();
        }
    }

    private bool EditStep(WorkPlanStep step)
    {
        if (step.Kind == WorkPlanStepKind.FileNameCorrection && GetSelectedTarget() is { } target)
        {
            return RenameReviewDialog.EditPlanStep(this, target.Path, step, _settings);
        }

        if (step.Kind == WorkPlanStepKind.FolderWrap)
        {
            return true;
        }

        if (step.Kind == WorkPlanStepKind.DuplicateDelete)
        {
            return false;
        }

        if (step.Kind == WorkPlanStepKind.ArchiveMerge)
        {
            if (step.ArchiveMergeOptions is null)
            {
                return false;
            }

            using var archiveMergeDialog = new ArchiveMergeOptionsDialog(step.ArchiveMergeOptions);
            if (archiveMergeDialog.ShowDialog(this) != DialogResult.OK)
            {
                return false;
            }

            step.ArchiveMergeOptions = archiveMergeDialog.Options;
            return true;
        }

        using var dialog = new PlanStepDialog(step, _settings);
        return dialog.ShowDialog(this) == DialogResult.OK;
    }

    private void RemoveSelectedStep()
    {
        var target = GetSelectedTarget();
        var step = GetSelectedStep();
        if (target is null || step is null)
        {
            return;
        }

        var removedIndex = target.Steps.IndexOf(step);
        RemoveStepFromPlans(target, step);
        RefreshTargetGridRows();
        RefreshPlanList();
        SelectPlanRow(Math.Min(removedIndex, target.Steps.Count - 1));
        UpdateCommandStates();
    }

    private void ClearSelectedTargetSteps()
    {
        var target = GetSelectedTarget();
        if (target is null || target.Steps.Count == 0)
        {
            return;
        }

        var steps = target.Steps.ToArray();
        target.Steps.Clear();
        foreach (var step in steps)
        {
            if (step.Kind == WorkPlanStepKind.ArchiveMerge)
            {
                RemoveSharedArchiveMergeStep(step);
            }
        }

        RefreshTargetGridRows();
        RefreshPlanList();
        UpdateCommandStates();
    }

    private void RunOrStopPlan()
    {
        if (_executionCancellation is not null)
        {
            if (!_executionCancellation.IsCancellationRequested)
            {
                _executionCancellation.Cancel();
                _archiveMergeDecisionPanel.CancelPendingDecisions();
                AppendLog(Localizer.Get("LogStopRequested"));
                UpdateCommandStates();
            }

            return;
        }

        _ = ExecutePlanAsync();
    }

    private async Task ExecutePlanAsync()
    {
        if (_targets.Count == 0)
        {
            MessageBox.Show(
                Localizer.Get("NoTargetsMessage"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (_targets.All(static target => target.Steps.Count == 0))
        {
            MessageBox.Show(
                Localizer.Get("NoPlanStepsMessage"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _executionCancellation = cancellation;
        var targets = _targets.ToArray();
        var stepCount = targets.Sum(static target => target.Steps.Count);
        _archiveMergeDecisionPanel.CancelPendingDecisions();
        UpdateArchiveMergeDecisionPanelVisibility();
        EnsureArchiveMergeDecisionPanelHandle();
        ClearLog();
        AppendLog(Localizer.Format("LogExecutionStartingFormat", targets.Length, stepCount));
        UpdateCommandStates();

        try
        {
            var progress = new Progress<string>(AppendLog);
            var questionSink = new UiArchiveMergeQuestionSink(this, _archiveMergeDecisionPanel, cancellation.Token);
            var result = await Task.Run(() => new WorkPlanExecutor(_settings, questionSink)
                .Run(targets, cancellation.Token, progress));

            AppendLog(Localizer.Format(
                "LogExecutionSummaryFormat",
                result.CandidateCount,
                result.AppliedCount,
                result.SkippedCount,
                result.Errors.Count));
            AppendLog(cancellation.IsCancellationRequested
                ? Localizer.Get("LogExecutionStopped")
                : Localizer.Get("LogExecutionCompleted"));
        }
        catch (Exception ex)
        {
            AppendLog(Localizer.Format("LogExecutionFailedFormat", ex.Message));
            MessageBox.Show(ex.Message, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (ReferenceEquals(_executionCancellation, cancellation))
            {
                _executionCancellation = null;
            }

            cancellation.Dispose();
            _archiveMergeDecisionPanel.CancelPendingDecisions();
            UpdateArchiveMergeDecisionPanelVisibility();
            RefreshTargetGrid();
            RefreshPlanList();
            UpdateCommandStates();
        }
    }

    private void RefreshPlanList()
    {
        var target = GetSelectedTarget();
        _planGrid.Rows.Clear();
        UpdatePlanScopeHeader(target);
        if (target is null)
        {
            return;
        }

        var previews = new WorkPlanPreviewBuilder(_settings).Build(target);
        foreach (var preview in previews)
        {
            var rowIndex = _planGrid.Rows.Add();
            var row = _planGrid.Rows[rowIndex];
            row.Tag = preview;
            row.Cells[PlanOrderColumnName].Value = preview.Number.ToString(CultureInfo.CurrentCulture);
            row.Cells[PlanActionColumnName].Value = CreatePlanActionCellText(preview.Step);
            row.Cells[PlanPreviewColumnName].Value = preview.PreviewText;

            if (preview.HasWarning)
            {
                row.Cells[PlanPreviewColumnName].Style.ForeColor = Color.FromArgb(160, 73, 28);
            }

            foreach (var cell in row.Cells.Cast<DataGridViewCell>())
            {
                cell.ToolTipText = preview.ToolTipText;
            }
        }
    }

    private void UpdatePlanScopeHeader(WorkTargetPlan? displayedTarget)
    {
        if (displayedTarget is null)
        {
            _planScopeLabel.Text = Localizer.Get("PlanScopeNoSelection");
            _planScopeLabel.ForeColor = Color.FromArgb(93, 99, 108);
            return;
        }

        var selectedTargets = GetSelectedTargets().ToArray();
        var selectedCount = selectedTargets.Length;
        var selectedStepCount = selectedTargets.Sum(static target => target.Steps.Count);
        var displayedName = GetTargetName(displayedTarget);
        _planScopeLabel.Text = selectedCount > 1
            ? Localizer.Format("PlanScopeSelectedFormat", displayedName, selectedCount, selectedStepCount)
            : Localizer.Format("PlanScopeSingleFormat", displayedName, displayedTarget.Steps.Count);
        _planScopeLabel.ForeColor = Color.FromArgb(55, 65, 81);
    }

    private WorkTargetPlan? GetSelectedTarget()
    {
        return _targetGrid.CurrentRow?.Tag as WorkTargetPlan;
    }

    private IEnumerable<WorkTargetPlan> GetSelectedTargets()
    {
        return _targetGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(static row => row.Index)
            .Select(static row => row.Tag)
            .OfType<WorkTargetPlan>();
    }

    private WorkPlanStep? GetSelectedStep()
    {
        return _planGrid.CurrentRow?.Tag switch
        {
            WorkPlanStepPreview preview => preview.Step,
            _ => null
        };
    }

    private void FileDrop_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void FileDrop_DragDrop(object? sender, DragEventArgs e)
    {
        if (_executionCancellation is not null)
        {
            return;
        }

        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddPaths(paths);
        }
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        var existing = _targets
            .Select(static target => target.Path)
            .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var selectedTargets = new List<WorkTargetPlan>();

        foreach (var path in paths.Where(static path => File.Exists(path) || Directory.Exists(path)))
        {
            var fullPath = Path.GetFullPath(path);
            var target = _targets.FirstOrDefault(item => string.Equals(
                item.Path,
                fullPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (target is null && existing.Add(fullPath))
            {
                target = new WorkTargetPlan(fullPath);
                _targets.Add(target);
            }

            if (target is not null && !selectedTargets.Contains(target))
            {
                selectedTargets.Add(target);
            }
        }

        if (selectedTargets.Count == 0)
        {
            return;
        }

        RefreshTargetGrid();
        SelectTargetRows(selectedTargets);
        UpdateCommandStates();
    }

    private void ConfigureTargetGrid()
    {
        _targetGrid.AutoGenerateColumns = false;
        _targetGrid.Columns.Clear();
        _targetGrid.Columns.Add(new DataGridViewImageColumn
        {
            Name = TargetIconColumnName,
            HeaderText = "",
            ImageLayout = DataGridViewImageCellLayout.Normal,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Width = 30,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });
        _targetGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = TargetNameColumnName,
            HeaderText = "Name",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 45,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _targetGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = TargetLocationColumnName,
            HeaderText = "Location",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 55,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _targetGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = TargetActionsColumnName,
            HeaderText = "Actions",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Width = 58,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });
        _targetGrid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
        _targetGrid.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
        _targetGrid.RowTemplate.Height = 26;
    }

    private void ApplyTargetGridLocalization()
    {
        _targetGrid.Columns[TargetNameColumnName].HeaderText = Localizer.Get("ColumnTargetName");
        _targetGrid.Columns[TargetLocationColumnName].HeaderText = Localizer.Get("ColumnTargetLocation");
        _targetGrid.Columns[TargetActionsColumnName].HeaderText = Localizer.Get("ColumnTargetActions");
    }

    private void ConfigurePlanGrid()
    {
        _planGrid.AutoGenerateColumns = false;
        _planGrid.Columns.Clear();
        _planGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = PlanOrderColumnName,
            HeaderText = "#",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Width = 44,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });
        _planGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = PlanActionColumnName,
            HeaderText = "Action",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Width = 150
        });
        _planGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = PlanPreviewColumnName,
            HeaderText = "Preview",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _planGrid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
        _planGrid.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
        _planGrid.RowTemplate.Height = 26;
    }

    private void ApplyPlanGridLocalization()
    {
        _planGrid.Columns[PlanOrderColumnName].HeaderText = Localizer.Get("ColumnPlanOrder");
        _planGrid.Columns[PlanActionColumnName].HeaderText = Localizer.Get("ColumnPlanAction");
        _planGrid.Columns[PlanPreviewColumnName].HeaderText = Localizer.Get("ColumnPlanPreview");
    }

    private void RefreshTargetGrid()
    {
        var selectedTargets = GetSelectedTargets().ToArray();
        _updatingTargetGridSelection = true;
        try
        {
            _targetGrid.Rows.Clear();
            foreach (var target in _targets)
            {
                var rowIndex = _targetGrid.Rows.Add();
                var row = _targetGrid.Rows[rowIndex];
                row.Tag = target;
                UpdateTargetGridRow(row, target);
            }
        }
        finally
        {
            _updatingTargetGridSelection = false;
        }

        if (selectedTargets.Length > 0)
        {
            SelectTargetRows(selectedTargets);
        }
    }

    private void RefreshTargetGridRows()
    {
        foreach (var row in _targetGrid.Rows.Cast<DataGridViewRow>())
        {
            if (row.Tag is WorkTargetPlan target)
            {
                UpdateTargetGridRow(row, target);
            }
        }
    }

    private void UpdateTargetGridRow(DataGridViewRow row, WorkTargetPlan target)
    {
        var isFolder = Directory.Exists(target.Path);
        row.Cells[TargetIconColumnName].Value = FileSystemIconProvider.GetSmallIcon(target.Path, isFolder);
        row.Cells[TargetNameColumnName].Value = GetTargetName(target);
        row.Cells[TargetLocationColumnName].Value = GetTargetLocation(target);
        row.Cells[TargetActionsColumnName].Value = target.Steps.Count.ToString(CultureInfo.CurrentCulture);

        row.DefaultCellStyle.BackColor = isFolder
            ? Color.FromArgb(240, 247, 255)
            : SystemColors.Window;
        row.Cells[TargetNameColumnName].Style.ForeColor = isFolder
            ? Color.FromArgb(22, 82, 145)
            : SystemColors.ControlText;
        row.Cells[TargetActionsColumnName].Style.ForeColor = target.Steps.Count > 0
            ? Color.FromArgb(26, 111, 66)
            : SystemColors.GrayText;

        var tooltip = CreateTargetTooltip(target);
        foreach (var cell in row.Cells.Cast<DataGridViewCell>())
        {
            cell.ToolTipText = tooltip;
        }
    }

    private void SelectTargetRows(IReadOnlyCollection<WorkTargetPlan> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }

        _updatingTargetGridSelection = true;
        try
        {
            _targetGrid.ClearSelection();
            DataGridViewRow? currentRow = null;
            foreach (var row in _targetGrid.Rows.Cast<DataGridViewRow>())
            {
                if (row.Tag is WorkTargetPlan target && targets.Contains(target))
                {
                    currentRow ??= row;
                }
            }

            if (currentRow is not null)
            {
                _targetGrid.CurrentCell = currentRow.Cells[TargetNameColumnName];
            }

            foreach (var row in _targetGrid.Rows.Cast<DataGridViewRow>())
            {
                if (row.Tag is WorkTargetPlan target && targets.Contains(target))
                {
                    row.Selected = true;
                }
            }
        }
        finally
        {
            _updatingTargetGridSelection = false;
        }

        RefreshPlanList();
    }

    private void SelectPlanRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _planGrid.Rows.Count)
        {
            _planGrid.ClearSelection();
            return;
        }

        _planGrid.ClearSelection();
        var row = _planGrid.Rows[rowIndex];
        row.Selected = true;
        _planGrid.CurrentCell = row.Cells[PlanActionColumnName];
    }

    private void UpdateCommandStates()
    {
        var isExecuting = _executionCancellation is not null;
        var cancellationPending = _executionCancellation?.IsCancellationRequested == true;
        var selectedTargets = GetSelectedTargets().ToArray();
        var hasSelectedTargets = selectedTargets.Length > 0;
        var selectedTarget = GetSelectedTarget();
        var hasTargets = _targets.Count > 0;
        var anyPlannedSteps = _targets.Any(static target => target.Steps.Count > 0);
        var canModify = !isExecuting;
        var canRename = canModify && hasSelectedTargets && selectedTargets.All(IsExistingTarget);
        var canWrap = canModify && hasSelectedTargets && selectedTargets.All(static target => File.Exists(target.Path));
        var canUnwrap = canModify && hasSelectedTargets && selectedTargets.All(static target => Directory.Exists(target.Path));
        var canRelocate = canModify && hasSelectedTargets && selectedTargets.All(IsExistingTarget);
        var canArchiveMerge = canModify &&
                              selectedTargets.Length >= 2 &&
                              selectedTargets.All(static target => ArchiveMergeOperations.IsSupportedArchivePath(target.Path)) &&
                              selectedTargets.All(static target => target.Steps.Count == 0);
        var canCompare = canModify;
        var canShowCompareProgress = _fileCompareProgressState is not null;
        var canMerge = canModify &&
                       selectedTargets.Length >= 2 &&
                       selectedTargets.All(IsExistingTarget) &&
                       selectedTargets.All(static target => target.Steps.Count == 0);
        var canRemoveStep = canModify && GetSelectedStep() is not null;
        var canClearSteps = canModify && selectedTarget?.Steps.Count > 0;
        var canRun = hasTargets && anyPlannedSteps;

        _addFilesMenuItem.Enabled = canModify;
        _addFolderMenuItem.Enabled = canModify;
        _removeTargetMenuItem.Enabled = canModify && hasSelectedTargets;
        _mergeSelectedMenuItem.Enabled = canMerge;
        _clearTargetsMenuItem.Enabled = canModify && hasTargets;
        _addRenameMenuItem.Enabled = canRename;
        _addWrapMenuItem.Enabled = canWrap;
        SetUnwrapItemsEnabled(canUnwrap);
        SetArchiveMergeItemsEnabled(canArchiveMerge);
        _compareSelectedMenuItem.Enabled = canCompare;
        _showCompareProgressMenuItem.Enabled = canShowCompareProgress;
        _addRelocationMenuItem.Enabled = canRelocate;
        _removeStepMenuItem.Enabled = canRemoveStep;
        _clearStepsMenuItem.Enabled = canClearSteps;
        _runStopMenuItem.Enabled = isExecuting ? !cancellationPending : canRun;
        _openSettingsMenuItem.Enabled = canModify;

        _addTargetToolButton.Enabled = canModify;
        _addFilesTargetMenuItem.Enabled = canModify;
        _addFolderTargetMenuItem.Enabled = canModify;
        _removeTargetToolButton.Enabled = canModify && hasSelectedTargets;
        _moveTargetUpToolButton.Enabled = canModify && CanMoveSelectedTargets(-1);
        _moveTargetDownToolButton.Enabled = canModify && CanMoveSelectedTargets(1);
        _mergeSelectedToolButton.Enabled = canMerge;
        _clearTargetsToolButton.Enabled = canModify && hasTargets;

        _addRenameToolButton.Enabled = canRename;
        _addWrapToolButton.Enabled = canWrap;
        _addUnwrapToolButton.Enabled = canUnwrap;
        _addArchiveMergeToolButton.Enabled = canArchiveMerge;
        _compareSelectedToolButton.Enabled = canCompare;
        _showCompareProgressToolButton.Enabled = canShowCompareProgress;
        _addRelocationToolButton.Enabled = canRelocate;
        _removeStepToolButton.Enabled = canRemoveStep;
        _clearStepsToolButton.Enabled = canClearSteps;
        _runStopButton.Enabled = isExecuting ? !cancellationPending : canRun;

        ApplyRunStopButtonState();
    }

    private void SetUnwrapItemsEnabled(bool enabled)
    {
        _addDefaultUnwrapMenuItem.Enabled = enabled;
        _addSameNameUnwrapMenuItem.Enabled = enabled;
        _addKeepNameUnwrapMenuItem.Enabled = enabled;
        _addUseFolderNameUnwrapMenuItem.Enabled = enabled;
        _addPrefixFolderNameUnwrapMenuItem.Enabled = enabled;
        _addMoveInnerFilesUpMenuItem.Enabled = enabled;
        _addDefaultUnwrapToolItem.Enabled = enabled;
        _addSameNameUnwrapToolItem.Enabled = enabled;
        _addKeepNameUnwrapToolItem.Enabled = enabled;
        _addUseFolderNameUnwrapToolItem.Enabled = enabled;
        _addPrefixFolderNameUnwrapToolItem.Enabled = enabled;
        _addMoveInnerFilesUpToolItem.Enabled = enabled;
    }

    private void SetArchiveMergeItemsEnabled(bool enabled)
    {
        _addArchiveMergeGroupMenuItem.Enabled = enabled;
        _addArchiveMergePreserveMenuItem.Enabled = enabled;
        _addArchiveMergeGroupToolItem.Enabled = enabled;
        _addArchiveMergePreserveToolItem.Enabled = enabled;
    }

    private void ApplyRunStopButtonState()
    {
        var isExecuting = _executionCancellation is not null;
        var text = Localizer.Get(isExecuting ? "ButtonStop" : "ButtonRun");
        var image = isExecuting ? UiIconFactory.Stop : UiIconFactory.Play;
        _runStopButton.Text = text;
        _runStopButton.Image = image;
        _runStopMenuItem.Text = text;
        _runStopMenuItem.Image = image;
    }

    private bool CanMoveSelectedTargets(int direction)
    {
        var selectedTargets = GetSelectedTargets().ToArray();
        if (selectedTargets.Length == 0)
        {
            return false;
        }

        var selectedSet = selectedTargets.ToHashSet();
        foreach (var target in selectedTargets)
        {
            var index = _targets.IndexOf(target);
            if (direction < 0 && index > 0 && !selectedSet.Contains(_targets[index - 1]))
            {
                return true;
            }

            if (direction > 0 && index >= 0 && index < _targets.Count - 1 &&
                !selectedSet.Contains(_targets[index + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearLog()
    {
        _logBox.Clear();
    }

    private void UpdateArchiveMergeDecisionPanelVisibility()
    {
        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)UpdateArchiveMergeDecisionPanelVisibility);
            return;
        }

        var hasPendingDecisions = _archiveMergeDecisionPanel.PendingCount > 0;
        _archiveMergeDecisionPanel.Visible = hasPendingDecisions;
        _executionPanel.Height = hasPendingDecisions
            ? ExecutionPanelDecisionHeight
            : ExecutionPanelDefaultHeight;
    }

    private void EnsureArchiveMergeDecisionPanelHandle()
    {
        if (!_archiveMergeDecisionPanel.IsHandleCreated)
        {
            _ = _archiveMergeDecisionPanel.Handle;
        }
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)(() => AppendLog(message)));
            return;
        }

        if (_logBox.TextLength > 0)
        {
            _logBox.AppendText(Environment.NewLine);
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        _logBox.AppendText($"[{timestamp}] {message}");
    }

    private static string FormatUnwrapSingleMenuText(FolderUnwrapNameMismatchMode mode)
    {
        return Localizer.Format(
            "MenuUnwrapSingleFormat",
            ToolModeText.GetDisplayName(mode));
    }

    private static string CreatePlanActionCellText(WorkPlanStep step)
    {
        var icon = step.Kind switch
        {
            WorkPlanStepKind.FileNameCorrection => "\u270E",
            WorkPlanStepKind.FolderWrap => "\u21B4",
            WorkPlanStepKind.FolderUnwrap => "\u21B1",
            WorkPlanStepKind.AutoRelocation => "\u21C4",
            WorkPlanStepKind.ArchiveMerge => "\u21C6",
            WorkPlanStepKind.DuplicateDelete => "\u232B",
            _ => "\u2022"
        };
        return icon + " " + GetPlanActionName(step);
    }

    private static string GetPlanActionName(WorkPlanStep step)
    {
        return step.Kind switch
        {
            WorkPlanStepKind.FileNameCorrection => Localizer.Get("PlanActionRename"),
            WorkPlanStepKind.FolderWrap => Localizer.Get("PlanActionWrap"),
            WorkPlanStepKind.FolderUnwrap => Localizer.Get("PlanActionUnwrap"),
            WorkPlanStepKind.AutoRelocation => Localizer.Get("PlanActionRelocate"),
            WorkPlanStepKind.ArchiveMerge => Localizer.Get("PlanActionArchiveMerge"),
            WorkPlanStepKind.DuplicateDelete => Localizer.Get("PlanActionDuplicateDelete"),
            _ => step.DisplayName
        };
    }

    private void RemoveStepFromPlans(WorkTargetPlan currentTarget, WorkPlanStep step)
    {
        if (step.Kind == WorkPlanStepKind.ArchiveMerge)
        {
            RemoveSharedArchiveMergeStep(step);
            return;
        }

        currentTarget.Steps.Remove(step);
    }

    private void RemoveSharedArchiveMergeStep(WorkPlanStep step)
    {
        var planId = step.ArchiveMergeOptions?.PlanId;
        if (string.IsNullOrWhiteSpace(planId))
        {
            foreach (var target in _targets)
            {
                target.Steps.Remove(step);
            }

            return;
        }

        foreach (var target in _targets)
        {
            target.Steps.RemoveAll(candidate =>
                candidate.Kind == WorkPlanStepKind.ArchiveMerge &&
                string.Equals(candidate.ArchiveMergeOptions?.PlanId, planId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static bool IsExistingTarget(WorkTargetPlan target)
    {
        return File.Exists(target.Path) || Directory.Exists(target.Path);
    }

    private static string GetTargetName(WorkTargetPlan target)
    {
        var name = Path.GetFileName(target.Path);
        return string.IsNullOrWhiteSpace(name) ? target.Path : name;
    }

    private static string GetTargetLocation(WorkTargetPlan target)
    {
        return Path.GetDirectoryName(target.Path) ?? "";
    }

    private static string CreateTargetTooltip(WorkTargetPlan target)
    {
        if (target.Steps.Count == 0)
        {
            return target.Path;
        }

        var steps = target.Steps
            .Select((step, index) => $"{index + 1}. {step.DisplayName}");
        return target.Path + Environment.NewLine + string.Join(Environment.NewLine, steps);
    }

    private static bool IsDesignerHosted()
    {
        return LicenseManager.UsageMode == LicenseUsageMode.Designtime;
    }

    private sealed class UiArchiveMergeQuestionSink : IArchiveMergeQuestionSink
    {
        private readonly Form _owner;
        private readonly ArchiveMergeDecisionPanel _decisionPanel;
        private readonly CancellationToken _cancellationToken;

        public UiArchiveMergeQuestionSink(
            Form owner,
            ArchiveMergeDecisionPanel decisionPanel,
            CancellationToken cancellationToken)
        {
            _owner = owner;
            _decisionPanel = decisionPanel;
            _cancellationToken = cancellationToken;
        }

        public Encoding? ChooseEncoding(ArchiveEncodingQuestion question)
        {
            if (_owner.IsDisposed)
            {
                return null;
            }

            if (_owner.InvokeRequired)
            {
                return (Encoding?)_owner.Invoke(new Func<Encoding?>(() => ChooseEncoding(question)));
            }

            using var dialog = new ArchiveEncodingDialog(question);
            return dialog.ShowDialog(_owner) == DialogResult.OK ? dialog.SelectedEncoding : null;
        }

        public ArchiveMergeNameCollisionDecision ResolveNameCollision(ArchiveMergeNameCollisionQuestion question)
        {
            if (_owner.IsDisposed)
            {
                return ArchiveMergeNameCollisionDecision.Abort;
            }

            return _decisionPanel.ResolveNameCollision(question, _cancellationToken);
        }

        public ArchiveMergeDuplicateContentDecision ResolveDuplicateContent(ArchiveMergeDuplicateContentQuestion question)
        {
            if (_owner.IsDisposed)
            {
                return ArchiveMergeDuplicateContentDecision.Abort;
            }

            return _decisionPanel.ResolveDuplicateContent(question, _cancellationToken);
        }
    }

}
