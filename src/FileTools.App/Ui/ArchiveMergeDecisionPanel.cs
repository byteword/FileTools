using System.Windows.Forms;

namespace FileTools;

internal sealed class ArchiveMergeDecisionPanel : UserControl
{
    private readonly ListBox _decisionList = new();
    private readonly TextBox _decisionDetailBox = new();
    private readonly Button _decisionPrimaryButton = new();
    private readonly Button _decisionSecondaryButton = new();
    private readonly Button _decisionAbortButton = new();

    public ArchiveMergeDecisionPanel()
    {
        BuildLayout();
    }

    public event EventHandler<ArchiveMergeDecisionAddedEventArgs>? DecisionAdded;

    public event EventHandler? PendingCountChanged;

    public int PendingCount => _decisionList.Items.Count;

    public ArchiveMergeNameCollisionDecision ResolveNameCollision(
        ArchiveMergeNameCollisionQuestion question,
        CancellationToken cancellationToken)
    {
        return RequestDecision(new PendingNameCollisionDecision(question), cancellationToken);
    }

    public ArchiveMergeDuplicateContentDecision ResolveDuplicateContent(
        ArchiveMergeDuplicateContentQuestion question,
        CancellationToken cancellationToken)
    {
        return RequestDecision(new PendingDuplicateContentDecision(question), cancellationToken);
    }

    public void CancelPendingDecisions()
    {
        foreach (var item in _decisionList.Items.Cast<PendingDecision>().ToArray())
        {
            item.Cancel();
        }

        _decisionList.Items.Clear();
        UpdateDecisionDetails();
        PendingCountChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BuildLayout()
    {
        Dock = DockStyle.Fill;

        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("ArchiveMergeDecisionGroup"),
            Padding = new Padding(8)
        };
        Controls.Add(group);

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
    }

    private void ConfigureDecisionButton(Button button, EventHandler handler)
    {
        button.Width = 82;
        button.Height = 28;
        button.Enabled = false;
        button.Click += handler;
    }

    private TDecision RequestDecision<TDecision>(
        PendingDecision<TDecision> decision,
        CancellationToken cancellationToken)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return decision.AbortValue;
        }

        BeginInvoke((MethodInvoker)(() => AddPendingDecision(decision)));
        using var registration = cancellationToken.Register(decision.Cancel);
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
        if (IsDisposed || decision.IsCompleted)
        {
            decision.Cancel();
            return;
        }

        _decisionList.Items.Add(decision);
        _decisionList.SelectedItem = decision;
        DecisionAdded?.Invoke(this, new ArchiveMergeDecisionAddedEventArgs(decision.Title));
        UpdateDecisionDetails();
        PendingCountChanged?.Invoke(this, EventArgs.Empty);
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
        PendingCountChanged?.Invoke(this, EventArgs.Empty);
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

        public abstract bool IsCompleted { get; }

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

        public override bool IsCompleted => _completion.Task.IsCompleted;

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

internal sealed class ArchiveMergeDecisionAddedEventArgs : EventArgs
{
    public ArchiveMergeDecisionAddedEventArgs(string title)
    {
        Title = title;
    }

    public string Title { get; }
}
