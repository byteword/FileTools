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
        _topPanel.Name = "_topPanel";
        _topPanel.Padding = new Padding(8, 8, 8, 4);
        _topPanel.WrapContents = false;
        _topPanel.Controls.Add(_taskLabel);
        _topPanel.Controls.Add(_toolCombo);
        _topPanel.Controls.Add(_runButton);
        _topPanel.Controls.Add(_saveSettingsButton);
        _topPanel.Controls.Add(_installContextMenuButton);
        _topPanel.Controls.Add(_uninstallContextMenuButton);

        _taskLabel.Height = 28;
        _taskLabel.Name = "_taskLabel";
        _taskLabel.Text = "Task";
        _taskLabel.TextAlign = ContentAlignment.MiddleLeft;
        _taskLabel.Width = 48;

        _toolCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _toolCombo.Items.AddRange(new object[]
        {
            "Correct file names",
            "Folder wrapping / unwrapping",
            "Auto relocate folders"
        });
        _toolCombo.Name = "_toolCombo";
        _toolCombo.SelectedIndex = 0;
        _toolCombo.Width = 220;

        _runButton.Height = 28;
        _runButton.Name = "_runButton";
        _runButton.Text = "Run";
        _runButton.Width = 90;
        _saveSettingsButton.Height = 28;
        _saveSettingsButton.Name = "_saveSettingsButton";
        _saveSettingsButton.Text = "Save settings";
        _saveSettingsButton.Width = 116;
        _installContextMenuButton.Height = 28;
        _installContextMenuButton.Name = "_installContextMenuButton";
        _installContextMenuButton.Text = "Install ContextMenu";
        _installContextMenuButton.Width = 138;
        _uninstallContextMenuButton.Height = 28;
        _uninstallContextMenuButton.Name = "_uninstallContextMenuButton";
        _uninstallContextMenuButton.Text = "Remove ContextMenu";
        _uninstallContextMenuButton.Width = 138;

        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.FixedPanel = FixedPanel.Panel1;
        _mainSplit.Name = "_mainSplit";
        _mainSplit.Size = new Size(980, 656);
        _mainSplit.SplitterDistance = 410;
        _mainSplit.Panel1.Controls.Add(_pathList);
        _mainSplit.Panel1.Controls.Add(_dropTargetsLabel);
        _mainSplit.Panel1.Controls.Add(_pathButtonPanel);
        _mainSplit.Panel2.Controls.Add(_settingsPanel);

        _dropTargetsLabel.Dock = DockStyle.Top;
        _dropTargetsLabel.Height = 28;
        _dropTargetsLabel.Name = "_dropTargetsLabel";
        _dropTargetsLabel.Padding = new Padding(8, 7, 0, 0);
        _dropTargetsLabel.Text = "Drag-and-drop targets";

        _pathList.AllowDrop = true;
        _pathList.Dock = DockStyle.Fill;
        _pathList.HorizontalScrollbar = true;
        _pathList.Name = "_pathList";
        _pathList.SelectionMode = SelectionMode.MultiExtended;

        _pathButtonPanel.Dock = DockStyle.Bottom;
        _pathButtonPanel.Height = 44;
        _pathButtonPanel.Name = "_pathButtonPanel";
        _pathButtonPanel.Padding = new Padding(8, 6, 8, 6);
        _pathButtonPanel.WrapContents = false;
        _pathButtonPanel.Controls.Add(_addFilesButton);
        _pathButtonPanel.Controls.Add(_addFolderButton);
        _pathButtonPanel.Controls.Add(_removeSelectedButton);
        _pathButtonPanel.Controls.Add(_clearButton);

        _addFilesButton.Height = 28;
        _addFilesButton.Name = "_addFilesButton";
        _addFilesButton.Text = "Add files";
        _addFilesButton.Width = 82;
        _addFolderButton.Height = 28;
        _addFolderButton.Name = "_addFolderButton";
        _addFolderButton.Text = "Add folder";
        _addFolderButton.Width = 82;
        _removeSelectedButton.Height = 28;
        _removeSelectedButton.Name = "_removeSelectedButton";
        _removeSelectedButton.Text = "Remove selected";
        _removeSelectedButton.Width = 104;
        _clearButton.Height = 28;
        _clearButton.Name = "_clearButton";
        _clearButton.Text = "Clear";
        _clearButton.Width = 72;

        _settingsPanel.AutoScroll = true;
        _settingsPanel.Dock = DockStyle.Fill;
        _settingsPanel.Name = "_settingsPanel";
        _settingsPanel.Padding = new Padding(10);
        _settingsPanel.Controls.Add(_folderGroup);
        _settingsPanel.Controls.Add(_contextMenuGroup);
        _settingsPanel.Controls.Add(_templateGroup);
        _settingsPanel.Controls.Add(_statusGroup);

        _folderGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _folderGroup.Left = 10;
        _folderGroup.Name = "_folderGroup";
        _folderGroup.Text = "Folder wrapping / unwrapping";
        _folderGroup.Top = 8;
        _folderGroup.Width = 500;
        _folderGroup.Height = 78;
        _folderGroup.Controls.Add(_folderOperationCombo);

        _folderOperationCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _folderOperationCombo.Items.AddRange(new object[]
        {
            "Auto: wrap files, unwrap folders",
            "Wrap files",
            "Unwrap same-name single-file folders",
            "Unwrap single-file folders",
            "Move inner files up"
        });
        _folderOperationCombo.Left = 16;
        _folderOperationCombo.Name = "_folderOperationCombo";
        _folderOperationCombo.SelectedIndex = 0;
        _folderOperationCombo.Top = 30;
        _folderOperationCombo.Width = 450;

        _contextMenuGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _contextMenuGroup.Left = 10;
        _contextMenuGroup.Name = "_contextMenuGroup";
        _contextMenuGroup.Text = "ContextMenu";
        _contextMenuGroup.Top = 96;
        _contextMenuGroup.Width = 500;
        _contextMenuGroup.Height = 92;
        _contextMenuGroup.Controls.Add(_contextMenuEnabledCheckBox);
        _contextMenuGroup.Controls.Add(_contextMenuLayoutCombo);

        _contextMenuEnabledCheckBox.Left = 16;
        _contextMenuEnabledCheckBox.Name = "_contextMenuEnabledCheckBox";
        _contextMenuEnabledCheckBox.Text = "Register Explorer ContextMenu";
        _contextMenuEnabledCheckBox.Top = 28;
        _contextMenuEnabledCheckBox.Width = 260;

        _contextMenuLayoutCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _contextMenuLayoutCombo.Items.AddRange(new object[]
        {
            "Grouped: FileTools submenu",
            "Expanded: show each command directly"
        });
        _contextMenuLayoutCombo.Left = 16;
        _contextMenuLayoutCombo.Name = "_contextMenuLayoutCombo";
        _contextMenuLayoutCombo.SelectedIndex = 0;
        _contextMenuLayoutCombo.Top = 56;
        _contextMenuLayoutCombo.Width = 450;

        _templateGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _templateGroup.Left = 10;
        _templateGroup.Name = "_templateGroup";
        _templateGroup.Text = "Auto relocation templates";
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
        _templateLabel.Name = "_templateLabel";
        _templateLabel.Text = "Template";
        _templateLabel.Top = 28;
        _templateLabel.Width = 78;
        _templateLabel.Height = 22;
        _templateLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateCombo.Left = 96;
        _templateCombo.Name = "_templateCombo";
        _templateCombo.Top = 24;
        _templateCombo.Width = 250;

        _newTemplateButton.Left = 354;
        _newTemplateButton.Name = "_newTemplateButton";
        _newTemplateButton.Text = "New";
        _newTemplateButton.Top = 23;
        _newTemplateButton.Width = 56;
        _newTemplateButton.Height = 28;

        _deleteTemplateButton.Left = 416;
        _deleteTemplateButton.Name = "_deleteTemplateButton";
        _deleteTemplateButton.Text = "Delete";
        _deleteTemplateButton.Top = 23;
        _deleteTemplateButton.Width = 56;
        _deleteTemplateButton.Height = 28;

        _idLabel.Left = 16;
        _idLabel.Name = "_idLabel";
        _idLabel.Text = "ID";
        _idLabel.Top = 62;
        _idLabel.Width = 78;
        _idLabel.Height = 22;
        _idLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateIdBox.Left = 96;
        _templateIdBox.Name = "_templateIdBox";
        _templateIdBox.Top = 58;
        _templateIdBox.Width = 376;

        _nameLabel.Left = 16;
        _nameLabel.Name = "_nameLabel";
        _nameLabel.Text = "Name";
        _nameLabel.Top = 94;
        _nameLabel.Width = 78;
        _nameLabel.Height = 22;
        _nameLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateNameBox.Left = 96;
        _templateNameBox.Name = "_templateNameBox";
        _templateNameBox.Top = 90;
        _templateNameBox.Width = 376;

        _descriptionLabel.Left = 16;
        _descriptionLabel.Name = "_descriptionLabel";
        _descriptionLabel.Text = "Description";
        _descriptionLabel.Top = 126;
        _descriptionLabel.Width = 78;
        _descriptionLabel.Height = 22;
        _descriptionLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateDescriptionBox.Left = 96;
        _templateDescriptionBox.Name = "_templateDescriptionBox";
        _templateDescriptionBox.Top = 122;
        _templateDescriptionBox.Width = 376;

        _sourceLabel.Left = 16;
        _sourceLabel.Name = "_sourceLabel";
        _sourceLabel.Text = "Source";
        _sourceLabel.Top = 158;
        _sourceLabel.Width = 78;
        _sourceLabel.Height = 22;
        _sourceLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateSourceCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateSourceCombo.Items.AddRange(new object[]
        {
            "Title",
            "FileName",
            "ParentFolder"
        });
        _templateSourceCombo.Left = 96;
        _templateSourceCombo.Name = "_templateSourceCombo";
        _templateSourceCombo.SelectedIndex = 0;
        _templateSourceCombo.Top = 154;
        _templateSourceCombo.Width = 130;

        _transformLabel.Left = 238;
        _transformLabel.Name = "_transformLabel";
        _transformLabel.Text = "Transform";
        _transformLabel.Top = 158;
        _transformLabel.Width = 78;
        _transformLabel.Height = 22;
        _transformLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateTransformCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateTransformCombo.Items.AddRange(new object[]
        {
            "InitialBucket",
            "Literal",
            "None"
        });
        _templateTransformCombo.Left = 318;
        _templateTransformCombo.Name = "_templateTransformCombo";
        _templateTransformCombo.SelectedIndex = 0;
        _templateTransformCombo.Top = 154;
        _templateTransformCombo.Width = 154;

        _languageLabel.Left = 16;
        _languageLabel.Name = "_languageLabel";
        _languageLabel.Text = "Language";
        _languageLabel.Top = 190;
        _languageLabel.Width = 78;
        _languageLabel.Height = 22;
        _languageLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateLanguageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _templateLanguageCombo.Items.AddRange(new object[]
        {
            "KoreanEnglish",
            "English",
            "Numeric"
        });
        _templateLanguageCombo.Left = 96;
        _templateLanguageCombo.Name = "_templateLanguageCombo";
        _templateLanguageCombo.SelectedIndex = 0;
        _templateLanguageCombo.Top = 186;
        _templateLanguageCombo.Width = 130;

        _formatLabel.Left = 238;
        _formatLabel.Name = "_formatLabel";
        _formatLabel.Text = "Format";
        _formatLabel.Top = 190;
        _formatLabel.Width = 78;
        _formatLabel.Height = 22;
        _formatLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateFormatBox.Left = 318;
        _templateFormatBox.Name = "_templateFormatBox";
        _templateFormatBox.Top = 186;
        _templateFormatBox.Width = 154;

        _fallbackLabel.Left = 16;
        _fallbackLabel.Name = "_fallbackLabel";
        _fallbackLabel.Text = "Fallback";
        _fallbackLabel.Top = 222;
        _fallbackLabel.Width = 78;
        _fallbackLabel.Height = 22;
        _fallbackLabel.TextAlign = ContentAlignment.MiddleLeft;

        _templateFallbackBox.Left = 96;
        _templateFallbackBox.Name = "_templateFallbackBox";
        _templateFallbackBox.Top = 218;
        _templateFallbackBox.Width = 130;

        _saveTemplateButton.Left = 318;
        _saveTemplateButton.Name = "_saveTemplateButton";
        _saveTemplateButton.Text = "Save template";
        _saveTemplateButton.Top = 246;
        _saveTemplateButton.Width = 154;
        _saveTemplateButton.Height = 28;

        _statusGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _statusGroup.Left = 10;
        _statusGroup.Name = "_statusGroup";
        _statusGroup.Text = "Operation result";
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
        _statusBox.Name = "_statusBox";
        _statusBox.ReadOnly = true;
        _statusBox.ScrollBars = ScrollBars.Both;
        _statusBox.Text = "Drag files or folders into the list, then run a task.";
        _statusBox.WordWrap = false;

        AllowDrop = true;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(980, 700);
        Controls.Add(_mainSplit);
        Controls.Add(_topPanel);
        MinimumSize = new Size(860, 580);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "FileTools settings and tasks";

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
