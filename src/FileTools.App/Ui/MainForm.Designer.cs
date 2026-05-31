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

        ConfigureTopButton(_runButton, 90);
        ConfigureTopButton(_saveSettingsButton, 116);
        ConfigureTopButton(_installContextMenuButton, 138);
        ConfigureTopButton(_uninstallContextMenuButton, 138);

        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.FixedPanel = FixedPanel.Panel1;
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

        ConfigureBottomButton(_addFilesButton, 82);
        ConfigureBottomButton(_addFolderButton, 82);
        ConfigureBottomButton(_removeSelectedButton, 104);
        ConfigureBottomButton(_clearButton, 72);

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

        ConfigureLabel(_templateLabel, 16, 28, 78);
        ConfigureCombo(_templateCombo, 96, 24, 250);
        ConfigureButton(_newTemplateButton, 354, 23, 56);
        ConfigureButton(_deleteTemplateButton, 416, 23, 56);
        ConfigureLabel(_idLabel, 16, 62, 78);
        ConfigureTextBox(_templateIdBox, 96, 58, 376);
        ConfigureLabel(_nameLabel, 16, 94, 78);
        ConfigureTextBox(_templateNameBox, 96, 90, 376);
        ConfigureLabel(_descriptionLabel, 16, 126, 78);
        ConfigureTextBox(_templateDescriptionBox, 96, 122, 376);
        ConfigureLabel(_sourceLabel, 16, 158, 78);
        ConfigureCombo(_templateSourceCombo, 96, 154, 130);
        ConfigureLabel(_transformLabel, 238, 158, 78);
        ConfigureCombo(_templateTransformCombo, 318, 154, 154);
        ConfigureLabel(_languageLabel, 16, 190, 78);
        ConfigureCombo(_templateLanguageCombo, 96, 186, 130);
        ConfigureLabel(_formatLabel, 238, 190, 78);
        ConfigureTextBox(_templateFormatBox, 318, 186, 154);
        ConfigureLabel(_fallbackLabel, 16, 222, 78);
        ConfigureTextBox(_templateFallbackBox, 96, 218, 130);
        ConfigureButton(_saveTemplateButton, 318, 246, 154);

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

    private static void ConfigureTopButton(Button button, int width)
    {
        button.Height = 28;
        button.Width = width;
    }

    private static void ConfigureBottomButton(Button button, int width)
    {
        button.Height = 28;
        button.Width = width;
    }

    private static void ConfigureButton(Button button, int left, int top, int width)
    {
        button.Left = left;
        button.Top = top;
        button.Width = width;
        button.Height = 28;
    }

    private static void ConfigureLabel(Label label, int left, int top, int width)
    {
        label.Left = left;
        label.Top = top;
        label.Width = width;
        label.Height = 22;
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private static void ConfigureTextBox(TextBox textBox, int left, int top, int width)
    {
        textBox.Left = left;
        textBox.Top = top;
        textBox.Width = width;
    }

    private static void ConfigureCombo(ComboBox combo, int left, int top, int width)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Left = left;
        combo.Top = top;
        combo.Width = width;
    }
}
