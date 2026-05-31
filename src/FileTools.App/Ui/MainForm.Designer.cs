using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private FlowLayoutPanel _topPanel = null!;
    private Label _taskLabel = null!;
    private ComboBox _toolCombo = null!;
    private Button _runButton = null!;
    private Button _saveSettingsButton = null!;
    private Button _installContextMenuButton = null!;
    private Button _uninstallContextMenuButton = null!;
    private SplitContainer _mainSplit = null!;
    private Label _dropTargetsLabel = null!;
    private ListBox _pathList = null!;
    private FlowLayoutPanel _pathButtonPanel = null!;
    private Button _addFilesButton = null!;
    private Button _addFolderButton = null!;
    private Button _removeSelectedButton = null!;
    private Button _clearButton = null!;
    private Panel _settingsPanel = null!;
    private GroupBox _folderGroup = null!;
    private ComboBox _folderOperationCombo = null!;
    private GroupBox _contextMenuGroup = null!;
    private CheckBox _contextMenuEnabledCheckBox = null!;
    private ComboBox _contextMenuLayoutCombo = null!;
    private GroupBox _templateGroup = null!;
    private Label _templateLabel = null!;
    private ComboBox _templateCombo = null!;
    private Button _newTemplateButton = null!;
    private Button _deleteTemplateButton = null!;
    private Label _idLabel = null!;
    private TextBox _templateIdBox = null!;
    private Label _nameLabel = null!;
    private TextBox _templateNameBox = null!;
    private Label _descriptionLabel = null!;
    private TextBox _templateDescriptionBox = null!;
    private Label _sourceLabel = null!;
    private ComboBox _templateSourceCombo = null!;
    private Label _transformLabel = null!;
    private ComboBox _templateTransformCombo = null!;
    private Label _languageLabel = null!;
    private ComboBox _templateLanguageCombo = null!;
    private Label _formatLabel = null!;
    private TextBox _templateFormatBox = null!;
    private Label _fallbackLabel = null!;
    private TextBox _templateFallbackBox = null!;
    private Button _saveTemplateButton = null!;
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
        _topPanel = new FlowLayoutPanel();
        _taskLabel = new Label();
        _toolCombo = new ComboBox();
        _runButton = new Button();
        _saveSettingsButton = new Button();
        _installContextMenuButton = new Button();
        _uninstallContextMenuButton = new Button();
        _mainSplit = new SplitContainer();
        _dropTargetsLabel = new Label();
        _pathList = new ListBox();
        _pathButtonPanel = new FlowLayoutPanel();
        _addFilesButton = new Button();
        _addFolderButton = new Button();
        _removeSelectedButton = new Button();
        _clearButton = new Button();
        _settingsPanel = new Panel();
        _folderGroup = new GroupBox();
        _folderOperationCombo = new ComboBox();
        _contextMenuGroup = new GroupBox();
        _contextMenuEnabledCheckBox = new CheckBox();
        _contextMenuLayoutCombo = new ComboBox();
        _templateGroup = new GroupBox();
        _templateLabel = new Label();
        _templateCombo = new ComboBox();
        _newTemplateButton = new Button();
        _deleteTemplateButton = new Button();
        _idLabel = new Label();
        _templateIdBox = new TextBox();
        _nameLabel = new Label();
        _templateNameBox = new TextBox();
        _descriptionLabel = new Label();
        _templateDescriptionBox = new TextBox();
        _sourceLabel = new Label();
        _templateSourceCombo = new ComboBox();
        _transformLabel = new Label();
        _templateTransformCombo = new ComboBox();
        _languageLabel = new Label();
        _templateLanguageCombo = new ComboBox();
        _formatLabel = new Label();
        _templateFormatBox = new TextBox();
        _fallbackLabel = new Label();
        _templateFallbackBox = new TextBox();
        _saveTemplateButton = new Button();
        _statusGroup = new GroupBox();
        _statusBox = new TextBox();
        ((System.ComponentModel.ISupportInitialize)_mainSplit).BeginInit();
        _mainSplit.Panel1.SuspendLayout();
        _mainSplit.Panel2.SuspendLayout();
        _mainSplit.SuspendLayout();
        _topPanel.SuspendLayout();
        _pathButtonPanel.SuspendLayout();
        _settingsPanel.SuspendLayout();
        _folderGroup.SuspendLayout();
        _contextMenuGroup.SuspendLayout();
        _templateGroup.SuspendLayout();
        _statusGroup.SuspendLayout();
        SuspendLayout();

        _topPanel.Dock = DockStyle.Top;
        _topPanel.FlowDirection = FlowDirection.LeftToRight;
        _topPanel.Height = 44;
        _topPanel.Padding = new Padding(8, 8, 8, 4);
        _topPanel.WrapContents = false;
        _topPanel.Controls.Add(_taskLabel);
        _topPanel.Controls.Add(_toolCombo);
        _topPanel.Controls.Add(_runButton);
        _topPanel.Controls.Add(_saveSettingsButton);
        _topPanel.Controls.Add(_installContextMenuButton);
        _topPanel.Controls.Add(_uninstallContextMenuButton);

        _taskLabel.Height = 28;
        _taskLabel.TextAlign = ContentAlignment.MiddleLeft;
        _taskLabel.Width = 48;

        _toolCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _toolCombo.Width = 220;

        _runButton.Height = 28;
        _runButton.Width = 90;
        _saveSettingsButton.Height = 28;
        _saveSettingsButton.Width = 116;
        _installContextMenuButton.Height = 28;
        _installContextMenuButton.Width = 138;
        _uninstallContextMenuButton.Height = 28;
        _uninstallContextMenuButton.Width = 138;

        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.FixedPanel = FixedPanel.Panel1;
        _mainSplit.Size = new Size(980, 656);
        _mainSplit.SplitterDistance = 410;
        _mainSplit.Panel1.Controls.Add(_pathList);
        _mainSplit.Panel1.Controls.Add(_dropTargetsLabel);
        _mainSplit.Panel1.Controls.Add(_pathButtonPanel);
        _mainSplit.Panel2.Controls.Add(_settingsPanel);

        _dropTargetsLabel.Dock = DockStyle.Top;
        _dropTargetsLabel.Height = 28;
        _dropTargetsLabel.Padding = new Padding(8, 7, 0, 0);

        _pathList.AllowDrop = true;
        _pathList.Dock = DockStyle.Fill;
        _pathList.HorizontalScrollbar = true;
        _pathList.SelectionMode = SelectionMode.MultiExtended;

        _pathButtonPanel.Dock = DockStyle.Bottom;
        _pathButtonPanel.Height = 44;
        _pathButtonPanel.Padding = new Padding(8, 6, 8, 6);
        _pathButtonPanel.WrapContents = false;
        _pathButtonPanel.Controls.Add(_addFilesButton);
        _pathButtonPanel.Controls.Add(_addFolderButton);
        _pathButtonPanel.Controls.Add(_removeSelectedButton);
        _pathButtonPanel.Controls.Add(_clearButton);

        _addFilesButton.Height = 28;
        _addFilesButton.Width = 82;
        _addFolderButton.Height = 28;
        _addFolderButton.Width = 82;
        _removeSelectedButton.Height = 28;
        _removeSelectedButton.Width = 104;
        _clearButton.Height = 28;
        _clearButton.Width = 72;

        _settingsPanel.AutoScroll = true;
        _settingsPanel.Dock = DockStyle.Fill;
        _settingsPanel.Padding = new Padding(10);
        _settingsPanel.Controls.Add(_folderGroup);
        _settingsPanel.Controls.Add(_contextMenuGroup);
        _settingsPanel.Controls.Add(_templateGroup);
        _settingsPanel.Controls.Add(_statusGroup);

        _folderGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _folderGroup.Left = 10;
        _folderGroup.Top = 8;
        _folderGroup.Width = 500;
        _folderGroup.Height = 78;
        _folderGroup.Controls.Add(_folderOperationCombo);

        _folderOperationCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _folderOperationCombo.Left = 16;
        _folderOperationCombo.Top = 30;
        _folderOperationCombo.Width = 450;

        _contextMenuGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _contextMenuGroup.Left = 10;
        _contextMenuGroup.Top = 96;
        _contextMenuGroup.Width = 500;
        _contextMenuGroup.Height = 92;
        _contextMenuGroup.Controls.Add(_contextMenuEnabledCheckBox);
        _contextMenuGroup.Controls.Add(_contextMenuLayoutCombo);

        _contextMenuEnabledCheckBox.Left = 16;
        _contextMenuEnabledCheckBox.Top = 28;
        _contextMenuEnabledCheckBox.Width = 260;

        _contextMenuLayoutCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _contextMenuLayoutCombo.Left = 16;
        _contextMenuLayoutCombo.Top = 56;
        _contextMenuLayoutCombo.Width = 450;

        _templateGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _templateGroup.Left = 10;
        _templateGroup.Top = 198;
        _templateGroup.Width = 500;
        _templateGroup.Height = 300;
        _templateGroup.Controls.Add(_templateLabel);
        _templateGroup.Controls.Add(_templateCombo);
        _templateGroup.Controls.Add(_newTemplateButton);
        _templateGroup.Controls.Add(_deleteTemplateButton);
        _templateGroup.Controls.Add(_idLabel);
        _templateGroup.Controls.Add(_templateIdBox);
        _templateGroup.Controls.Add(_nameLabel);
        _templateGroup.Controls.Add(_templateNameBox);
        _templateGroup.Controls.Add(_descriptionLabel);
        _templateGroup.Controls.Add(_templateDescriptionBox);
        _templateGroup.Controls.Add(_sourceLabel);
        _templateGroup.Controls.Add(_templateSourceCombo);
        _templateGroup.Controls.Add(_transformLabel);
        _templateGroup.Controls.Add(_templateTransformCombo);
        _templateGroup.Controls.Add(_languageLabel);
        _templateGroup.Controls.Add(_templateLanguageCombo);
        _templateGroup.Controls.Add(_formatLabel);
        _templateGroup.Controls.Add(_templateFormatBox);
        _templateGroup.Controls.Add(_fallbackLabel);
        _templateGroup.Controls.Add(_templateFallbackBox);
        _templateGroup.Controls.Add(_saveTemplateButton);

        _templateLabel.Left = 16;
        _templateLabel.Top = 28;
        _templateLabel.Width = 78;
        _templateLabel.Height = 22;
        _templateLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateCombo.Left = 96;
        _templateCombo.Top = 24;
        _templateCombo.Width = 250;

        _newTemplateButton.Left = 354;
        _newTemplateButton.Top = 23;
        _newTemplateButton.Width = 56;
        _newTemplateButton.Height = 28;

        _deleteTemplateButton.Left = 416;
        _deleteTemplateButton.Top = 23;
        _deleteTemplateButton.Width = 56;
        _deleteTemplateButton.Height = 28;

        _idLabel.Left = 16;
        _idLabel.Top = 62;
        _idLabel.Width = 78;
        _idLabel.Height = 22;
        _idLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateIdBox.Left = 96;
        _templateIdBox.Top = 58;
        _templateIdBox.Width = 376;

        _nameLabel.Left = 16;
        _nameLabel.Top = 94;
        _nameLabel.Width = 78;
        _nameLabel.Height = 22;
        _nameLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateNameBox.Left = 96;
        _templateNameBox.Top = 90;
        _templateNameBox.Width = 376;

        _descriptionLabel.Left = 16;
        _descriptionLabel.Top = 126;
        _descriptionLabel.Width = 78;
        _descriptionLabel.Height = 22;
        _descriptionLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateDescriptionBox.Left = 96;
        _templateDescriptionBox.Top = 122;
        _templateDescriptionBox.Width = 376;

        _sourceLabel.Left = 16;
        _sourceLabel.Top = 158;
        _sourceLabel.Width = 78;
        _sourceLabel.Height = 22;
        _sourceLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateSourceCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateSourceCombo.Left = 96;
        _templateSourceCombo.Top = 154;
        _templateSourceCombo.Width = 130;

        _transformLabel.Left = 238;
        _transformLabel.Top = 158;
        _transformLabel.Width = 78;
        _transformLabel.Height = 22;
        _transformLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateTransformCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateTransformCombo.Left = 318;
        _templateTransformCombo.Top = 154;
        _templateTransformCombo.Width = 154;

        _languageLabel.Left = 16;
        _languageLabel.Top = 190;
        _languageLabel.Width = 78;
        _languageLabel.Height = 22;
        _languageLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateLanguageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateLanguageCombo.Left = 96;
        _templateLanguageCombo.Top = 186;
        _templateLanguageCombo.Width = 130;

        _formatLabel.Left = 238;
        _formatLabel.Top = 190;
        _formatLabel.Width = 78;
        _formatLabel.Height = 22;
        _formatLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateFormatBox.Left = 318;
        _templateFormatBox.Top = 186;
        _templateFormatBox.Width = 154;

        _fallbackLabel.Left = 16;
        _fallbackLabel.Top = 222;
        _fallbackLabel.Width = 78;
        _fallbackLabel.Height = 22;
        _fallbackLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateFallbackBox.Left = 96;
        _templateFallbackBox.Top = 218;
        _templateFallbackBox.Width = 130;

        _saveTemplateButton.Left = 318;
        _saveTemplateButton.Top = 246;
        _saveTemplateButton.Width = 154;
        _saveTemplateButton.Height = 28;

        _statusGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _statusGroup.Left = 10;
        _statusGroup.Top = 508;
        _statusGroup.Width = 500;
        _statusGroup.Height = 210;
        _statusGroup.Controls.Add(_statusBox);

        _statusBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _statusBox.Left = 12;
        _statusBox.Top = 24;
        _statusBox.Width = 468;
        _statusBox.Height = 170;
        _statusBox.Multiline = true;
        _statusBox.ReadOnly = true;
        _statusBox.ScrollBars = ScrollBars.Both;
        _statusBox.WordWrap = false;

        AllowDrop = true;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(980, 700);
        Controls.Add(_mainSplit);
        Controls.Add(_topPanel);
        MinimumSize = new Size(860, 580);
        StartPosition = FormStartPosition.CenterScreen;

        _statusGroup.ResumeLayout(false);
        _statusGroup.PerformLayout();
        _templateGroup.ResumeLayout(false);
        _templateGroup.PerformLayout();
        _contextMenuGroup.ResumeLayout(false);
        _folderGroup.ResumeLayout(false);
        _settingsPanel.ResumeLayout(false);
        _pathButtonPanel.ResumeLayout(false);
        _topPanel.ResumeLayout(false);
        _mainSplit.Panel1.ResumeLayout(false);
        _mainSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_mainSplit).EndInit();
        _mainSplit.ResumeLayout(false);
        ResumeLayout(false);
    }

}
