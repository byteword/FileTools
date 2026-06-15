using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed class FileCompareProgressDialog : Form
{
    private readonly FileCompareProgressState _state;
    private readonly Label _statusLabel = new();
    private readonly Label _pairLabel = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Button _cancelButton = new();
    private readonly Button _hideButton = new();
    private bool _allowClose;

    public FileCompareProgressDialog(FileCompareProgressState state)
    {
        _state = state;
        Text = Localizer.Get("FileCompareProgressTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(560, 240);
        MinimumSize = new Size(480, 230);

        BuildLayout();
        _state.Changed += StateChanged;
        UpdateFromState();
    }

    public void CloseForSessionEnd()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _state.Changed -= StateChanged;
        base.OnFormClosing(e);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.AutoEllipsis = true;
        root.Controls.Add(_statusLabel, 0, 0);

        _pairLabel.Dock = DockStyle.Fill;
        _pairLabel.TextAlign = ContentAlignment.MiddleLeft;
        _pairLabel.AutoEllipsis = true;
        root.Controls.Add(_pairLabel, 0, 1);

        _progressBar.Dock = DockStyle.Fill;
        root.Controls.Add(_progressBar, 0, 2);

        _cancelButton.Text = Localizer.Get("ButtonCancel");
        _cancelButton.Width = 96;
        _cancelButton.Height = 30;
        _cancelButton.Click += (_, _) => _state.Cancel();
        _hideButton.Text = Localizer.Get("FileCompareProgressHide");
        _hideButton.Width = 96;
        _hideButton.Height = 30;
        _hideButton.Click += (_, _) => Hide();
        var buttons = DialogButtonPanelFactory.CreateRightAligned(_cancelButton, _hideButton);
        root.Controls.Add(buttons, 0, 3);
    }

    private void StateChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)UpdateFromState);
            return;
        }

        UpdateFromState();
    }

    private void UpdateFromState()
    {
        _statusLabel.Text = _state.StatusText;
        _pairLabel.Text = string.IsNullOrWhiteSpace(_state.CurrentLeftPath)
            ? Localizer.Format("FileCompareProgressInputTargetsFormat", _state.InputTargetCount)
            : Localizer.Format(
                "FileCompareProgressCurrentPairFormat",
                Path.GetFileName(_state.CurrentLeftPath),
                Path.GetFileName(_state.CurrentRightPath));

        if (_state.TotalPairs <= 0)
        {
            _progressBar.Style = ProgressBarStyle.Marquee;
            _progressBar.Value = 0;
        }
        else
        {
            _progressBar.Style = ProgressBarStyle.Blocks;
            _progressBar.Maximum = Math.Max(1, _state.TotalPairs);
            _progressBar.Value = Math.Clamp(_state.CompletedPairs, 0, _progressBar.Maximum);
        }

        _cancelButton.Enabled = !_state.IsCompleted &&
                                !_state.IsCancelled &&
                                !_state.HasError &&
                                !_state.Cancellation.IsCancellationRequested;
    }
}
