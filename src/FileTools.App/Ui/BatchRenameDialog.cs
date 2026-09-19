using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class BatchRenameDialog : Form
{
    private readonly string[] _paths;
    private readonly HashSet<string> _excluded;
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 220 };
    private CancellationTokenSource? _previewCancellation;
    private bool _updating;
    public BatchRenamePlan? Plan { get; private set; }

    public BatchRenameDialog(IEnumerable<string> paths, BatchRenameOptions? options = null, IEnumerable<string>? excluded = null)
    {
        _paths = paths.ToArray();
        _excluded = (excluded ?? []).ToHashSet(FileOperationPathGuard.Comparer);
        InitializeComponent();
        Text = Localizer.Get("BatchRenameTitle");
        _help.Text = Localizer.Get("BatchRenameHelp");
        foreach (var field in _fieldLabels) field.Value.Text = Localizer.Get(field.Key);
        _preserveExtension.Text = Localizer.Get("BatchRenamePreserveExtension");
        _ignoreCase.Text = Localizer.Get("BatchRenameIgnoreCase");
        _acceptButton.Text = Localizer.Get("OrganizationAddToPlan");
        _cancelButton.Text = Localizer.Get("ButtonCancel");
        foreach (var (column, key) in new[] { ("Include", "BatchRenameInclude"), ("Before", "BatchRenameBefore"),
                     ("After", "BatchRenameAfter"), ("Number", "BatchRenameNumber"), ("State", "EmptyFolderState"), ("Path", "BatchRenamePath") })
            _previewGrid.Columns[column]!.HeaderText = Localizer.Get(key);
        _orderCombo.DataSource = Enum.GetValues<BatchRenameOrder>()
            .Select(order => new ComboOption<BatchRenameOrder>(Localizer.Get("BatchRenameOrder" + order), order)).ToArray();
        LoadOptions(options ?? new());
        foreach (var box in new[] { _findBox, _replaceBox, _removePrefixBox, _removeSuffixBox, _prefixBox, _suffixBox, _patternBox })
            box.TextChanged += (_, _) => SchedulePreview();
        _preserveExtension.CheckedChanged += (_, _) => SchedulePreview();
        _ignoreCase.CheckedChanged += (_, _) => SchedulePreview();
        _orderCombo.SelectedIndexChanged += (_, _) => SchedulePreview();
        _startNumber.ValueChanged += (_, _) => SchedulePreview();
        _incrementNumber.ValueChanged += (_, _) => SchedulePreview();
        _digitsNumber.ValueChanged += (_, _) => SchedulePreview();
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await RefreshPreviewAsync(); };
        Shown += async (_, _) => await RefreshPreviewAsync();
        FormClosing += (_, _) => { _debounce.Stop(); _previewCancellation?.Cancel(); };
        FormClosed += (_, _) => _debounce.Dispose();
        _previewGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_previewGrid.IsCurrentCellDirty) _previewGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _previewGrid.CellValueChanged += (_, e) =>
        {
            if (_updating || e.RowIndex < 0 || e.ColumnIndex != 0 || _previewGrid.Rows[e.RowIndex].Tag is not BatchRenamePreview row) return;
            if (_previewGrid.Rows[e.RowIndex].Cells[0].Value is true) _excluded.Remove(row.OriginalPath);
            else _excluded.Add(row.OriginalPath);
            SchedulePreview();
        };
        _acceptButton.Click += (_, _) =>
        {
            if (Plan is null || Plan.Rows.Any(static row => row.Status == BatchRenameStatus.Blocked) ||
                !Plan.Rows.Any(static row => row.Status == BatchRenameStatus.Ready) || _previewCancellation is not null || _debounce.Enabled) return;
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    private void LoadOptions(BatchRenameOptions options)
    {
        _findBox.Text = options.Find; _replaceBox.Text = options.ReplaceWith;
        _removePrefixBox.Text = options.RemovePrefix; _removeSuffixBox.Text = options.RemoveSuffix;
        _prefixBox.Text = options.Prefix; _suffixBox.Text = options.Suffix; _patternBox.Text = options.Pattern;
        _preserveExtension.Checked = options.PreserveExtension; _ignoreCase.Checked = options.IgnoreCase;
        _startNumber.Value = Math.Clamp(options.Start, 0, int.MaxValue);
        _incrementNumber.Value = Math.Clamp(options.Increment, 1, int.MaxValue);
        _digitsNumber.Value = Math.Clamp(options.Digits, 1, 10);
        _orderCombo.SelectedIndex = (int)options.Order;
    }

    private BatchRenameOptions ReadOptions() => new()
    {
        Find = _findBox.Text, ReplaceWith = _replaceBox.Text, RemovePrefix = _removePrefixBox.Text,
        RemoveSuffix = _removeSuffixBox.Text, Prefix = _prefixBox.Text, Suffix = _suffixBox.Text,
        Pattern = _patternBox.Text, PreserveExtension = _preserveExtension.Checked, IgnoreCase = _ignoreCase.Checked,
        Start = (int)_startNumber.Value, Increment = (int)_incrementNumber.Value, Digits = (int)_digitsNumber.Value,
        Order = (_orderCombo.SelectedItem as ComboOption<BatchRenameOrder>)?.Value ?? BatchRenameOrder.NaturalName
    };

    private void SchedulePreview()
    {
        _previewCancellation?.Cancel();
        Plan = null;
        _acceptButton.Enabled = false;
        _debounce.Stop();
        _debounce.Start();
    }

    internal async Task RefreshPreviewAsync()
    {
        _previewCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        var options = ReadOptions();
        var excluded = _excluded.ToArray();
        Plan = null;
        _acceptButton.Enabled = false;
        _previewGrid.Enabled = false;
        _summary.Text = Localizer.Get("BatchRenameCalculating");
        try
        {
            var plan = await Task.Run(() => BatchRenamePlanBuilder.Build(_paths, options, excluded, cancellation.Token));
            if (IsDisposed || Disposing || cancellation.IsCancellationRequested) return;
            Plan = plan;
            _updating = true;
            try
            {
                _previewGrid.Rows.Clear();
                foreach (var row in plan.Rows)
                {
                    var index = _previewGrid.Rows.Add(row.Status != BatchRenameStatus.Excluded, Path.GetFileName(row.OriginalPath),
                        Path.GetFileName(row.TargetPath), row.Number?.ToString() ?? "", StatusText(row), Path.GetDirectoryName(row.OriginalPath));
                    var gridRow = _previewGrid.Rows[index];
                    gridRow.Tag = row;
                    foreach (DataGridViewCell cell in gridRow.Cells) cell.ToolTipText = row.OriginalPath + Environment.NewLine + row.Detail;
                    if (row.Status == BatchRenameStatus.Blocked) gridRow.DefaultCellStyle.BackColor = Color.MistyRose;
                    else if (row.Status == BatchRenameStatus.Excluded) gridRow.DefaultCellStyle.ForeColor = SystemColors.GrayText;
                }
            }
            finally { _updating = false; }
            var ready = plan.Rows.Count(static row => row.Status == BatchRenameStatus.Ready);
            var blocked = plan.Rows.Count(static row => row.Status == BatchRenameStatus.Blocked);
            _summary.Text = Localizer.Format("BatchRenameSummary", ready, blocked,
                plan.Rows.Count(static row => row.Status == BatchRenameStatus.Unchanged), excluded.Length);
            _acceptButton.Enabled = ready > 0 && blocked == 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!IsDisposed && !cancellation.IsCancellationRequested) _summary.Text = ex.Message; }
        finally
        {
            if (ReferenceEquals(_previewCancellation, cancellation))
            {
                _previewCancellation = null;
                if (!IsDisposed && !Disposing) _previewGrid.Enabled = true;
            }
        }
    }

    private static string StatusText(BatchRenamePreview row) => row.Status == BatchRenameStatus.Blocked
        ? row.Detail : Localizer.Get("BatchRenameStatus" + row.Status);
}
