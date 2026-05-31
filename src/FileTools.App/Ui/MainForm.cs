using System.ComponentModel;
using System.Windows.Forms;

namespace FileTools;

public sealed partial class MainForm : Form
{
    private readonly string[] _initialPaths;
    private readonly BindingList<WorkTargetPlan> _targets = [];
    private FileToolsSettings _settings = new();

    public MainForm()
        : this(null)
    {
    }

    public MainForm(IEnumerable<string>? initialPaths)
    {
        _initialPaths = initialPaths?.ToArray() ?? [];
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
        _targetList.DataSource = _targets;
        _targetList.SelectedIndexChanged += (_, _) => RefreshPlanList();
        _targetList.DragEnter += FileDrop_DragEnter;
        _targetList.DragDrop += FileDrop_DragDrop;
        _planList.DoubleClick += (_, _) => EditSelectedStep();

        _addFilesButton.Click += (_, _) => AddFiles();
        _addFolderButton.Click += (_, _) => AddFolder();
        _removeTargetButton.Click += (_, _) => RemoveSelectedTarget();
        _clearTargetsButton.Click += (_, _) => ClearTargets();
        _settingsButton.Click += (_, _) => OpenSettings();
        _addRenameButton.Click += (_, _) => AddStep(CreateRenameStep());
        _addWrapButton.Click += (_, _) => AddStep(CreateWrapStep());
        _addUnwrapButton.Click += (_, _) => AddStep(CreateUnwrapStep());
        _addRelocationButton.Click += (_, _) => AddStep(CreateAutoRelocationStep());
        _removeStepButton.Click += (_, _) => RemoveSelectedStep();
        _executePlanButton.Click += (_, _) => ExecutePlan();
    }

    private void ApplyLocalization()
    {
        Text = Localizer.Get("MainFormTitle");
        _targetsGroup.Text = Localizer.Get("GroupDropTargets");
        _planGroup.Text = Localizer.Get("GroupWorkPlan");
        _statusGroup.Text = Localizer.Get("GroupOperationResult");
        _addFilesButton.Text = Localizer.Get("ButtonAddFiles");
        _addFolderButton.Text = Localizer.Get("ButtonAddFolder");
        _removeTargetButton.Text = Localizer.Get("ButtonRemoveSelected");
        _clearTargetsButton.Text = Localizer.Get("ButtonClear");
        _settingsButton.Text = Localizer.Get("ButtonSettings");
        _addRenameButton.Text = Localizer.Get("ButtonAddRenameStep");
        _addWrapButton.Text = Localizer.Get("ButtonAddWrapStep");
        _addUnwrapButton.Text = Localizer.Get("ButtonAddUnwrapStep");
        _addRelocationButton.Text = Localizer.Get("ButtonAddRelocationStep");
        _removeStepButton.Text = Localizer.Get("ButtonRemoveStep");
        _executePlanButton.Text = Localizer.Get("ButtonExecutePlan");
        _statusBox.Text = Localizer.Get("InitialPlanStatus");
    }

    private void LoadState()
    {
        _settings = SettingsStore.Load();
        AddPaths(_initialPaths);
        if (_targets.Count > 0)
        {
            _targetList.SelectedIndex = 0;
        }

        _statusBox.Text = Localizer.Get("InitialPlanStatus");
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
        if (_targetList.SelectedItem is not WorkTargetPlan target)
        {
            return;
        }

        _targets.Remove(target);
        RefreshPlanList();
    }

    private void ClearTargets()
    {
        _targets.Clear();
        RefreshPlanList();
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _settings = form.Settings;
            SettingsStore.Save(_settings);
            _statusBox.Text = Localizer.Format("SettingsSavedFormat", SettingsStore.SettingsPath);
        }
    }

    private void AddStep(WorkPlanStep? step)
    {
        if (step is null)
        {
            return;
        }

        var target = GetSelectedTarget();
        if (target is null)
        {
            MessageBox.Show(
                Localizer.Get("NoSelectedTargetMessage"),
                FileToolsEnvironment.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        target.Steps.Add(step);
        RefreshPlanList();
        _planList.SelectedIndex = target.Steps.Count - 1;
    }

    private WorkPlanStep? CreateRenameStep()
    {
        var step = new WorkPlanStep { Kind = WorkPlanStepKind.FileNameCorrection };
        return EditStep(step) ? step : null;
    }

    private WorkPlanStep? CreateWrapStep()
    {
        var step = new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FolderWrap,
            FolderOperation = FolderStructureOperation.WrapFiles
        };
        return EditStep(step) ? step : null;
    }

    private WorkPlanStep? CreateUnwrapStep()
    {
        var step = new WorkPlanStep
        {
            Kind = WorkPlanStepKind.FolderUnwrap,
            FolderOperation = FolderStructureOperation.UnwrapSameNameSingleFile,
            FolderUnwrapNameMismatchMode = _settings.FolderUnwrapNameMismatchMode
        };
        return EditStep(step) ? step : null;
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

    private void EditSelectedStep()
    {
        if (_planList.SelectedItem is not WorkPlanStep step)
        {
            return;
        }

        if (EditStep(step))
        {
            RefreshPlanList();
        }
    }

    private bool EditStep(WorkPlanStep step)
    {
        using var dialog = new PlanStepDialog(step, _settings);
        return dialog.ShowDialog(this) == DialogResult.OK;
    }

    private void RemoveSelectedStep()
    {
        var target = GetSelectedTarget();
        if (target is null || _planList.SelectedItem is not WorkPlanStep step)
        {
            return;
        }

        target.Steps.Remove(step);
        RefreshPlanList();
    }

    private void ExecutePlan()
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

        var result = new WorkPlanExecutor(_settings).Run(_targets);
        var message = result.ToUserMessage(Localizer.Get("PlanExecutionTitle"));
        _statusBox.Text = message;
        MessageBox.Show(
            message,
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            result.HasErrors ? MessageBoxIcon.Error : MessageBoxIcon.Information);
    }

    private void RefreshPlanList()
    {
        _planList.DataSource = null;
        _planList.DataSource = GetSelectedTarget()?.Steps;
    }

    private WorkTargetPlan? GetSelectedTarget()
    {
        return _targetList.SelectedItem as WorkTargetPlan;
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
        var existing = _targets
            .Select(static target => target.Path)
            .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var path in paths.Where(static path => File.Exists(path) || Directory.Exists(path)))
        {
            var fullPath = Path.GetFullPath(path);
            if (existing.Add(fullPath))
            {
                _targets.Add(new WorkTargetPlan(fullPath));
            }
        }
    }

    private static bool IsDesignerHosted()
    {
        return LicenseManager.UsageMode == LicenseUsageMode.Designtime;
    }
}
