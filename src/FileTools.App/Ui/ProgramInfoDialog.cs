using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace FileTools;

internal sealed class ProgramInfoDialog : Form
{
    private const string LicenseResourceName = "FileTools.LICENSE.txt";

    private readonly Label _nameLabel = new();
    private readonly Label _versionLabel = new();
    private readonly Label _licenseLabel = new();
    private readonly TextBox _licenseBox = new();
    private readonly Button _closeButton = new();

    public ProgramInfoDialog()
    {
        Text = Localizer.Get("ProgramInfoTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(720, 520);
        MinimumSize = new Size(560, 420);
        ShowIcon = false;

        BuildLayout();
        LoadProgramInfo();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.Controls.Add(header, 0, 0);

        var iconBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.CenterImage,
            Image = ApplicationIconProvider.GetApplicationIconImage()
        };
        header.Controls.Add(iconBox, 0, 0);
        header.SetRowSpan(iconBox, 2);

        _nameLabel.Dock = DockStyle.Fill;
        _nameLabel.Font = new Font(Font, FontStyle.Bold);
        _nameLabel.TextAlign = ContentAlignment.MiddleLeft;
        _nameLabel.AutoEllipsis = true;
        header.Controls.Add(_nameLabel, 1, 0);

        _versionLabel.Dock = DockStyle.Fill;
        _versionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _versionLabel.AutoEllipsis = true;
        header.Controls.Add(_versionLabel, 1, 1);

        var licensePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        licensePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        licensePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(licensePanel, 0, 1);

        _licenseLabel.Dock = DockStyle.Fill;
        _licenseLabel.TextAlign = ContentAlignment.MiddleLeft;
        licensePanel.Controls.Add(_licenseLabel, 0, 0);

        _licenseBox.Dock = DockStyle.Fill;
        _licenseBox.Multiline = true;
        _licenseBox.ReadOnly = true;
        _licenseBox.ScrollBars = ScrollBars.Both;
        _licenseBox.WordWrap = false;
        licensePanel.Controls.Add(_licenseBox, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        _closeButton.Text = Localizer.Get("ButtonClose");
        _closeButton.Width = 96;
        _closeButton.Height = 30;
        _closeButton.DialogResult = DialogResult.OK;
        buttons.Controls.Add(_closeButton);
        root.Controls.Add(buttons, 0, 2);

        AcceptButton = _closeButton;
        CancelButton = _closeButton;
    }

    private void LoadProgramInfo()
    {
        _nameLabel.Text = FileToolsEnvironment.AppName;
        _versionLabel.Text = Localizer.Format("ProgramInfoVersionFormat", GetApplicationVersion());
        _licenseLabel.Text = Localizer.Get("ProgramInfoLicenseTitle");
        _licenseBox.Text = LoadLicenseText();
    }

    private static string GetApplicationVersion()
    {
        var assembly = typeof(ProgramInfoDialog).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? Localizer.Get("ProgramInfoUnknownVersion");
    }

    private static string LoadLicenseText()
    {
        var assembly = typeof(ProgramInfoDialog).Assembly;
        using var stream = assembly.GetManifestResourceStream(LicenseResourceName);
        if (stream is null)
        {
            return Localizer.Get("ProgramInfoLicenseUnavailable");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

}
