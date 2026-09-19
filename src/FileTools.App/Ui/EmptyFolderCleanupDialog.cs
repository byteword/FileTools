using System.Windows.Forms;

namespace FileTools;

internal sealed partial class EmptyFolderCleanupDialog : Form
{
    private readonly string[] _roots;
    private HashSet<string>? _initialSelection;
    private EmptyFolderScanResult? _scan;
    private EmptyFolderCleanupOptions _scannedOptions = new();
    private CancellationTokenSource? _scanCancellation;
    private bool _updatingChecks;
    public IReadOnlyList<EmptyFolderCleanupPlan> Plans { get; private set; } = [];

    public EmptyFolderCleanupDialog(IEnumerable<string> roots, EmptyFolderCleanupPlan? previous = null)
    {
        _roots = roots.ToArray();
        _initialSelection = previous?.CandidatePaths.ToHashSet(FileOperationPathGuard.Comparer);
        InitializeComponent();
        Text = Localizer.Get("EmptyFolderTitle");
        _description.Text = Localizer.Get("EmptyFolderHelp");
        _recursive.Text = Localizer.Get("EmptyFolderRecursive");
        _includeRoots.Text = Localizer.Get("EmptyFolderIncludeRoots");
        _scanButton.Text = Localizer.Get("EmptyFolderScan");
        _acceptButton.Text = Localizer.Get("OrganizationAddToPlan");
        _cancelButton.Text = Localizer.Get("ButtonCancel");
        _candidates.Columns[0].Text = Localizer.Get("EmptyFolderPath");
        _candidates.Columns[1].Text = Localizer.Get("EmptyFolderState");
        _recursive.Checked = previous?.Options.IncludeSubfolders ?? true;
        _includeRoots.Checked = previous?.Options.IncludeSelectedRoots ?? false;
        _recursive.CheckedChanged += (_, _) => InvalidateScan();
        _includeRoots.CheckedChanged += (_, _) => InvalidateScan();
        _scanButton.Click += async (_, _) => await ScanAsync();
        Shown += async (_, _) => await ScanAsync();
        FormClosing += (_, _) => _scanCancellation?.Cancel();
        _candidates.ItemChecked += (_, e) => UpdateRelatedChecks(e.Item);
        _acceptButton.Click += (_, _) => AcceptPlans();
    }

    private void InvalidateScan()
    {
        _initialSelection = null;
        _scan = null;
        _acceptButton.Enabled = false;
        _summary.Text = Localizer.Get("EmptyFolderRescan");
    }

    internal async Task ScanAsync()
    {
        if (_scanCancellation is not null) return;
        using var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        _scan = null;
        _scannedOptions = new(_recursive.Checked, _includeRoots.Checked);
        _recursive.Enabled = _includeRoots.Enabled = _scanButton.Enabled = _acceptButton.Enabled = false;
        _summary.Text = Localizer.Get("EmptyFolderScanning");
        _errors.Clear();
        try
        {
            var scan = await Task.Run(() => EmptyFolderCleanupOperations.Scan(_roots, _scannedOptions, cancellation.Token));
            if (IsDisposed || Disposing) return;
            _scan = scan;
            _updatingChecks = true;
            _candidates.BeginUpdate();
            try
            {
                _candidates.Items.Clear();
                foreach (var candidate in scan.Candidates)
                {
                    var item = new ListViewItem(candidate.Path)
                    {
                        Tag = candidate, ToolTipText = candidate.Path,
                        Checked = _initialSelection is null || _initialSelection.Contains(candidate.Path)
                    };
                    item.SubItems.Add(Localizer.Get(candidate.AfterChildren ? "EmptyFolderAfterChildren" : "EmptyFolderEmptyNow"));
                    _candidates.Items.Add(item);
                }
            }
            finally { _candidates.EndUpdate(); _updatingChecks = false; }
            _errors.Text = string.Join(Environment.NewLine, scan.Errors);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!IsDisposed) _errors.Text = ex.Message; }
        finally
        {
            _scanCancellation = null;
            if (!IsDisposed && !Disposing)
            {
                _recursive.Enabled = _includeRoots.Enabled = _scanButton.Enabled = true;
                UpdateSummary();
            }
        }
    }

    private void UpdateRelatedChecks(ListViewItem changed)
    {
        if (_updatingChecks || changed.Tag is not EmptyFolderCandidate candidate) return;
        _updatingChecks = true;
        try
        {
            foreach (ListViewItem item in _candidates.Items)
            {
                if (item.Tag is not EmptyFolderCandidate other || ReferenceEquals(item, changed)) continue;
                if (changed.Checked && FileOperationPathGuard.IsWithin(other.Path, candidate.Path)) item.Checked = true;
                if (!changed.Checked && FileOperationPathGuard.IsWithin(candidate.Path, other.Path)) item.Checked = false;
            }
        }
        finally { _updatingChecks = false; }
        UpdateSummary();
    }

    private IReadOnlyList<EmptyFolderCleanupPlan> BuildPlans() => _scan is null ? [] :
        EmptyFolderCleanupOperations.CreatePlans(_scan, _scannedOptions,
            _candidates.CheckedItems.Cast<ListViewItem>().Select(static item => ((EmptyFolderCandidate)item.Tag!).Path));

    private void UpdateSummary()
    {
        var count = BuildPlans().Sum(static plan => plan.CandidatePaths.Count);
        _summary.Text = Localizer.Format("EmptyFolderSummary", _scan?.Candidates.Count ?? 0, count, _scan?.Errors.Count ?? 0);
        _acceptButton.Enabled = count > 0 && _scanCancellation is null;
    }

    private void AcceptPlans()
    {
        Plans = BuildPlans();
        if (Plans.Count == 0) return;
        DialogResult = DialogResult.OK;
        Close();
    }
}
