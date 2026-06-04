using System.Drawing;
using System.Windows.Forms;

#nullable enable

namespace FileTools;

internal sealed partial class FileKindClassificationEditorDialog
{
    private readonly ListBox _kindList = new();
    private readonly TextBox _kindNameBox = new();
    private readonly TextBox _extensionsBox = new();
    private readonly TextBox _extensionSearchBox = new();
    private readonly ListBox _registeredExtensionList = new();
    private readonly Label _statusLabel = new();
    private readonly Button _addKindButton = new();
    private readonly Button _renameKindButton = new();
    private readonly Button _deleteKindButton = new();

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(CreateHelpLabel(), 0, 0);
        root.Controls.Add(CreateEditorPanel(), 0, 1);
        root.Controls.Add(CreateButtonPanel(), 0, 2);
    }

    private Control CreateHelpLabel()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("FileKindClassificationHelp"),
            ForeColor = Color.FromArgb(55, 65, 81),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private Control CreateEditorPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(CreateKindPanel(), 0, 0);
        panel.Controls.Add(CreateRulePanel(), 1, 0);
        return panel;
    }

    private Control CreateKindPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 10, 0),
            ColumnCount = 1,
            RowCount = 5
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.Controls.Add(CreateFieldLabel(Localizer.Get("LabelFileKindCategories")), 0, 0);

        _kindList.Dock = DockStyle.Fill;
        _kindList.SelectedIndexChanged += (_, _) => SelectCurrentRule();
        panel.Controls.Add(_kindList, 0, 1);

        panel.Controls.Add(CreateFieldLabel(Localizer.Get("LabelFileKindRepresentativeName")), 0, 2);
        _kindNameBox.Dock = DockStyle.Fill;
        _kindNameBox.PlaceholderText = Localizer.Get("LabelFileKindRepresentativeName");
        _kindNameBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                RenameCurrentRule();
                e.SuppressKeyPress = true;
            }
        };
        panel.Controls.Add(_kindNameBox, 0, 3);
        panel.Controls.Add(CreateKindButtonPanel(), 0, 4);
        return panel;
    }

    private Control CreateKindButtonPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0)
        };

        _addKindButton.Text = Localizer.Get("ButtonAdd");
        _addKindButton.Width = 64;
        _addKindButton.Height = 30;
        _addKindButton.Click += (_, _) => AddFileKind();

        _renameKindButton.Text = Localizer.Get("ButtonRenameFileKind");
        _renameKindButton.Width = 86;
        _renameKindButton.Height = 30;
        _renameKindButton.Click += (_, _) => RenameCurrentRule();

        _deleteKindButton.Text = Localizer.Get("ButtonDelete");
        _deleteKindButton.Width = 64;
        _deleteKindButton.Height = 30;
        _deleteKindButton.Click += (_, _) => DeleteCurrentRule();

        panel.Controls.Add(_addKindButton);
        panel.Controls.Add(_renameKindButton);
        panel.Controls.Add(_deleteKindButton);
        return panel;
    }

    private Control CreateRulePanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        panel.Controls.Add(CreateFieldLabel(Localizer.Get("LabelFileKindExtensions")), 0, 0);
        _extensionsBox.Dock = DockStyle.Fill;
        _extensionsBox.Multiline = true;
        _extensionsBox.ScrollBars = ScrollBars.Vertical;
        _extensionsBox.AcceptsReturn = true;
        panel.Controls.Add(_extensionsBox, 0, 1);

        panel.Controls.Add(CreateFieldLabel(Localizer.Get("LabelRegisteredExtensions")), 0, 2);
        _extensionSearchBox.Dock = DockStyle.Fill;
        _extensionSearchBox.PlaceholderText = Localizer.Get("LabelExtensionSearch");
        _extensionSearchBox.TextChanged += (_, _) => RefreshRegisteredExtensions();
        panel.Controls.Add(_extensionSearchBox, 0, 3);

        _registeredExtensionList.Dock = DockStyle.Fill;
        _registeredExtensionList.DoubleClick += (_, _) => AddSelectedRegisteredExtension();
        panel.Controls.Add(_registeredExtensionList, 0, 4);

        var addButton = new Button
        {
            Text = Localizer.Get("ButtonAddExtension"),
            Width = 150,
            Height = 30
        };
        addButton.Click += (_, _) => AddSelectedRegisteredExtension();
        panel.Controls.Add(addButton, 0, 5);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.FromArgb(100, 116, 139);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(_statusLabel, 0, 6);
        return panel;
    }

    private Control CreateButtonPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 90
        };
        var okButton = new Button { Text = "OK", Width = 90 };
        var resetButton = new Button
        {
            Text = Localizer.Get("ButtonRestoreDefaults"),
            Width = 140
        };

        okButton.Click += (_, _) => Confirm();
        resetButton.Click += (_, _) => RestoreDefaults();
        panel.Controls.Add(cancelButton);
        panel.Controls.Add(okButton);
        panel.Controls.Add(resetButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        return panel;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }
}
