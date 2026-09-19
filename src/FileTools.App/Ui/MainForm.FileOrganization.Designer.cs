using System.Windows.Forms;

namespace FileTools;

public sealed partial class MainForm
{
    private ToolStripMenuItem _emptyFolderMenuItem = null!;
    private ToolStripButton _emptyFolderToolButton = null!;
    private ToolStripMenuItem _batchRenameMenuItem = null!;
    private ToolStripButton _batchRenameToolButton = null!;
    private ToolStripMenuItem _fileListMenuItem = null!;
    private ToolStripButton _fileListToolButton = null!;
    private ToolStripMenuItem _collectFilesMenuItem = null!;
    private ToolStripButton _collectFilesToolButton = null!;

    private void InitializeFileOrganizationCommands()
    {
        _emptyFolderMenuItem = new ToolStripMenuItem { Name = "_emptyFolderMenuItem", Text = "Clean empty folders" };
        _emptyFolderToolButton = new ToolStripButton { Name = "_emptyFolderToolButton", Text = "Clean empty folders", DisplayStyle = ToolStripItemDisplayStyle.Image };
        _taskMenuItem.DropDownItems.Insert(0, _emptyFolderMenuItem);
        _actionToolStrip.Items.Insert(0, _emptyFolderToolButton);
        _batchRenameMenuItem = new ToolStripMenuItem { Name = "_batchRenameMenuItem", Text = "Batch rename" };
        _batchRenameToolButton = new ToolStripButton { Name = "_batchRenameToolButton", Text = "Batch rename", DisplayStyle = ToolStripItemDisplayStyle.Image };
        _taskMenuItem.DropDownItems.Insert(1, _batchRenameMenuItem);
        _actionToolStrip.Items.Insert(1, _batchRenameToolButton);
        _fileListMenuItem = new ToolStripMenuItem { Name = "_fileListMenuItem" };
        _fileListToolButton = new ToolStripButton { Name = "_fileListToolButton", DisplayStyle = ToolStripItemDisplayStyle.Image };
        _taskMenuItem.DropDownItems.Insert(2, _fileListMenuItem);
        _actionToolStrip.Items.Insert(2, _fileListToolButton);
        _collectFilesMenuItem = new ToolStripMenuItem { Name = "_collectFilesMenuItem" };
        _collectFilesToolButton = new ToolStripButton { Name = "_collectFilesToolButton", DisplayStyle = ToolStripItemDisplayStyle.Image };
        _taskMenuItem.DropDownItems.Insert(3, _collectFilesMenuItem);
        _actionToolStrip.Items.Insert(3, _collectFilesToolButton);
    }
}
