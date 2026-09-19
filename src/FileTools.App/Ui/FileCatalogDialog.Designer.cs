using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class FileCatalogDialog
{
    private Label _help = null!, _summary = null!;
    private CheckBox _recursive = null!, _hidden = null!, _system = null!, _descending = null!;
    private ComboBox _sort = null!, _format = null!, _textField = null!;
    private Button _refresh = null!, _stop = null!, _accept = null!, _close = null!;
    private FlowLayoutPanel _columns = null!;
    private Control _options = null!;
    private DataGridView _grid = null!;
    private TextBox _errors = null!;
    private readonly Dictionary<FileListColumn, CheckBox> _columnChecks = [];

    private void InitializeComponent()
    {
        Text = Localizer.Get(_collect ? "CollectionTitle" : "FileListTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1140, _collect ? 860 : 800);
        MinimumSize = new Size(920, _collect ? 760 : 650);
        AutoScaleMode = AutoScaleMode.Font;
        ShowInTaskbar = false;
        MinimizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 7 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        _help = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 8), Text = Localizer.Get(_collect ? "CollectionHelp" : "FileListHelp") };
        var scanOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        _recursive = ScanCheck("CatalogRecursive", true);
        _hidden = ScanCheck("CatalogHidden", false);
        _system = ScanCheck("CatalogSystem", false);
        _refresh = new Button { AutoSize = true, Height = 30, Text = Localizer.Get("CatalogScan") };
        _stop = new Button { AutoSize = true, Height = 30, Text = Localizer.Get("CatalogStop"), Enabled = false };
        scanOptions.Controls.AddRange([_recursive, _hidden, _system, _refresh, _stop]);
        _options = _collect ? CreateCollectionOptions() : CreateExportOptions();
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, VirtualMode = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false, ReadOnly = !_collect, AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = SystemColors.Window
        };
        if (_collect) _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Include", HeaderText = Localizer.Get("CollectionInclude"), Width = 55 });
        foreach (var column in new[] { FileListColumn.Name, FileListColumn.Kind, FileListColumn.Size, FileListColumn.Modified,
                     FileListColumn.Created, FileListColumn.FullPath, FileListColumn.RootPath, FileListColumn.RelativePath, FileListColumn.Extension })
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = column.ToString(), Tag = column,
                HeaderText = Localizer.Get("CatalogColumn" + column), ReadOnly = true, Width = column switch
                { FileListColumn.Name => 220, FileListColumn.FullPath or FileListColumn.RootPath or FileListColumn.RelativePath => 320,
                    FileListColumn.Modified or FileListColumn.Created => 170, _ => 100 }, SortMode = DataGridViewColumnSortMode.NotSortable });
        _errors = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
            WordWrap = false, AccessibleName = Localizer.Get("CatalogErrors") };
        _summary = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        _accept = new Button { Width = 170, Height = 30, Text = Localizer.Get(_collect ? "CollectionAdd" : "FileListSave"), Enabled = false };
        _close = new Button { Width = 100, Height = 30, Text = Localizer.Get("CatalogClose") };
        layout.Controls.Add(_help, 0, 0);
        layout.Controls.Add(scanOptions, 0, 1);
        layout.Controls.Add(_options, 0, 2);
        layout.Controls.Add(_grid, 0, 3);
        layout.Controls.Add(_errors, 0, 4);
        layout.Controls.Add(_summary, 0, 5);
        layout.Controls.Add(DialogButtonPanelFactory.CreateRightAligned(_accept, _close), 0, 6);
        Controls.Add(layout);
        AcceptButton = _accept;
        CancelButton = _close;
    }

    private Control CreateExportOptions()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        _format = Choice<FileListFormat>("FileListFormat", 100);
        _textField = Choice<FileListTextField>("FileListText", 160);
        _sort = Choice<FileCatalogSort>("CatalogSort", 130);
        _descending = ScanCheck("CatalogDescending", false);
        AddChoice(options, "FileListFormatLabel", _format);
        AddChoice(options, "FileListTextLabel", _textField);
        AddChoice(options, "CatalogSortLabel", _sort);
        options.Controls.Add(_descending);
        _columns = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 3, 0, 8) };
        foreach (var column in Enum.GetValues<FileListColumn>())
        {
            var check = ScanCheck("CatalogColumn" + column, true);
            _columnChecks.Add(column, check);
            _columns.Controls.Add(check);
        }
        panel.Controls.Add(options, 0, 0);
        panel.Controls.Add(_columns, 0, 1);
        return panel;
    }

    private static CheckBox ScanCheck(string key, bool value) => new()
    { AutoSize = true, Checked = value, Text = Localizer.Get(key), Margin = new Padding(3, 7, 14, 3) };

    private static ComboBox Choice<T>(string prefix, int width) where T : struct, Enum
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, Margin = new Padding(3, 3, 14, 3) };
        combo.Items.AddRange(Enum.GetValues<T>().Select(value => (object)new ComboOption<T>(Localizer.Get(prefix + value), value)).ToArray());
        combo.SelectedIndex = 0;
        return combo;
    }

    private static void AddChoice(FlowLayoutPanel panel, string key, Control input)
    {
        panel.Controls.Add(new Label { Text = Localizer.Get(key), AutoSize = true, Margin = new Padding(3, 7, 6, 3) });
        panel.Controls.Add(input);
    }
}
