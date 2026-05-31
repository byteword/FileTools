using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private SplitContainer _mainSplit = null!;
    private GroupBox _targetsGroup = null!;
    private ListBox _targetList = null!;
    private FlowLayoutPanel _targetButtonPanel = null!;
    private Button _addFilesButton = null!;
    private Button _addFolderButton = null!;
    private Button _removeTargetButton = null!;
    private Button _clearTargetsButton = null!;
    private Panel _rightPanel = null!;
    private FlowLayoutPanel _actionPanel = null!;
    private Button _settingsButton = null!;
    private Button _addRenameButton = null!;
    private Button _addWrapButton = null!;
    private Button _addUnwrapButton = null!;
    private Button _addRelocationButton = null!;
    private Button _removeStepButton = null!;
    private Button _executePlanButton = null!;
    private GroupBox _planGroup = null!;
    private ListBox _planList = null!;
    private GroupBox _statusGroup = null!;
    private TextBox _statusBox = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _mainSplit = new SplitContainer();
        _targetsGroup = new GroupBox();
        _targetList = new ListBox();
        _targetButtonPanel = new FlowLayoutPanel();
        _addFilesButton = new Button();
        _addFolderButton = new Button();
        _removeTargetButton = new Button();
        _clearTargetsButton = new Button();
        _rightPanel = new Panel();
        _actionPanel = new FlowLayoutPanel();
        _settingsButton = new Button();
        _addRenameButton = new Button();
        _addWrapButton = new Button();
        _addUnwrapButton = new Button();
        _addRelocationButton = new Button();
        _removeStepButton = new Button();
        _executePlanButton = new Button();
        _planGroup = new GroupBox();
        _planList = new ListBox();
        _statusGroup = new GroupBox();
        _statusBox = new TextBox();
        ((System.ComponentModel.ISupportInitialize)_mainSplit).BeginInit();
        _mainSplit.Panel1.SuspendLayout();
        _mainSplit.Panel2.SuspendLayout();
        _mainSplit.SuspendLayout();
        _targetsGroup.SuspendLayout();
        _targetButtonPanel.SuspendLayout();
        _rightPanel.SuspendLayout();
        _actionPanel.SuspendLayout();
        _planGroup.SuspendLayout();
        _statusGroup.SuspendLayout();
        SuspendLayout();

        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.FixedPanel = FixedPanel.Panel1;
        _mainSplit.Name = "_mainSplit";
        _mainSplit.Size = new Size(980, 700);
        _mainSplit.SplitterDistance = 410;
        _mainSplit.Panel1.Controls.Add(_targetsGroup);
        _mainSplit.Panel2.Controls.Add(_rightPanel);

        _targetsGroup.Dock = DockStyle.Fill;
        _targetsGroup.Name = "_targetsGroup";
        _targetsGroup.Padding = new Padding(8);
        _targetsGroup.Text = "Targets";
        _targetsGroup.Controls.Add(_targetList);
        _targetsGroup.Controls.Add(_targetButtonPanel);

        _targetList.AllowDrop = true;
        _targetList.Dock = DockStyle.Fill;
        _targetList.HorizontalScrollbar = true;
        _targetList.Name = "_targetList";

        _targetButtonPanel.Dock = DockStyle.Bottom;
        _targetButtonPanel.Height = 44;
        _targetButtonPanel.Name = "_targetButtonPanel";
        _targetButtonPanel.Padding = new Padding(0, 8, 0, 0);
        _targetButtonPanel.WrapContents = false;
        _targetButtonPanel.Controls.Add(_addFilesButton);
        _targetButtonPanel.Controls.Add(_addFolderButton);
        _targetButtonPanel.Controls.Add(_removeTargetButton);
        _targetButtonPanel.Controls.Add(_clearTargetsButton);

        _addFilesButton.Height = 28;
        _addFilesButton.Name = "_addFilesButton";
        _addFilesButton.Text = "Add files";
        _addFilesButton.Width = 82;

        _addFolderButton.Height = 28;
        _addFolderButton.Name = "_addFolderButton";
        _addFolderButton.Text = "Add folder";
        _addFolderButton.Width = 82;

        _removeTargetButton.Height = 28;
        _removeTargetButton.Name = "_removeTargetButton";
        _removeTargetButton.Text = "Remove";
        _removeTargetButton.Width = 92;

        _clearTargetsButton.Height = 28;
        _clearTargetsButton.Name = "_clearTargetsButton";
        _clearTargetsButton.Text = "Clear";
        _clearTargetsButton.Width = 72;

        _rightPanel.Dock = DockStyle.Fill;
        _rightPanel.Name = "_rightPanel";
        _rightPanel.Padding = new Padding(8);
        _rightPanel.Controls.Add(_statusGroup);
        _rightPanel.Controls.Add(_planGroup);
        _rightPanel.Controls.Add(_actionPanel);

        _actionPanel.Dock = DockStyle.Top;
        _actionPanel.Height = 82;
        _actionPanel.Name = "_actionPanel";
        _actionPanel.Padding = new Padding(0, 0, 0, 8);
        _actionPanel.WrapContents = true;
        _actionPanel.Controls.Add(_settingsButton);
        _actionPanel.Controls.Add(_addRenameButton);
        _actionPanel.Controls.Add(_addWrapButton);
        _actionPanel.Controls.Add(_addUnwrapButton);
        _actionPanel.Controls.Add(_addRelocationButton);
        _actionPanel.Controls.Add(_removeStepButton);
        _actionPanel.Controls.Add(_executePlanButton);

        _settingsButton.Height = 30;
        _settingsButton.Name = "_settingsButton";
        _settingsButton.Text = "Settings";
        _settingsButton.Width = 92;

        _addRenameButton.Height = 30;
        _addRenameButton.Name = "_addRenameButton";
        _addRenameButton.Text = "Add rename";
        _addRenameButton.Width = 112;

        _addWrapButton.Height = 30;
        _addWrapButton.Name = "_addWrapButton";
        _addWrapButton.Text = "Add wrap";
        _addWrapButton.Width = 96;

        _addUnwrapButton.Height = 30;
        _addUnwrapButton.Name = "_addUnwrapButton";
        _addUnwrapButton.Text = "Add unwrap";
        _addUnwrapButton.Width = 108;

        _addRelocationButton.Height = 30;
        _addRelocationButton.Name = "_addRelocationButton";
        _addRelocationButton.Text = "Add relocation";
        _addRelocationButton.Width = 126;

        _removeStepButton.Height = 30;
        _removeStepButton.Name = "_removeStepButton";
        _removeStepButton.Text = "Remove step";
        _removeStepButton.Width = 110;

        _executePlanButton.Height = 30;
        _executePlanButton.Name = "_executePlanButton";
        _executePlanButton.Text = "Run plan";
        _executePlanButton.Width = 96;

        _planGroup.Dock = DockStyle.Top;
        _planGroup.Height = 260;
        _planGroup.Name = "_planGroup";
        _planGroup.Padding = new Padding(8);
        _planGroup.Text = "Work plan";
        _planGroup.Controls.Add(_planList);

        _planList.Dock = DockStyle.Fill;
        _planList.HorizontalScrollbar = true;
        _planList.Name = "_planList";

        _statusGroup.Dock = DockStyle.Fill;
        _statusGroup.Name = "_statusGroup";
        _statusGroup.Padding = new Padding(8);
        _statusGroup.Text = "Result";
        _statusGroup.Controls.Add(_statusBox);

        _statusBox.Dock = DockStyle.Fill;
        _statusBox.Multiline = true;
        _statusBox.Name = "_statusBox";
        _statusBox.ReadOnly = true;
        _statusBox.ScrollBars = ScrollBars.Both;
        _statusBox.Text = "Add targets, add actions, then run the plan.";
        _statusBox.WordWrap = false;

        AllowDrop = true;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(980, 700);
        Controls.Add(_mainSplit);
        MinimumSize = new Size(920, 620);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "FileTools tasks";

        _statusGroup.ResumeLayout(false);
        _statusGroup.PerformLayout();
        _planGroup.ResumeLayout(false);
        _actionPanel.ResumeLayout(false);
        _rightPanel.ResumeLayout(false);
        _targetButtonPanel.ResumeLayout(false);
        _targetsGroup.ResumeLayout(false);
        _mainSplit.Panel1.ResumeLayout(false);
        _mainSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_mainSplit).EndInit();
        _mainSplit.ResumeLayout(false);
        ResumeLayout(false);
    }
}
