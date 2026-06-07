namespace FileTools;

internal sealed class FileCompareProgressState
{
    public FileCompareProgressState(int inputTargetCount)
    {
        InputTargetCount = inputTargetCount;
        StatusText = Localizer.Get("FileCompareProgressPreparing");
    }

    public event EventHandler? Changed;

    public CancellationTokenSource Cancellation { get; } = new();

    public int InputTargetCount { get; }

    public int CompletedPairs { get; private set; }

    public int TotalPairs { get; private set; }

    public string CurrentLeftPath { get; private set; } = "";

    public string CurrentRightPath { get; private set; } = "";

    public string StatusText { get; private set; }

    public bool IsCompleted { get; private set; }

    public bool IsCancelled { get; private set; }

    public bool HasError { get; private set; }

    public void Report(FileCompareProgress progress)
    {
        CompletedPairs = progress.CompletedPairs;
        TotalPairs = progress.TotalPairs;
        CurrentLeftPath = progress.CurrentLeftPath;
        CurrentRightPath = progress.CurrentRightPath;
        StatusText = Localizer.Format("FileCompareProgressRunningFormat", CompletedPairs, TotalPairs);
        OnChanged();
    }

    public void Complete(FileCompareReport report)
    {
        CompletedPairs = report.Pairs.Count;
        TotalPairs = report.Pairs.Count;
        IsCompleted = true;
        StatusText = Localizer.Format("FileCompareProgressCompletedFormat", report.Pairs.Count);
        OnChanged();
    }

    public void MarkCancelled()
    {
        IsCancelled = true;
        StatusText = Localizer.Get("FileCompareProgressCancelled");
        OnChanged();
    }

    public void MarkFailed(Exception ex)
    {
        HasError = true;
        StatusText = Localizer.Format("FileCompareProgressFailedFormat", ex.Message);
        OnChanged();
    }

    public void Cancel()
    {
        if (!Cancellation.IsCancellationRequested)
        {
            Cancellation.Cancel();
            MarkCancelled();
        }
    }

    private void OnChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
