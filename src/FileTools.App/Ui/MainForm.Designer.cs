using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private MenuStrip _menuStrip = null!;
    private ToolStripMenuItem _fileMenuItem = null!;
    private ToolStripMenuItem _addFilesMenuItem = null!;
    private ToolStripMenuItem _addFolderMenuItem = null!;
    private ToolStripMenuItem _removeTargetMenuItem = null!;
    private ToolStripMenuItem _clearTargetsMenuItem = null!;
    private ToolStripMenuItem _mergeSelectedMenuItem = null!;
    private ToolStripMenuItem _taskMenuItem = null!;
    private ToolStripMenuItem _addRenameMenuItem = null!;
    private ToolStripMenuItem _addWrapMenuItem = null!;
    private ToolStripMenuItem _addDefaultUnwrapMenuItem = null!;
    private ToolStripMenuItem _addSameNameUnwrapMenuItem = null!;
    private ToolStripMenuItem _addKeepNameUnwrapMenuItem = null!;
    private ToolStripMenuItem _addUseFolderNameUnwrapMenuItem = null!;
    private ToolStripMenuItem _addPrefixFolderNameUnwrapMenuItem = null!;
    private ToolStripMenuItem _addMoveInnerFilesUpMenuItem = null!;
    private ToolStripMenuItem _addArchiveMergeGroupMenuItem = null!;
    private ToolStripMenuItem _addArchiveMergePreserveMenuItem = null!;
    private ToolStripMenuItem _compareSelectedMenuItem = null!;
    private ToolStripMenuItem _showCompareProgressMenuItem = null!;
    private ToolStripMenuItem _addRelocationMenuItem = null!;
    private ToolStripMenuItem _removeStepMenuItem = null!;
    private ToolStripMenuItem _clearStepsMenuItem = null!;
    private ToolStripMenuItem _runStopMenuItem = null!;
    private ToolStripMenuItem _settingsMenuItem = null!;
    private ToolStripMenuItem _openSettingsMenuItem = null!;
    private SplitContainer _mainSplit = null!;
    private GroupBox _targetsGroup = null!;
    private DataGridView _targetGrid = null!;
    private ToolStrip _targetToolStrip = null!;
    private ToolStripSplitButton _addTargetToolButton = null!;
    private ToolStripMenuItem _addFilesTargetMenuItem = null!;
    private ToolStripMenuItem _addFolderTargetMenuItem = null!;
    private ToolStripButton _removeTargetToolButton = null!;
    private ToolStripButton _moveTargetUpToolButton = null!;
    private ToolStripButton _moveTargetDownToolButton = null!;
    private ToolStripButton _mergeSelectedToolButton = null!;
    private ToolStripButton _clearTargetsToolButton = null!;
    private Panel _rightPanel = null!;
    private ToolStrip _actionToolStrip = null!;
    private ToolStripButton _addRenameToolButton = null!;
    private ToolStripButton _addWrapToolButton = null!;
    private ToolStripSplitButton _addUnwrapToolButton = null!;
    private ToolStripMenuItem _addDefaultUnwrapToolItem = null!;
    private ToolStripMenuItem _addSameNameUnwrapToolItem = null!;
    private ToolStripMenuItem _addKeepNameUnwrapToolItem = null!;
    private ToolStripMenuItem _addUseFolderNameUnwrapToolItem = null!;
    private ToolStripMenuItem _addPrefixFolderNameUnwrapToolItem = null!;
    private ToolStripMenuItem _addMoveInnerFilesUpToolItem = null!;
    private ToolStripSplitButton _addArchiveMergeToolButton = null!;
    private ToolStripMenuItem _addArchiveMergeGroupToolItem = null!;
    private ToolStripMenuItem _addArchiveMergePreserveToolItem = null!;
    private ToolStripButton _compareSelectedToolButton = null!;
    private ToolStripButton _showCompareProgressToolButton = null!;
    private ToolStripButton _addRelocationToolButton = null!;
    private GroupBox _planGroup = null!;
    private Label _planScopeLabel = null!;
    private DataGridView _planGrid = null!;
    private ToolStrip _planToolStrip = null!;
    private ToolStripButton _editStepToolButton = null!;
    private ToolStripButton _removeStepToolButton = null!;
    private ToolStripButton _clearStepsToolButton = null!;
    private Panel _executionPanel = null!;
    private ArchiveMergeDecisionPanel _archiveMergeDecisionPanel = null!;
    private TextBox _logBox = null!;
    private Button _runStopButton = null!;

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
        _menuStrip = new MenuStrip();
        _fileMenuItem = new ToolStripMenuItem();
        _addFilesMenuItem = new ToolStripMenuItem();
        _addFolderMenuItem = new ToolStripMenuItem();
        _removeTargetMenuItem = new ToolStripMenuItem();
        _clearTargetsMenuItem = new ToolStripMenuItem();
        _mergeSelectedMenuItem = new ToolStripMenuItem();
        _taskMenuItem = new ToolStripMenuItem();
        _addRenameMenuItem = new ToolStripMenuItem();
        _addWrapMenuItem = new ToolStripMenuItem();
        _addDefaultUnwrapMenuItem = new ToolStripMenuItem();
        _addSameNameUnwrapMenuItem = new ToolStripMenuItem();
        _addKeepNameUnwrapMenuItem = new ToolStripMenuItem();
        _addUseFolderNameUnwrapMenuItem = new ToolStripMenuItem();
        _addPrefixFolderNameUnwrapMenuItem = new ToolStripMenuItem();
        _addMoveInnerFilesUpMenuItem = new ToolStripMenuItem();
        _addArchiveMergeGroupMenuItem = new ToolStripMenuItem();
        _addArchiveMergePreserveMenuItem = new ToolStripMenuItem();
        _compareSelectedMenuItem = new ToolStripMenuItem();
        _showCompareProgressMenuItem = new ToolStripMenuItem();
        _addRelocationMenuItem = new ToolStripMenuItem();
        _removeStepMenuItem = new ToolStripMenuItem();
        _clearStepsMenuItem = new ToolStripMenuItem();
        _runStopMenuItem = new ToolStripMenuItem();
        _settingsMenuItem = new ToolStripMenuItem();
        _openSettingsMenuItem = new ToolStripMenuItem();
        _mainSplit = new SplitContainer();
        _targetsGroup = new GroupBox();
        _targetGrid = new DataGridView();
        _targetToolStrip = new ToolStrip();
        _addTargetToolButton = new ToolStripSplitButton();
        _addFilesTargetMenuItem = new ToolStripMenuItem();
        _addFolderTargetMenuItem = new ToolStripMenuItem();
        _removeTargetToolButton = new ToolStripButton();
        _moveTargetUpToolButton = new ToolStripButton();
        _moveTargetDownToolButton = new ToolStripButton();
        _mergeSelectedToolButton = new ToolStripButton();
        _clearTargetsToolButton = new ToolStripButton();
        _rightPanel = new Panel();
        _actionToolStrip = new ToolStrip();
        _addRenameToolButton = new ToolStripButton();
        _addWrapToolButton = new ToolStripButton();
        _addUnwrapToolButton = new ToolStripSplitButton();
        _addDefaultUnwrapToolItem = new ToolStripMenuItem();
        _addSameNameUnwrapToolItem = new ToolStripMenuItem();
        _addKeepNameUnwrapToolItem = new ToolStripMenuItem();
        _addUseFolderNameUnwrapToolItem = new ToolStripMenuItem();
        _addPrefixFolderNameUnwrapToolItem = new ToolStripMenuItem();
        _addMoveInnerFilesUpToolItem = new ToolStripMenuItem();
        _addArchiveMergeToolButton = new ToolStripSplitButton();
        _addArchiveMergeGroupToolItem = new ToolStripMenuItem();
        _addArchiveMergePreserveToolItem = new ToolStripMenuItem();
        _compareSelectedToolButton = new ToolStripButton();
        _showCompareProgressToolButton = new ToolStripButton();
        _addRelocationToolButton = new ToolStripButton();
        _planGroup = new GroupBox();
        _planScopeLabel = new Label();
        _planGrid = new DataGridView();
        _planToolStrip = new ToolStrip();
        _editStepToolButton = new ToolStripButton();
        _removeStepToolButton = new ToolStripButton();
        _clearStepsToolButton = new ToolStripButton();
        _executionPanel = new Panel();
        _archiveMergeDecisionPanel = new ArchiveMergeDecisionPanel();
        _logBox = new TextBox();
        _runStopButton = new Button();
        _menuStrip.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_mainSplit).BeginInit();
        _mainSplit.Panel1.SuspendLayout();
        _mainSplit.Panel2.SuspendLayout();
        _mainSplit.SuspendLayout();
        _targetsGroup.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_targetGrid).BeginInit();
        _targetToolStrip.SuspendLayout();
        _rightPanel.SuspendLayout();
        _actionToolStrip.SuspendLayout();
        _planGroup.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_planGrid).BeginInit();
        _planToolStrip.SuspendLayout();
        _executionPanel.SuspendLayout();
        SuspendLayout();

        _menuStrip.ImageScalingSize = new Size(16, 16);
        _menuStrip.Items.AddRange(new ToolStripItem[]
        {
            _fileMenuItem,
            _taskMenuItem,
            _settingsMenuItem
        });
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(980, 24);

        _fileMenuItem.DropDownItems.AddRange(new ToolStripItem[]
        {
            _addFilesMenuItem,
            _addFolderMenuItem,
            new ToolStripSeparator(),
            _removeTargetMenuItem,
            _mergeSelectedMenuItem,
            _clearTargetsMenuItem
        });
        _fileMenuItem.Name = "_fileMenuItem";
        _fileMenuItem.Text = "File";

        _addFilesMenuItem.Name = "_addFilesMenuItem";
        _addFilesMenuItem.Text = "Add files";

        _addFolderMenuItem.Name = "_addFolderMenuItem";
        _addFolderMenuItem.Text = "Add folder";

        _removeTargetMenuItem.Name = "_removeTargetMenuItem";
        _removeTargetMenuItem.Text = "Remove selected";

        _clearTargetsMenuItem.Name = "_clearTargetsMenuItem";
        _clearTargetsMenuItem.Text = "Clear";

        _mergeSelectedMenuItem.Name = "_mergeSelectedMenuItem";
        _mergeSelectedMenuItem.Text = "Merge selected into folder";

        _taskMenuItem.DropDownItems.AddRange(new ToolStripItem[]
        {
            _addRenameMenuItem,
            _addWrapMenuItem,
            _addDefaultUnwrapMenuItem,
            _addSameNameUnwrapMenuItem,
            _addKeepNameUnwrapMenuItem,
            _addUseFolderNameUnwrapMenuItem,
            _addPrefixFolderNameUnwrapMenuItem,
            _addMoveInnerFilesUpMenuItem,
            new ToolStripSeparator(),
            _addArchiveMergeGroupMenuItem,
            _addArchiveMergePreserveMenuItem,
            new ToolStripSeparator(),
            _compareSelectedMenuItem,
            _showCompareProgressMenuItem,
            new ToolStripSeparator(),
            _addRelocationMenuItem,
            new ToolStripSeparator(),
            _removeStepMenuItem,
            _clearStepsMenuItem,
            _runStopMenuItem
        });
        _taskMenuItem.Name = "_taskMenuItem";
        _taskMenuItem.Text = "Tasks";

        _addRenameMenuItem.Name = "_addRenameMenuItem";
        _addRenameMenuItem.Text = "Add rename";

        _addWrapMenuItem.Name = "_addWrapMenuItem";
        _addWrapMenuItem.Text = "Add wrap";

        _addDefaultUnwrapMenuItem.Name = "_addDefaultUnwrapMenuItem";
        _addDefaultUnwrapMenuItem.Text = "Add unwrap";

        _addSameNameUnwrapMenuItem.Name = "_addSameNameUnwrapMenuItem";
        _addSameNameUnwrapMenuItem.Text = "Unwrap same-name single-file folders";

        _addKeepNameUnwrapMenuItem.Name = "_addKeepNameUnwrapMenuItem";
        _addKeepNameUnwrapMenuItem.Text = "Unwrap single-file folders - keep file name";

        _addUseFolderNameUnwrapMenuItem.Name = "_addUseFolderNameUnwrapMenuItem";
        _addUseFolderNameUnwrapMenuItem.Text = "Unwrap single-file folders - use folder name";

        _addPrefixFolderNameUnwrapMenuItem.Name = "_addPrefixFolderNameUnwrapMenuItem";
        _addPrefixFolderNameUnwrapMenuItem.Text = "Unwrap single-file folders - folder-file name";

        _addMoveInnerFilesUpMenuItem.Name = "_addMoveInnerFilesUpMenuItem";
        _addMoveInnerFilesUpMenuItem.Text = "Move inner files up";

        _addArchiveMergeGroupMenuItem.Name = "_addArchiveMergeGroupMenuItem";
        _addArchiveMergeGroupMenuItem.Text = "Merge ZIPs by archive name";

        _addArchiveMergePreserveMenuItem.Name = "_addArchiveMergePreserveMenuItem";
        _addArchiveMergePreserveMenuItem.Text = "Merge ZIPs preserving paths";

        _compareSelectedMenuItem.Name = "_compareSelectedMenuItem";
        _compareSelectedMenuItem.Text = "Compare selected";

        _showCompareProgressMenuItem.Name = "_showCompareProgressMenuItem";
        _showCompareProgressMenuItem.Text = "Show compare progress";

        _addRelocationMenuItem.Name = "_addRelocationMenuItem";
        _addRelocationMenuItem.Text = "Add relocation";

        _removeStepMenuItem.Name = "_removeStepMenuItem";
        _removeStepMenuItem.Text = "Remove step";

        _clearStepsMenuItem.Name = "_clearStepsMenuItem";
        _clearStepsMenuItem.Text = "Clear steps";

        _runStopMenuItem.Name = "_runStopMenuItem";
        _runStopMenuItem.Text = "Run plan";

        _settingsMenuItem.DropDownItems.AddRange(new ToolStripItem[]
        {
            _openSettingsMenuItem
        });
        _settingsMenuItem.Name = "_settingsMenuItem";
        _settingsMenuItem.Text = "Settings";

        _openSettingsMenuItem.Name = "_openSettingsMenuItem";
        _openSettingsMenuItem.Text = "Settings";

        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.FixedPanel = FixedPanel.Panel1;
        _mainSplit.Name = "_mainSplit";
        _mainSplit.Panel1.Controls.Add(_targetsGroup);
        _mainSplit.Panel2.Controls.Add(_rightPanel);
        _mainSplit.Size = new Size(980, 676);
        _mainSplit.SplitterDistance = 410;

        _targetsGroup.Dock = DockStyle.Fill;
        _targetsGroup.Name = "_targetsGroup";
        _targetsGroup.Padding = new Padding(8);
        _targetsGroup.Text = "Targets";
        _targetsGroup.Controls.Add(_targetGrid);
        _targetsGroup.Controls.Add(_targetToolStrip);

        _targetGrid.AllowDrop = true;
        _targetGrid.AllowUserToAddRows = false;
        _targetGrid.AllowUserToDeleteRows = false;
        _targetGrid.AllowUserToResizeRows = false;
        _targetGrid.BackgroundColor = SystemColors.Window;
        _targetGrid.BorderStyle = BorderStyle.FixedSingle;
        _targetGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _targetGrid.Dock = DockStyle.Fill;
        _targetGrid.MultiSelect = true;
        _targetGrid.Name = "_targetGrid";
        _targetGrid.ReadOnly = true;
        _targetGrid.RowHeadersVisible = false;
        _targetGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _targetGrid.ShowCellToolTips = true;

        _targetToolStrip.Dock = DockStyle.Bottom;
        _targetToolStrip.GripStyle = ToolStripGripStyle.Hidden;
        _targetToolStrip.ImageScalingSize = new Size(18, 18);
        _targetToolStrip.Items.AddRange(new ToolStripItem[]
        {
            _addTargetToolButton,
            _removeTargetToolButton,
            new ToolStripSeparator(),
            _moveTargetUpToolButton,
            _moveTargetDownToolButton,
            new ToolStripSeparator(),
            _mergeSelectedToolButton,
            _clearTargetsToolButton
        });
        _targetToolStrip.Name = "_targetToolStrip";
        _targetToolStrip.Padding = new Padding(2, 2, 2, 2);
        _targetToolStrip.Size = new Size(394, 29);

        _addTargetToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _addTargetToolButton.DropDownItems.AddRange(new ToolStripItem[]
        {
            _addFilesTargetMenuItem,
            _addFolderTargetMenuItem
        });
        _addTargetToolButton.ImageTransparentColor = Color.Magenta;
        _addTargetToolButton.Name = "_addTargetToolButton";
        _addTargetToolButton.Text = "Add";

        _addFilesTargetMenuItem.Name = "_addFilesTargetMenuItem";
        _addFilesTargetMenuItem.Text = "Add files";

        _addFolderTargetMenuItem.Name = "_addFolderTargetMenuItem";
        _addFolderTargetMenuItem.Text = "Add folder";

        _removeTargetToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _removeTargetToolButton.ImageTransparentColor = Color.Magenta;
        _removeTargetToolButton.Name = "_removeTargetToolButton";
        _removeTargetToolButton.Text = "Remove";

        _moveTargetUpToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _moveTargetUpToolButton.ImageTransparentColor = Color.Magenta;
        _moveTargetUpToolButton.Name = "_moveTargetUpToolButton";
        _moveTargetUpToolButton.Text = "Up";

        _moveTargetDownToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _moveTargetDownToolButton.ImageTransparentColor = Color.Magenta;
        _moveTargetDownToolButton.Name = "_moveTargetDownToolButton";
        _moveTargetDownToolButton.Text = "Down";

        _mergeSelectedToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _mergeSelectedToolButton.ImageTransparentColor = Color.Magenta;
        _mergeSelectedToolButton.Name = "_mergeSelectedToolButton";
        _mergeSelectedToolButton.Text = "Merge";

        _clearTargetsToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _clearTargetsToolButton.ImageTransparentColor = Color.Magenta;
        _clearTargetsToolButton.Name = "_clearTargetsToolButton";
        _clearTargetsToolButton.Text = "Clear";

        _rightPanel.Dock = DockStyle.Fill;
        _rightPanel.Name = "_rightPanel";
        _rightPanel.Padding = new Padding(8);
        _rightPanel.Controls.Add(_planGroup);
        _rightPanel.Controls.Add(_executionPanel);
        _rightPanel.Controls.Add(_actionToolStrip);

        _actionToolStrip.Dock = DockStyle.Top;
        _actionToolStrip.GripStyle = ToolStripGripStyle.Hidden;
        _actionToolStrip.ImageScalingSize = new Size(20, 20);
        _actionToolStrip.Items.AddRange(new ToolStripItem[]
        {
            _addRenameToolButton,
            _addWrapToolButton,
            _addUnwrapToolButton,
            _addArchiveMergeToolButton,
            _compareSelectedToolButton,
            _showCompareProgressToolButton,
            _addRelocationToolButton
        });
        _actionToolStrip.Name = "_actionToolStrip";
        _actionToolStrip.Padding = new Padding(2, 2, 2, 2);
        _actionToolStrip.Size = new Size(554, 31);

        _addRenameToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _addRenameToolButton.ImageTransparentColor = Color.Magenta;
        _addRenameToolButton.Name = "_addRenameToolButton";
        _addRenameToolButton.Text = "Add rename";

        _addWrapToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _addWrapToolButton.ImageTransparentColor = Color.Magenta;
        _addWrapToolButton.Name = "_addWrapToolButton";
        _addWrapToolButton.Text = "Add wrap";

        _addUnwrapToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _addUnwrapToolButton.DropDownItems.AddRange(new ToolStripItem[]
        {
            _addDefaultUnwrapToolItem,
            _addSameNameUnwrapToolItem,
            _addKeepNameUnwrapToolItem,
            _addUseFolderNameUnwrapToolItem,
            _addPrefixFolderNameUnwrapToolItem,
            _addMoveInnerFilesUpToolItem
        });
        _addUnwrapToolButton.ImageTransparentColor = Color.Magenta;
        _addUnwrapToolButton.Name = "_addUnwrapToolButton";
        _addUnwrapToolButton.Text = "Add unwrap";

        _addDefaultUnwrapToolItem.Name = "_addDefaultUnwrapToolItem";
        _addDefaultUnwrapToolItem.Text = "Default unwrap";

        _addSameNameUnwrapToolItem.Name = "_addSameNameUnwrapToolItem";
        _addSameNameUnwrapToolItem.Text = "Unwrap same-name single-file folders";

        _addKeepNameUnwrapToolItem.Name = "_addKeepNameUnwrapToolItem";
        _addKeepNameUnwrapToolItem.Text = "Unwrap single-file folders - keep file name";

        _addUseFolderNameUnwrapToolItem.Name = "_addUseFolderNameUnwrapToolItem";
        _addUseFolderNameUnwrapToolItem.Text = "Unwrap single-file folders - use folder name";

        _addPrefixFolderNameUnwrapToolItem.Name = "_addPrefixFolderNameUnwrapToolItem";
        _addPrefixFolderNameUnwrapToolItem.Text = "Unwrap single-file folders - folder-file name";

        _addMoveInnerFilesUpToolItem.Name = "_addMoveInnerFilesUpToolItem";
        _addMoveInnerFilesUpToolItem.Text = "Move inner files up";

        _addArchiveMergeToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _addArchiveMergeToolButton.DropDownItems.AddRange(new ToolStripItem[]
        {
            _addArchiveMergeGroupToolItem,
            _addArchiveMergePreserveToolItem
        });
        _addArchiveMergeToolButton.ImageTransparentColor = Color.Magenta;
        _addArchiveMergeToolButton.Name = "_addArchiveMergeToolButton";
        _addArchiveMergeToolButton.Text = "Add archive merge";

        _addArchiveMergeGroupToolItem.Name = "_addArchiveMergeGroupToolItem";
        _addArchiveMergeGroupToolItem.Text = "Merge ZIPs by archive name";

        _addArchiveMergePreserveToolItem.Name = "_addArchiveMergePreserveToolItem";
        _addArchiveMergePreserveToolItem.Text = "Merge ZIPs preserving paths";

        _compareSelectedToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _compareSelectedToolButton.ImageTransparentColor = Color.Magenta;
        _compareSelectedToolButton.Name = "_compareSelectedToolButton";
        _compareSelectedToolButton.Text = "Compare selected";

        _showCompareProgressToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _showCompareProgressToolButton.ImageTransparentColor = Color.Magenta;
        _showCompareProgressToolButton.Name = "_showCompareProgressToolButton";
        _showCompareProgressToolButton.Text = "Show progress";

        _addRelocationToolButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
        _addRelocationToolButton.ImageTransparentColor = Color.Magenta;
        _addRelocationToolButton.Name = "_addRelocationToolButton";
        _addRelocationToolButton.Text = "Add relocation";

        _planGroup.Dock = DockStyle.Fill;
        _planGroup.Name = "_planGroup";
        _planGroup.Padding = new Padding(8);
        _planGroup.Text = "Work plan";
        _planGroup.Controls.Add(_planGrid);
        _planGroup.Controls.Add(_planToolStrip);
        _planGroup.Controls.Add(_planScopeLabel);

        _planScopeLabel.Dock = DockStyle.Top;
        _planScopeLabel.Height = 28;
        _planScopeLabel.Name = "_planScopeLabel";
        _planScopeLabel.Padding = new Padding(2, 0, 0, 0);
        _planScopeLabel.Text = "No target selected.";
        _planScopeLabel.TextAlign = ContentAlignment.MiddleLeft;

        _planGrid.AllowUserToAddRows = false;
        _planGrid.AllowUserToDeleteRows = false;
        _planGrid.AllowUserToResizeRows = false;
        _planGrid.BackgroundColor = SystemColors.Window;
        _planGrid.BorderStyle = BorderStyle.FixedSingle;
        _planGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _planGrid.Dock = DockStyle.Fill;
        _planGrid.MultiSelect = false;
        _planGrid.Name = "_planGrid";
        _planGrid.ReadOnly = true;
        _planGrid.RowHeadersVisible = false;
        _planGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _planGrid.ShowCellToolTips = true;

        _planToolStrip.Dock = DockStyle.Top;
        _planToolStrip.GripStyle = ToolStripGripStyle.Hidden;
        _planToolStrip.ImageScalingSize = new Size(18, 18);
        _planToolStrip.Items.AddRange(new ToolStripItem[]
        {
            _editStepToolButton,
            _removeStepToolButton,
            _clearStepsToolButton
        });
        _planToolStrip.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        _planToolStrip.Name = "_planToolStrip";
        _planToolStrip.Padding = new Padding(2, 2, 2, 2);
        _planToolStrip.Size = new Size(554, 29);

        _editStepToolButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
        _editStepToolButton.ImageTransparentColor = Color.Magenta;
        _editStepToolButton.Name = "_editStepToolButton";
        _editStepToolButton.Text = "Edit step";
        _editStepToolButton.TextImageRelation = TextImageRelation.ImageBeforeText;

        _removeStepToolButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
        _removeStepToolButton.ImageTransparentColor = Color.Magenta;
        _removeStepToolButton.Name = "_removeStepToolButton";
        _removeStepToolButton.Text = "Remove step";
        _removeStepToolButton.TextImageRelation = TextImageRelation.ImageBeforeText;

        _clearStepsToolButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
        _clearStepsToolButton.ImageTransparentColor = Color.Magenta;
        _clearStepsToolButton.Name = "_clearStepsToolButton";
        _clearStepsToolButton.Text = "Clear steps";
        _clearStepsToolButton.TextImageRelation = TextImageRelation.ImageBeforeText;

        _executionPanel.Dock = DockStyle.Bottom;
        _executionPanel.Height = 96;
        _executionPanel.Name = "_executionPanel";
        _executionPanel.Padding = new Padding(0, 8, 0, 0);
        _executionPanel.Controls.Add(_logBox);
        _executionPanel.Controls.Add(_archiveMergeDecisionPanel);
        _executionPanel.Controls.Add(_runStopButton);

        _archiveMergeDecisionPanel.Dock = DockStyle.Right;
        _archiveMergeDecisionPanel.Name = "_archiveMergeDecisionPanel";
        _archiveMergeDecisionPanel.Visible = false;
        _archiveMergeDecisionPanel.Width = 310;

        _logBox.BackColor = SystemColors.Window;
        _logBox.BorderStyle = BorderStyle.FixedSingle;
        _logBox.Dock = DockStyle.Fill;
        _logBox.Font = new Font("Consolas", 9F);
        _logBox.Multiline = true;
        _logBox.Name = "_logBox";
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Text = "Ready.";
        _logBox.WordWrap = true;

        _runStopButton.Dock = DockStyle.Right;
        _runStopButton.Height = 88;
        _runStopButton.Name = "_runStopButton";
        _runStopButton.Text = "Run";
        _runStopButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        _runStopButton.Width = 118;

        AllowDrop = true;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(980, 700);
        Controls.Add(_mainSplit);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;
        MinimumSize = new Size(920, 620);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "FileTools tasks";

        _executionPanel.ResumeLayout(false);
        _executionPanel.PerformLayout();
        _planToolStrip.ResumeLayout(false);
        _planToolStrip.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_planGrid).EndInit();
        _planGroup.ResumeLayout(false);
        _planGroup.PerformLayout();
        _actionToolStrip.ResumeLayout(false);
        _actionToolStrip.PerformLayout();
        _rightPanel.ResumeLayout(false);
        _rightPanel.PerformLayout();
        _targetToolStrip.ResumeLayout(false);
        _targetToolStrip.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_targetGrid).EndInit();
        _targetsGroup.ResumeLayout(false);
        _targetsGroup.PerformLayout();
        _mainSplit.Panel1.ResumeLayout(false);
        _mainSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_mainSplit).EndInit();
        _mainSplit.ResumeLayout(false);
        _menuStrip.ResumeLayout(false);
        _menuStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
