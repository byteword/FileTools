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
    private readonly ListBox _decisionList = new();
    private readonly TextBox _decisionDetailBox = new();
    private readonly Button _decisionPrimaryButton = new();
    private readonly Button _decisionSecondaryButton = new();
    private readonly Button _decisionAbortButton = new();
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
        return RequestDecision(new PendingNameCollisionDecision(question));
    }

    public ArchiveMergeDuplicateContentDecision ResolveDuplicateContent(ArchiveMergeDuplicateContentQuestion question)
    {
        return RequestDecision(new PendingDuplicateContentDecision(question));
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
            SplitterDistance = 430,
            Panel1MinSize = 260,
            Panel2MinSize = 240
        };

        _logBox.Dock = DockStyle.Fill;
        _logBox.BackColor = SystemColors.Window;
        _logBox.BorderStyle = BorderStyle.FixedSingle;
        _logBox.Font = new Font("Consolas", 9F);
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.WordWrap = true;
        contentSplit.Panel1.Controls.Add(_logBox);
        contentSplit.Panel2.Controls.Add(CreateDecisionPanel());
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
            CancelPendingDecisions();
            AppendLog(Localizer.Get("LogStopRequested"));
            return;
        }

        Close();
    }

    private Control CreateDecisionPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("ArchiveMergeDecisionGroup"),
            Padding = new Padding(8)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        group.Controls.Add(layout);

        _decisionList.Dock = DockStyle.Fill;
        _decisionList.IntegralHeight = false;
        _decisionList.SelectedIndexChanged += (_, _) => UpdateDecisionDetails();
        layout.Controls.Add(_decisionList, 0, 0);

        _decisionDetailBox.Dock = DockStyle.Fill;
        _decisionDetailBox.BackColor = SystemColors.Window;
        _decisionDetailBox.BorderStyle = BorderStyle.FixedSingle;
        _decisionDetailBox.Multiline = true;
        _decisionDetailBox.ReadOnly = true;
        _decisionDetailBox.ScrollBars = ScrollBars.Vertical;
        _decisionDetailBox.Text = Localizer.Get("ArchiveMergeDecisionNone");
        layout.Controls.Add(_decisionDetailBox, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0)
        };
        ConfigureDecisionButton(_decisionPrimaryButton, ResolveSelectedDecisionPrimary);
        ConfigureDecisionButton(_decisionSecondaryButton, ResolveSelectedDecisionSecondary);
        ConfigureDecisionButton(_decisionAbortButton, ResolveSelectedDecisionAbort);
        buttons.Controls.Add(_decisionPrimaryButton);
        buttons.Controls.Add(_decisionSecondaryButton);
        buttons.Controls.Add(_decisionAbortButton);
        layout.Controls.Add(buttons, 0, 2);

        UpdateDecisionDetails();
        return group;
    }

    private void ConfigureDecisionButton(Button button, EventHandler handler)
    {
        button.Width = 82;
        button.Height = 28;
        button.Enabled = false;
        button.Click += handler;
    }

    private TDecision RequestDecision<TDecision>(PendingDecision<TDecision> decision)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return decision.AbortValue;
        }

        BeginInvoke((MethodInvoker)(() => AddPendingDecision(decision)));
        using var registration = _cancellation.Token.Register(() => decision.Cancel());
        try
        {
            return decision.Task.GetAwaiter().GetResult();
        }
        catch
        {
            return decision.AbortValue;
        }
    }

    private void AddPendingDecision(PendingDecision decision)
    {
        if (IsDisposed)
        {
            decision.Cancel();
            return;
        }

        _decisionList.Items.Add(decision);
        _decisionList.SelectedItem = decision;
        _statusLabel.Text = Localizer.Get("ArchiveMergeProgressWaitingForDecision");
        AppendLog(Localizer.Format("ArchiveMergeDecisionAddedFormat", decision.Title));
        UpdateDecisionDetails();
    }

    private void UpdateDecisionDetails()
    {
        if (_decisionList.SelectedItem is not PendingDecision decision)
        {
            _decisionDetailBox.Text = Localizer.Get("ArchiveMergeDecisionNone");
            _decisionPrimaryButton.Enabled = false;
            _decisionSecondaryButton.Enabled = false;
            _decisionAbortButton.Enabled = false;
            _decisionPrimaryButton.Text = "";
            _decisionSecondaryButton.Text = "";
            _decisionAbortButton.Text = "";
            return;
        }

        _decisionDetailBox.Text = decision.Detail;
        _decisionPrimaryButton.Text = decision.PrimaryText;
        _decisionSecondaryButton.Text = decision.SecondaryText;
        _decisionAbortButton.Text = decision.AbortText;
        _decisionPrimaryButton.Enabled = true;
        _decisionSecondaryButton.Enabled = true;
        _decisionAbortButton.Enabled = true;
    }

    private void ResolveSelectedDecisionPrimary(object? sender, EventArgs e)
    {
        ResolveSelectedDecision(static decision => decision.ChoosePrimary());
    }

    private void ResolveSelectedDecisionSecondary(object? sender, EventArgs e)
    {
        ResolveSelectedDecision(static decision => decision.ChooseSecondary());
    }

    private void ResolveSelectedDecisionAbort(object? sender, EventArgs e)
    {
        ResolveSelectedDecision(static decision => decision.ChooseAbort());
    }

    private void ResolveSelectedDecision(Action<PendingDecision> resolve)
    {
        if (_decisionList.SelectedItem is not PendingDecision decision)
        {
            return;
        }

        resolve(decision);
        _decisionList.Items.Remove(decision);
        if (_decisionList.Items.Count > 0)
        {
            _decisionList.SelectedIndex = 0;
        }

        UpdateDecisionDetails();
    }

    private void CancelPendingDecisions()
    {
        foreach (var item in _decisionList.Items.Cast<PendingDecision>().ToArray())
        {
            item.Cancel();
        }

        _decisionList.Items.Clear();
        UpdateDecisionDetails();
    }

    private void AppendLog(string message)
    {
        if (_logBox.TextLength > 0)
        {
            _logBox.AppendText(Environment.NewLine);
        }

        _logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message);
    }

    private abstract class PendingDecision
    {
        protected PendingDecision(string title, string detail, string primaryText, string secondaryText)
        {
            Title = title;
            Detail = detail;
            PrimaryText = primaryText;
            SecondaryText = secondaryText;
        }

        public string Title { get; }

        public string Detail { get; }

        public string PrimaryText { get; }

        public string SecondaryText { get; }

        public string AbortText => Localizer.Get("ArchiveMergeDecisionAbort");

        public abstract void ChoosePrimary();

        public abstract void ChooseSecondary();

        public abstract void ChooseAbort();

        public abstract void Cancel();

        public override string ToString()
        {
            return Title;
        }
    }

    private abstract class PendingDecision<TDecision> : PendingDecision
    {
        private readonly TaskCompletionSource<TDecision> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected PendingDecision(
            string title,
            string detail,
            string primaryText,
            string secondaryText,
            TDecision primaryValue,
            TDecision secondaryValue,
            TDecision abortValue)
            : base(title, detail, primaryText, secondaryText)
        {
            PrimaryValue = primaryValue;
            SecondaryValue = secondaryValue;
            AbortValue = abortValue;
        }

        public Task<TDecision> Task => _completion.Task;

        public TDecision AbortValue { get; }

        private TDecision PrimaryValue { get; }

        private TDecision SecondaryValue { get; }

        public override void ChoosePrimary()
        {
            _completion.TrySetResult(PrimaryValue);
        }

        public override void ChooseSecondary()
        {
            _completion.TrySetResult(SecondaryValue);
        }

        public override void ChooseAbort()
        {
            _completion.TrySetResult(AbortValue);
        }

        public override void Cancel()
        {
            _completion.TrySetResult(AbortValue);
        }
    }

    private sealed class PendingNameCollisionDecision : PendingDecision<ArchiveMergeNameCollisionDecision>
    {
        public PendingNameCollisionDecision(ArchiveMergeNameCollisionQuestion question)
            : base(
                Localizer.Format("ArchiveMergeNameCollisionDecisionTitleFormat", question.TargetPath),
                CreateDetail(question),
                Localizer.Get("ArchiveMergeDecisionAutoNumberCurrent"),
                Localizer.Get("ArchiveMergeDecisionSkipCurrent"),
                ArchiveMergeNameCollisionDecision.AutoNumberCurrent,
                ArchiveMergeNameCollisionDecision.SkipCurrent,
                ArchiveMergeNameCollisionDecision.Abort)
        {
        }

        private static string CreateDetail(ArchiveMergeNameCollisionQuestion question)
        {
            return string.Join(Environment.NewLine, new[]
            {
                Localizer.Format("ArchiveMergeDecisionTargetPathFormat", question.TargetPath),
                "",
                Localizer.Get("ArchiveMergeDecisionExistingHeader"),
                FormatEntry(question.ExistingEntry),
                "",
                Localizer.Get("ArchiveMergeDecisionCurrentHeader"),
                FormatEntry(question.CurrentEntry)
            });
        }
    }

    private sealed class PendingDuplicateContentDecision : PendingDecision<ArchiveMergeDuplicateContentDecision>
    {
        public PendingDuplicateContentDecision(ArchiveMergeDuplicateContentQuestion question)
            : base(
                Localizer.Format("ArchiveMergeDuplicateDecisionTitleFormat", Path.GetFileName(question.CurrentEntry.OriginalPath)),
                CreateDetail(question),
                Localizer.Get("ArchiveMergeDecisionKeepBoth"),
                Localizer.Get("ArchiveMergeDecisionSkipDuplicate"),
                ArchiveMergeDuplicateContentDecision.KeepBoth,
                ArchiveMergeDuplicateContentDecision.SkipCurrent,
                ArchiveMergeDuplicateContentDecision.Abort)
        {
        }

        private static string CreateDetail(ArchiveMergeDuplicateContentQuestion question)
        {
            return string.Join(Environment.NewLine, new[]
            {
                Localizer.Format("ArchiveMergeDecisionHashFormat", question.Hash),
                "",
                Localizer.Get("ArchiveMergeDecisionExistingHeader"),
                FormatEntry(question.FirstEntry),
                "",
                Localizer.Get("ArchiveMergeDecisionCurrentHeader"),
                FormatEntry(question.CurrentEntry)
            });
        }
    }

    private static string FormatEntry(ArchiveMergeQuestionEntry entry)
    {
        return string.Join(Environment.NewLine, new[]
        {
            Localizer.Format("ArchiveMergeDecisionArchiveFormat", entry.SourceArchivePath),
            Localizer.Format("ArchiveMergeDecisionOriginalPathFormat", entry.OriginalPath),
            Localizer.Format("ArchiveMergeDecisionTargetPathFormat", entry.TargetPath),
            Localizer.Format("ArchiveMergeDecisionSizeFormat", entry.Size)
        });
    }
}
