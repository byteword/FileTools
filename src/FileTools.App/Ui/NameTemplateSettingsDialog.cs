using System.Windows.Forms;

namespace FileTools;

internal sealed partial class NameTemplateSettingsDialog : Form
{

    public NameTemplateSettingsDialog(FileToolsSettings settings)
    {
        Settings = settings.Clone();
        Text = Localizer.Get("DialogNameTemplateSettingsTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 430);
        MinimumSize = new Size(680, 380);
        MinimizeBox = false;
        MaximizeBox = false;

        BuildLayout();
        LoadSettings();
        UpdatePreview();
    }

    public FileToolsSettings Settings { get; private set; }

    private void LoadSettings()
    {
        _wrapTemplateBox.Text = Settings.FolderWrapFolderNameTemplate;
        _unwrapTemplateBox.Text = Settings.FolderUnwrapMismatchFileNameTemplate;
        _conflictTemplateBox.Text = Settings.FolderStructureConflictNameTemplate;

        _conflictPolicyCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _conflictPolicyCombo.DataSource = new[]
        {
            NameCollisionPolicy.Skip,
            NameCollisionPolicy.AutoNumber
        }
            .Select(policy => new ComboOption<NameCollisionPolicy>(NameTemplateText.GetDisplayName(policy), policy))
            .ToArray();
        SelectComboValue(_conflictPolicyCombo, Settings.FolderStructureConflictPolicy);

        _indexStyleCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _indexStyleCombo.DataSource = Enum.GetValues<ConflictIndexStyle>()
            .Select(style => new ComboOption<ConflictIndexStyle>(NameTemplateText.GetDisplayName(style), style))
            .ToArray();
        SelectComboValue(_indexStyleCombo, Settings.FolderStructureConflictIndexStyle);

        _toolTip.SetToolTip(_wrapTemplateBox, Localizer.Get("NameTemplateTokensHelp"));
        _toolTip.SetToolTip(_unwrapTemplateBox, Localizer.Get("NameTemplateTokensHelp"));
        _toolTip.SetToolTip(_conflictTemplateBox, Localizer.Get("ConflictTemplateTokensHelp"));
    }

    private void SaveAndClose()
    {
        var wrapTemplate = NormalizeTemplate(_wrapTemplateBox.Text, NameTemplateDefaults.FolderWrapFolderNameTemplate);
        var unwrapTemplate = NormalizeTemplate(_unwrapTemplateBox.Text, NameTemplateDefaults.FolderUnwrapPrefixFolderNameTemplate);
        var conflictTemplate = NormalizeTemplate(_conflictTemplateBox.Text, NameTemplateDefaults.DefaultConflictNameTemplate);
        var validation = ValidateTemplates(wrapTemplate, unwrapTemplate, conflictTemplate);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            MessageBox.Show(validation, FileToolsEnvironment.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Settings.FolderWrapFolderNameTemplate = wrapTemplate;
        Settings.FolderUnwrapMismatchFileNameTemplate = unwrapTemplate;
        Settings.FolderStructureConflictNameTemplate = conflictTemplate;
        if (_conflictPolicyCombo.SelectedItem is ComboOption<NameCollisionPolicy> policy)
        {
            Settings.FolderStructureConflictPolicy = policy.Value;
        }

        if (_indexStyleCombo.SelectedItem is ComboOption<ConflictIndexStyle> indexStyle)
        {
            Settings.FolderStructureConflictIndexStyle = indexStyle.Value;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ResetDefaults()
    {
        _wrapTemplateBox.Text = NameTemplateDefaults.FolderWrapFolderNameTemplate;
        _unwrapTemplateBox.Text = NameTemplateDefaults.FolderUnwrapPrefixFolderNameTemplate;
        _conflictTemplateBox.Text = NameTemplateDefaults.DefaultConflictNameTemplate;
        SelectComboValue(_conflictPolicyCombo, NameCollisionPolicy.Skip);
        SelectComboValue(_indexStyleCombo, ConflictIndexStyle.Number);
    }

    private void UpdatePreview()
    {
        var wrapTemplate = NormalizeTemplate(_wrapTemplateBox.Text, NameTemplateDefaults.FolderWrapFolderNameTemplate);
        var unwrapTemplate = NormalizeTemplate(_unwrapTemplateBox.Text, NameTemplateDefaults.FolderUnwrapPrefixFolderNameTemplate);
        var conflictTemplate = NormalizeTemplate(_conflictTemplateBox.Text, NameTemplateDefaults.DefaultConflictNameTemplate);
        var resolver = NameTemplateResolver.CreateDefault(Settings);
        var wrap = resolver.Evaluate(wrapTemplate, NameTemplateContext.FromFile(@"C:\Sample\Book 01.zip"));
        var unwrap = resolver.Evaluate(unwrapTemplate, NameTemplateContext.FromFolderChild(@"C:\Sample\FolderA", "Image01.jpg"));
        var policy = _conflictPolicyCombo.SelectedItem is ComboOption<NameCollisionPolicy> selectedPolicy
            ? selectedPolicy.Value
            : NameCollisionPolicy.Skip;
        var indexStyle = _indexStyleCombo.SelectedItem is ComboOption<ConflictIndexStyle> selectedStyle
            ? selectedStyle.Value
            : ConflictIndexStyle.Number;
        var conflictContext = NameTemplateContext.FromNameParts("Book.zip", "Book", ".zip") with
        {
            Index = 2,
            IndexLabel = ConflictIndexFormatter.Format(2, indexStyle)
        };
        var conflict = resolver.Evaluate(conflictTemplate, conflictContext);
        _previewLabel.Text = string.Join(
            Environment.NewLine,
            Localizer.Format("NameTemplatePreviewWrapFormat", FormatPreview(wrap)),
            Localizer.Format("NameTemplatePreviewUnwrapFormat", FormatPreview(unwrap)),
            Localizer.Format("NameTemplatePreviewConflictFormat", NameTemplateText.GetDisplayName(policy), FormatPreview(conflict)));
    }

    private string ValidateTemplates(string wrapTemplate, string unwrapTemplate, string conflictTemplate)
    {
        var resolver = NameTemplateResolver.CreateDefault(Settings);
        var validations = new[]
        {
            (Localizer.Get("LabelFolderWrapTemplate"), resolver.Evaluate(wrapTemplate, NameTemplateContext.FromFile(@"C:\Sample\Book 01.zip"))),
            (Localizer.Get("LabelFolderUnwrapTemplate"), resolver.Evaluate(unwrapTemplate, NameTemplateContext.FromFolderChild(@"C:\Sample\FolderA", "Image01.jpg"))),
            (Localizer.Get("LabelConflictNameTemplate"), resolver.Evaluate(
                conflictTemplate,
                NameTemplateContext.FromNameParts("Book.zip", "Book", ".zip") with
                {
                    Index = 2,
                    IndexLabel = ConflictIndexFormatter.Format(2, GetSelectedIndexStyle())
                }))
        };

        foreach (var (label, result) in validations)
        {
            if (!result.IsReady)
            {
                return Localizer.Format("NameTemplateValidationFailedFormat", label, result.Reason ?? result.Status.ToString());
            }
        }

        return "";
    }

    private ConflictIndexStyle GetSelectedIndexStyle()
    {
        return _indexStyleCombo.SelectedItem is ComboOption<ConflictIndexStyle> selectedStyle
            ? selectedStyle.Value
            : ConflictIndexStyle.Number;
    }

    private static string NormalizeTemplate(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string FormatPreview(NameTemplateEvaluationResult result)
    {
        return result.IsReady ? WindowsFileNameSafety.MakeSafeFileName(result.Value) : result.Status.ToString();
    }

    private static void SelectComboValue<T>(ComboBox combo, T value)
        where T : notnull
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboOption<T> option && EqualityComparer<T>.Default.Equals(option.Value, value))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }
}
