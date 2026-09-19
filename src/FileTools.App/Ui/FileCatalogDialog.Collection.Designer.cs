using System.Drawing;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class FileCatalogDialog
{
    private CheckedListBox _kinds = null!;
    private TextBox _extensions = null!, _name = null!, _excludedPaths = null!;
    private ComboBox _nameMode = null!;
    private CheckBox _ignoreCase = null!, _minimumEnabled = null!, _maximumEnabled = null!;
    private NumericUpDown _minimumSize = null!, _maximumSize = null!;
    private DateTimePicker _createdFrom = null!, _createdThrough = null!, _modifiedFrom = null!, _modifiedThrough = null!;
    private Button _selectAll = null!, _selectNone = null!, _excludeFolder = null!;

    private Control CreateCollectionOptions()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 7 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _kinds = new CheckedListBox { Dock = DockStyle.Fill, Height = 56, CheckOnClick = true, MultiColumn = true, ColumnWidth = 130, IntegralHeight = false };
        var kinds = AutoRelocationFileTypeClassifier.NormalizeExtensionRules(_settings.FileKindExtensionRules)
            .Select(static rule => rule.Kind).Append(AutoRelocationFileTypeClassifier.OtherKind).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _kinds.Items.AddRange(kinds);
        Row(0, "CollectionKinds", _kinds);
        var names = Flow();
        _extensions = new TextBox { Width = 155, PlaceholderText = "jpg; png; pdf", Margin = new Padding(3, 3, 10, 3) };
        _nameMode = Choice<FileCollectionNameMode>("CollectionName", 140);
        _name = new TextBox { Width = 180, MaxLength = 255, Margin = new Padding(3, 3, 10, 3) };
        _ignoreCase = ScanCheck("CollectionIgnoreCase", true);
        names.Controls.Add(_extensions);
        AddChoice(names, "CollectionNameLabel", _nameMode);
        names.Controls.Add(_name); names.Controls.Add(_ignoreCase);
        Row(1, "CollectionExtensions", names);
        var dates = Flow();
        _modifiedFrom = DateControl(); _modifiedThrough = DateControl(); _createdFrom = DateControl(); _createdThrough = DateControl();
        dates.Controls.Add(_modifiedFrom); AddChoice(dates, "CollectionThrough", _modifiedThrough);
        Row(2, "CollectionModified", dates);
        var created = Flow();
        created.Controls.Add(_createdFrom); AddChoice(created, "CollectionThrough", _createdThrough);
        Row(3, "CollectionCreated", created);
        var sizes = Flow();
        _minimumEnabled = ScanCheck("CollectionMinimum", false); _maximumEnabled = ScanCheck("CollectionMaximum", false);
        _minimumSize = SizeControl(); _maximumSize = SizeControl();
        sizes.Controls.AddRange([_minimumEnabled, _minimumSize, _maximumEnabled, _maximumSize]);
        sizes.Controls.Add(new Label { Text = Localizer.Get("CollectionSizeUnit"), AutoSize = true, Margin = new Padding(8, 7, 3, 3) });
        Row(4, "CollectionSize", sizes);
        var excluded = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Height = 50 };
        excluded.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); excluded.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        _excludedPaths = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical,
            PlaceholderText = Localizer.Get("CollectionExcludeHelp") };
        _excludeFolder = new Button { Text = Localizer.Get("CollectionExcludeFolder"), Dock = DockStyle.Top, Height = 30 };
        excluded.Controls.Add(_excludedPaths, 0, 0); excluded.Controls.Add(_excludeFolder, 1, 0);
        Row(5, "CollectionExclude", excluded);
        var selection = Flow();
        _sort = Choice<FileCatalogSort>("CatalogSort", 135);
        _descending = ScanCheck("CatalogDescending", false);
        AddChoice(selection, "CatalogSortLabel", _sort); selection.Controls.Add(_descending);
        _selectAll = new Button { AutoSize = true, Height = 28, Text = Localizer.Get("CollectionSelectAll") };
        _selectNone = new Button { AutoSize = true, Height = 28, Text = Localizer.Get("CollectionSelectNone") };
        selection.Controls.AddRange([_selectAll, _selectNone]);
        Row(6, "CollectionSelection", selection);
        return panel;

        void Row(int index, string key, Control control)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, index switch { 0 => 62, 5 => 54, 6 => 42, _ => 36 }));
            panel.Controls.Add(new Label { Text = Localizer.Get(key), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = true }, 0, index);
            panel.Controls.Add(control, 1, index);
        }
        static FlowLayoutPanel Flow() => new() { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Margin = new Padding(0, 2, 0, 2) };
        static DateTimePicker DateControl() => new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", ShowCheckBox = true,
            Checked = false, Width = 126, Margin = new Padding(3, 3, 10, 3) };
        static NumericUpDown SizeControl() => new() { Minimum = 0, Maximum = 8_000_000_000_000m, DecimalPlaces = 3, Increment = 1,
            Width = 155, Enabled = false, ThousandsSeparator = true };
    }
}
