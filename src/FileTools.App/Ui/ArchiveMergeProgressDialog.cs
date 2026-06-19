using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FileTools;

internal sealed class ArchiveMergeProgressDialog : Form, IArchiveMergeQuestionSink
{
    private readonly ArchiveMergeOptions _options;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ProgressBar _progressBar = new();
    private readonly TextBox _logBox = new();
    private readonly Label _statusLabel = new();
    private readonly Button _cancelCloseButton = new();
    private readonly ArchiveMergeDecisionPanel _decisionPanel = new();
    private bool _started;

    public ArchiveMergeProgressDialog(ArchiveMergeOptions options)
    {
        _options = options.Clone();
        Text = Localizer.Get("ArchiveMergeProgressDialogTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(720, 420);
        MinimumSize = new Size(620, 340);

        BuildLayout();
        Shown += (_, _) => StartMergeOnce();
        FormClosing += (_, e) =>
        {
            if (Result is null && !_cancellation.IsCancellationRequested)
            {
                _cancellation.Cancel();
                e.Cancel = true;
                AppendLog(Localizer.Get("LogStopRequested"));
            }
        };
    }

    public OperationResult? Result { get; private set; }

    public static OperationResult? Run(IWin32Window? owner, ArchiveMergeOptions options)
    {
        using var dialog = new ArchiveMergeProgressDialog(options);
        dialog.ShowDialog(owner);
        return dialog.Result;
    }

    public Encoding? ChooseEncoding(ArchiveEncodingQuestion question)
    {
        if (InvokeRequired)
        {
            return (Encoding?)Invoke(new Func<Encoding?>(() => ChooseEncoding(question)));
        }

        using var dialog = new ArchiveEncodingDialog(question);
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedEncoding : null;
    }

    public ArchiveMergeNameCollisionDecision ResolveNameCollision(ArchiveMergeNameCollisionQuestion question)
    {
        return _decisionPanel.ResolveNameCollision(question, _cancellation.Token);
    }

    public ArchiveMergeDuplicateContentDecision ResolveDuplicateContent(ArchiveMergeDuplicateContentQuestion question)
    {
        return _decisionPanel.ResolveDuplicateContent(question, _cancellation.Token);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Text = Localizer.Get("ArchiveMergeProgressPreparing");
        root.Controls.Add(_statusLabel, 0, 0);

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.MarqueeAnimationSpeed = 30;
        root.Controls.Add(_progressBar, 0, 1);

        var contentSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2,
            SplitterWidth = 8,
            Panel1MinSize = 0,
            Panel2MinSize = 0
        };
        contentSplit.SizeChanged += (_, _) => ClampProgressSplitter(contentSplit);

        _logBox.Dock = DockStyle.Fill;
        _logBox.BackColor = SystemColors.Window;
        _logBox.BorderStyle = BorderStyle.FixedSingle;
        _logBox.Font = new Font("Consolas", 9F);
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.WordWrap = true;
        contentSplit.Panel1.Controls.Add(_logBox);
        _decisionPanel.DecisionAdded += (_, e) =>
        {
            _statusLabel.Text = Localizer.Get("ArchiveMergeProgressWaitingForDecision");
            AppendLog(Localizer.Format("ArchiveMergeDecisionAddedFormat", e.Title));
        };
        contentSplit.Panel2.Controls.Add(_decisionPanel);
        root.Controls.Add(contentSplit, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };
        _cancelCloseButton.Text = Localizer.Get("ButtonCancel");
        _cancelCloseButton.Width = 92;
        _cancelCloseButton.Height = 30;
        _cancelCloseButton.Click += (_, _) => CancelOrClose();
        buttons.Controls.Add(_cancelCloseButton);
        root.Controls.Add(buttons, 0, 3);
        CancelButton = _cancelCloseButton;
        Shown += (_, _) => ClampProgressSplitter(contentSplit);
    }

    private static void ClampProgressSplitter(SplitContainer split)
    {
        var availableWidth = split.ClientSize.Width - split.SplitterWidth;
        if (availableWidth <= 2)
        {
            return;
        }

        const int desiredDecisionPanelWidth = 300;
        const int desiredLogPanelMinimum = 260;
        var minimumLogPanelWidth = Math.Min(desiredLogPanelMinimum, Math.Max(1, availableWidth / 2));
        var minimumDecisionPanelWidth = Math.Min(280, Math.Max(1, availableWidth - minimumLogPanelWidth));
        var minimumDistance = minimumLogPanelWidth;
        var maximumDistance = Math.Max(minimumDistance, availableWidth - minimumDecisionPanelWidth);
        var desiredDistance = Math.Clamp(
            availableWidth - desiredDecisionPanelWidth,
            minimumDistance,
            maximumDistance);
        if (split.SplitterDistance != desiredDistance)
        {
            split.SplitterDistance = desiredDistance;
        }
    }

    private void StartMergeOnce()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _ = RunMergeAsync();
    }

    private async Task RunMergeAsync()
    {
        try
        {
            var progress = new Progress<string>(message =>
            {
                _statusLabel.Text = message;
                AppendLog(message);
            });
            AppendLog(Localizer.Format(
                "LogArchiveMergeStartingFormat",
                _options.SourcePaths.Count,
                Path.GetFileName(_options.OutputPath)));
            Result = await Task.Run(() => ArchiveMergeOperations.Merge(_options, _cancellation.Token, progress, this));
            foreach (var error in Result.Errors)
            {
                AppendLog(Localizer.Format("LogErrorFormat", error));
            }

            foreach (var message in Result.Messages)
            {
                AppendLog(message);
            }

            _statusLabel.Text = Result.HasErrors
                ? Localizer.Get("ArchiveMergeProgressCompletedWithErrors")
                : Localizer.Get("ArchiveMergeProgressCompleted");
        }
        catch (Exception ex)
        {
            var result = new OperationResult();
            result.AddError(ex.Message);
            Result = result;
            _statusLabel.Text = Localizer.Format("LogExecutionFailedFormat", ex.Message);
            AppendLog(_statusLabel.Text);
        }
        finally
        {
            _progressBar.MarqueeAnimationSpeed = 0;
            _progressBar.Style = ProgressBarStyle.Blocks;
            _progressBar.Value = 100;
            _cancelCloseButton.Text = Localizer.Get("ButtonClose");
            _cancellation.Dispose();
        }
    }

    private void CancelOrClose()
    {
        if (Result is null)
        {
            _cancellation.Cancel();
            _decisionPanel.CancelPendingDecisions();
            AppendLog(Localizer.Get("LogStopRequested"));
            return;
        }

        Close();
    }

    private void AppendLog(string message)
    {
        if (_logBox.TextLength > 0)
        {
            _logBox.AppendText(Environment.NewLine);
        }

        _logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message);
    }
}
