using System.Windows.Forms;

namespace FileTools;

internal sealed partial class FileCatalogDialog : Form
{
    private readonly string[] _roots;
    private readonly FileToolsSettings _settings;
    private readonly bool _collect;
    private FileCatalogResult? _result;
    private IReadOnlyList<FileCatalogEntry> _rows = [];
    private CancellationTokenSource? _operationCancellation;
    private bool _closeWhenFinished;
    private string? _scanSignature;

    public FileCatalogDialog(IEnumerable<string> roots, FileToolsSettings settings, bool collect = false)
    {
        _roots = roots.ToArray();
        _settings = settings.Clone();
        _collect = collect;
        InitializeComponent();
        foreach (var check in new[] { _recursive, _hidden, _system }) check.CheckedChanged += (_, _) => InvalidateScan();
        _sort.SelectedIndexChanged += (_, _) => DisplayRows();
        _descending.CheckedChanged += (_, _) => DisplayRows();
        if (_collect) BindCollectionOptions();
        else
        {
            _format.SelectedIndexChanged += (_, _) => UpdateExportChoices();
            foreach (var check in _columnChecks.Values) check.CheckedChanged += (_, _) => UpdateAcceptState();
        }
        _refresh.Click += async (_, _) => await ScanAsync();
        _stop.Click += (_, _) => _operationCancellation?.Cancel();
        _close.Click += (_, _) => Close();
        _accept.Click += async (_, _) => { if (_collect) AcceptCollection(); else await SaveAsync(); };
        Shown += async (_, _) => await ScanAsync();
        FormClosing += (_, e) =>
        {
            if (_operationCancellation is null) return;
            e.Cancel = true;
            _closeWhenFinished = true;
            _operationCancellation.Cancel();
        };
        _grid.CellValueNeeded += (_, e) =>
        {
            if (e.RowIndex >= _rows.Count) return;
            var row = _rows[e.RowIndex];
            if (_collect && e.ColumnIndex == 0) e.Value = !_uncheckedPaths.Contains(row.FullPath);
            else if (_grid.Columns[e.ColumnIndex].Tag is FileListColumn column)
                e.Value = column switch
                {
                    FileListColumn.Created => row.CreatedUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    FileListColumn.Modified => row.ModifiedUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    _ => FileListExport.Value(row, column)
                };
        };
        _grid.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _rows.Count) e.ToolTipText = _rows[e.RowIndex].FullPath;
        };
        if (!_collect) UpdateExportChoices();
    }

    private static T Selected<T>(ComboBox combo) where T : struct, Enum => ((ComboOption<T>)combo.SelectedItem!).Value;
    private void UpdateExportChoices()
    {
        _columns.Enabled = Selected<FileListFormat>(_format) == FileListFormat.Csv;
        _textField.Enabled = !_columns.Enabled;
        UpdateAcceptState();
    }

    private void InvalidateScan()
    {
        _result = null;
        _accept.Enabled = false;
        _summary.Text = Localizer.Get("CatalogRescan");
    }

    internal async Task ScanAsync()
    {
        if (_operationCancellation is not null) return;
        using var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        _result = null;
        _rows = [];
        _grid.RowCount = 0;
        _errors.Clear();
        SetBusy(true);
        _summary.Text = Localizer.Get("CatalogScanning");
        try
        {
            var options = new FileCatalogOptions { Recursive = _recursive.Checked, IncludeHidden = _hidden.Checked, IncludeSystem = _system.Checked,
                ExcludedPaths = _collect ? _excludedPaths.Lines.Select(static path => path.Trim()).Where(static path => path.Length > 0).ToArray() : [] };
            var predicate = _collect ? ReadQuery().CreateMatcher() : null;
            _scanSignature = _collect ? CollectionSignature() : null;
            _uncheckedPaths.Clear();
            var progress = new Progress<FileCatalogProgress>(value =>
            {
                if (!IsDisposed && !Disposing && ReferenceEquals(_operationCancellation, cancellation))
                    _summary.Text = Localizer.Format("CatalogScanProgress", value.Files, value.Errors, value.Skipped);
            });
            _result = await Task.Run(() => FileCatalog.Scan(_roots, _settings, options, cancellation.Token, progress, predicate));
            cancellation.Token.ThrowIfCancellationRequested();
            _errors.Text = string.Join(Environment.NewLine, _result.Errors.Take(200).Select(error => error.Path + " | " + error.Message));
            if (_result.Errors.Count > 200) _errors.AppendText(Environment.NewLine + Localizer.Get("CatalogErrorsTruncated"));
            DisplayRows();
        }
        catch (OperationCanceledException) { _result = null; _summary.Text = Localizer.Get("CatalogCancelled"); }
        catch (Exception ex) { _result = null; _summary.Text = ex.Message; }
        finally { _operationCancellation = null; SetBusy(false); if (_closeWhenFinished) Close(); }
    }

    private void DisplayRows()
    {
        if (_result is null) return;
        _rows = FileCatalog.Sort(_result.Entries, Selected<FileCatalogSort>(_sort), _descending.Checked);
        _grid.RowCount = _rows.Count;
        _grid.Invalidate();
        _summary.Text = Localizer.Format("CatalogSummary", _rows.Count, _result.Errors.Count, _result.Skipped);
        if (_collect) UpdateCollectionSummary();
        UpdateAcceptState();
    }

    private void UpdateAcceptState() => _accept.Enabled = _operationCancellation is null && _result is not null &&
        (_collect ? _rows.Any(row => !_uncheckedPaths.Contains(row.FullPath)) :
            Selected<FileListFormat>(_format) == FileListFormat.Text || _columnChecks.Values.Any(static check => check.Checked));

    private void SetBusy(bool value)
    {
        _options.Enabled = _recursive.Enabled = _hidden.Enabled = _system.Enabled = _refresh.Enabled = !value;
        _stop.Enabled = value;
        _grid.Enabled = !value;
        UpdateAcceptState();
    }

    private async Task SaveAsync()
    {
        if (_result is null || _operationCancellation is not null) return;
        var options = new FileListExportOptions { Format = Selected<FileListFormat>(_format), TextField = Selected<FileListTextField>(_textField),
            Columns = _columnChecks.Where(static item => item.Value.Checked).Select(static item => item.Key).ToArray() };
        using var dialog = new SaveFileDialog { Filter = options.Format == FileListFormat.Csv ? "CSV (*.csv)|*.csv" : "TXT (*.txt)|*.txt",
            DefaultExt = options.Format == FileListFormat.Csv ? "csv" : "txt", FileName = "FileTools-list", AddExtension = true, OverwritePrompt = true };
        FileListDestinationReview? reviewed = null;
        dialog.FileOk += (_, e) =>
        {
            try { reviewed = FileListDestinationReview.Capture(dialog.FileName); }
            catch (Exception ex) { e.Cancel = true; MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (reviewed is null) return;
        using var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        SetBusy(true);
        _summary.Text = Localizer.Get("FileListSaving");
        var rows = _rows;
        try
        {
            var saved = await Task.Run(() => FileListExport.Save(rows, dialog.FileName, options,
                overwrite: reviewed.Snapshot is not null, cancellationToken: cancellation.Token, destinationReview: reviewed));
            _summary.Text = Localizer.Format("FileListSaved", saved.Written, saved.ExcludedOutput, dialog.FileName);
            FileToolsEnvironment.Log("FILE-LIST", dialog.FileName + " | " + saved.Written);
        }
        catch (OperationCanceledException) { _summary.Text = Localizer.Get("CatalogCancelled"); }
        catch (Exception ex) { _summary.Text = ex.Message; }
        finally { _operationCancellation = null; SetBusy(false); if (_closeWhenFinished) Close(); }
    }
}
