using System.Windows.Forms;

namespace FileTools;

internal sealed partial class FileCatalogDialog
{
    private readonly HashSet<string> _uncheckedPaths = new(FileOperationPathGuard.Comparer);
    public IReadOnlyList<string> CollectedPaths { get; private set; } = [];

    private void BindCollectionOptions()
    {
        foreach (var box in new[] { _extensions, _name, _excludedPaths }) box.TextChanged += (_, _) => InvalidateScan();
        _kinds.ItemCheck += (_, _) => InvalidateScan();
        _nameMode.SelectedIndexChanged += (_, _) => InvalidateScan();
        _ignoreCase.CheckedChanged += (_, _) => InvalidateScan();
        foreach (var picker in new[] { _createdFrom, _createdThrough, _modifiedFrom, _modifiedThrough }) picker.ValueChanged += (_, _) => InvalidateScan();
        foreach (var number in new[] { _minimumSize, _maximumSize }) number.ValueChanged += (_, _) => InvalidateScan();
        _minimumEnabled.CheckedChanged += (_, _) => { _minimumSize.Enabled = _minimumEnabled.Checked; InvalidateScan(); };
        _maximumEnabled.CheckedChanged += (_, _) => { _maximumSize.Enabled = _maximumEnabled.Checked; InvalidateScan(); };
        _excludeFolder.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { ShowNewFolderButton = false };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _excludedPaths.Text = _excludedPaths.Text.TrimEnd() + (_excludedPaths.TextLength == 0 ? "" : Environment.NewLine) + dialog.SelectedPath;
        };
        _selectAll.Click += (_, _) => { _uncheckedPaths.Clear(); _grid.Invalidate(); UpdateCollectionSummary(); };
        _selectNone.Click += (_, _) => { _uncheckedPaths.UnionWith(_rows.Select(static row => row.FullPath)); _grid.Invalidate(); UpdateCollectionSummary(); };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValuePushed += (_, e) =>
        {
            if (e.ColumnIndex != 0 || e.RowIndex >= _rows.Count) return;
            var path = _rows[e.RowIndex].FullPath;
            if (e.Value is true) _uncheckedPaths.Remove(path); else _uncheckedPaths.Add(path);
            UpdateCollectionSummary();
        };
    }

    private FileCollectionQuery ReadQuery() => new()
    {
        Kinds = _kinds.CheckedItems.Cast<string>().ToArray(),
        Extensions = _extensions.Text.Split([';', ',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        Name = _name.Text, NameMode = Selected<FileCollectionNameMode>(_nameMode), IgnoreCase = _ignoreCase.Checked,
        CreatedFrom = Date(_createdFrom), CreatedThrough = Date(_createdThrough), ModifiedFrom = Date(_modifiedFrom), ModifiedThrough = Date(_modifiedThrough),
        MinimumSize = _minimumEnabled.Checked ? checked((long)decimal.Ceiling(_minimumSize.Value * 1048576m)) : null,
        MaximumSize = _maximumEnabled.Checked ? checked((long)decimal.Floor(_maximumSize.Value * 1048576m)) : null
    };

    private static DateOnly? Date(DateTimePicker picker) => picker.Checked ? DateOnly.FromDateTime(picker.Value) : null;

    private string CollectionSignature() => System.Text.Json.JsonSerializer.Serialize(new
    { Query = ReadQuery(), Recursive = _recursive.Checked, Hidden = _hidden.Checked, System = _system.Checked, Excluded = _excludedPaths.Text });

    private void UpdateCollectionSummary()
    {
        if (_result is null) return;
        _summary.Text = Localizer.Format("CollectionSummary", _rows.Count, _result.ScannedFiles,
            _rows.Count(row => !_uncheckedPaths.Contains(row.FullPath)), _result.Errors.Count, _result.Skipped);
        UpdateAcceptState();
    }

    private void AcceptCollection()
    {
        if (_result is null || _operationCancellation is not null) return;
        // 날짜 체크 상태 등 컨트롤 이벤트가 누락돼도 오래된 결과를 넘기지 않는다.
        if (_scanSignature != CollectionSignature()) { InvalidateScan(); return; }
        CollectedPaths = _rows.Where(row => !_uncheckedPaths.Contains(row.FullPath)).Select(static row => row.FullPath).ToArray();
        if (CollectedPaths.Count == 0) return;
        DialogResult = DialogResult.OK;
        Close();
    }
}
