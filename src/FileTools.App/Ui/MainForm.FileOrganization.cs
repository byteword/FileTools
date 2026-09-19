using System.Windows.Forms;

namespace FileTools;

public sealed partial class MainForm
{
    private void BindFileOrganizationCommands()
    {
        _emptyFolderMenuItem.Click += (_, _) => AddEmptyFolderCleanupSteps();
        _emptyFolderToolButton.Click += (_, _) => AddEmptyFolderCleanupSteps();
        _batchRenameMenuItem.Click += (_, _) => AddBatchRenameSteps();
        _batchRenameToolButton.Click += (_, _) => AddBatchRenameSteps();
        _fileListMenuItem.Click += (_, _) => OpenFileList();
        _fileListToolButton.Click += (_, _) => OpenFileList();
        _collectFilesMenuItem.Click += (_, _) => OpenFileCollection();
        _collectFilesToolButton.Click += (_, _) => OpenFileCollection();
    }

    private void LocalizeFileOrganizationCommands()
    {
        _emptyFolderMenuItem.Text = _emptyFolderToolButton.Text = Localizer.Get("EmptyFolderTitle");
        _emptyFolderToolButton.ToolTipText = Localizer.Get("EmptyFolderHelp");
        _emptyFolderMenuItem.Image = UiIconFactory.Clear;
        _batchRenameMenuItem.Text = _batchRenameToolButton.Text = Localizer.Get("BatchRenameTitle");
        _batchRenameToolButton.ToolTipText = Localizer.Get("BatchRenameHelp");
        _batchRenameMenuItem.Image = UiIconFactory.Rename;
        _fileListMenuItem.Text = _fileListToolButton.Text = Localizer.Get("FileListTitle");
        _fileListToolButton.ToolTipText = Localizer.Get("FileListTitle");
        _fileListMenuItem.Image = UiIconFactory.GetIcon(UiIconKind.FileList);
        _collectFilesMenuItem.Text = _collectFilesToolButton.Text = Localizer.Get("CollectionTitle");
        _collectFilesToolButton.ToolTipText = Localizer.Get("CollectionTitle");
        _collectFilesMenuItem.Image = UiIconFactory.GetIcon(UiIconKind.CollectFiles);
    }

    private void UpdateFileOrganizationCommands(bool canModify, WorkTargetPlan[] selected)
    {
        _fileListMenuItem.Enabled = _fileListToolButton.Enabled = canModify && selected.Length > 0;
        _collectFilesMenuItem.Enabled = _collectFilesToolButton.Enabled = canModify && selected.Length > 0;
        _emptyFolderMenuItem.Enabled = _emptyFolderToolButton.Enabled = canModify && selected.Length > 0 &&
            selected.All(static target => Directory.Exists(target.Path) && target.Steps.Count == 0);
        _batchRenameMenuItem.Enabled = _batchRenameToolButton.Enabled = canModify && selected.Length > 0 &&
            selected.All(static target => File.Exists(target.Path) && target.Steps.Count == 0);
    }

    private static bool HasBatchRename(WorkTargetPlan target) => target.Steps.Any(static step => step.Kind == WorkPlanStepKind.BatchRename);

    private void OpenFileList()
    {
        var paths = GetSelectedTargets().Select(static target => target.Path).ToArray();
        if (paths.Length == 0) return;
        using var dialog = new FileCatalogDialog(paths, _settings);
        dialog.ShowDialog(this);
    }

    private void OpenFileCollection()
    {
        var paths = GetSelectedTargets().Select(static target => target.Path).ToArray();
        if (paths.Length == 0) return;
        using var dialog = new FileCatalogDialog(paths, _settings, collect: true);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        AddCollectedFiles(dialog.CollectedPaths);
    }

    private void AddCollectedFiles(IReadOnlyList<string> paths)
    {
        var before = _targets.Count;
        AddPaths(paths.Where(FileCatalog.IsRegularFile));
        AppendLog(Localizer.Format("CollectionAdded", _targets.Count - before, paths.Count));
    }

    private void AddBatchRenameSteps()
    {
        var selected = GetSelectedTargets().ToArray();
        if (selected.Length == 0 || selected.Any(static target => !File.Exists(target.Path) || target.Steps.Count > 0)) return;
        using var dialog = new BatchRenameDialog(selected.Select(static target => target.Path));
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Plan is null) return;
        ApplyBatchRenamePlan(dialog.Plan);
    }

    private void EditBatchRenameSteps(WorkPlanStep step)
    {
        if (step.BatchRenamePlan is not { } plan) return;
        var paths = plan.SourcePaths.Where(path => _targets.Any(target =>
            PathComparer.Equals(target.Path, path) && (target.Steps.Count == 0 ||
            target.Steps.All(item => item.BatchRenamePlan?.Id == plan.Id)))).ToArray();
        using var dialog = new BatchRenameDialog(paths, plan.Options, plan.ExcludedPaths.Intersect(paths, PathComparer));
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Plan is null) return;
        foreach (var target in _targets) target.Steps.RemoveAll(item => item.BatchRenamePlan?.Id == plan.Id);
        ApplyBatchRenamePlan(dialog.Plan);
    }

    private void ApplyBatchRenamePlan(BatchRenamePlan plan)
    {
        foreach (var row in plan.Rows.Where(static row => row.Status == BatchRenameStatus.Ready))
        {
            var target = _targets.FirstOrDefault(item => PathComparer.Equals(item.Path, row.OriginalPath));
            if (target is null || target.Steps.Count != 0) continue;
            target.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.BatchRename, BatchRenamePlan = plan, BatchRenameItem = row });
        }
        RefreshTargetGridRows();
        RefreshPlanList();
        UpdateCommandStates();
    }

    private void AddEmptyFolderCleanupSteps()
    {
        var selected = GetSelectedTargets().ToArray();
        if (selected.Length == 0 || selected.Any(static target => !Directory.Exists(target.Path) || target.Steps.Count > 0)) return;
        using var dialog = new EmptyFolderCleanupDialog(selected.Select(static target => target.Path));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        foreach (var plan in dialog.Plans)
        {
            var target = selected.First(item => PathComparer.Equals(FileOperationPathGuard.Normalize(item.Path), plan.RootPath));
            target.Steps.Add(new WorkPlanStep { Kind = WorkPlanStepKind.EmptyFolderCleanup, EmptyFolderCleanupPlan = plan });
        }
        RefreshTargetGridRows();
        RefreshPlanList();
        UpdateCommandStates();
    }

    private bool EditEmptyFolderCleanupStep(WorkPlanStep step)
    {
        if (step.EmptyFolderCleanupPlan is not { } plan) return false;
        using var dialog = new EmptyFolderCleanupDialog(plan.ScanRoots.Count > 0 ? plan.ScanRoots : [plan.RootPath], plan);
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        step.EmptyFolderCleanupPlan = dialog.Plans.Single();
        return true;
    }
}
