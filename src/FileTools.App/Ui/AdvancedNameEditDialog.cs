using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FileTools;

internal sealed record NameEditRequest(
    string OriginalName,
    string SuggestedName,
    string AutomaticName,
    string? RequiredExtension = null);

internal sealed class AdvancedNameEditDialog : Form
{
    private readonly NameEditRequest _request;
    private readonly IReadOnlyList<string> _recommendations;
    private readonly TextBox _originalNameBox = new();
    private readonly TextBox _suggestedNameBox = new();
    private readonly Label _validationLabel = new();
    private readonly FlowLayoutPanel _recommendationPanel = new();
    private readonly Button _okButton = new();
    private bool _updatingEditor;

    private AdvancedNameEditDialog(string title, string header, NameEditRequest request)
    {
        _request = request;
        _recommendations = BuildRecommendationsForName(request.OriginalName, request.RequiredExtension);

        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 420);
        MinimumSize = new Size(620, 320);
        MinimizeBox = false;
        ShowInTaskbar = false;

        BuildLayout(header);
        LoadRequest();
        ValidateName();
    }

    public string ResultName { get; private set; } = "";

    public static string? EditName(
        IWin32Window owner,
        string title,
        string header,
        NameEditRequest request)
    {
        using var dialog = new AdvancedNameEditDialog(title, header, request);
        return dialog.ShowDialog(owner) == DialogResult.OK
            ? dialog.ResultName
            : null;
    }

    private void BuildLayout(string headerText)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = headerText,
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        root.Controls.Add(CreateBody(), 0, 1);
        root.Controls.Add(CreateButtons(), 0, 2);

        AcceptButton = _okButton;
    }

    private Control CreateBody()
    {
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        fields.Controls.Add(CreateLabel(Localizer.Get("AdvancedNameOriginal")), 0, 0);
        _originalNameBox.Dock = DockStyle.Fill;
        _originalNameBox.ReadOnly = true;
        _originalNameBox.HideSelection = false;
        fields.Controls.Add(_originalNameBox, 1, 0);

        fields.Controls.Add(CreateLabel(Localizer.Get("AdvancedNameSuggested")), 0, 1);
        fields.Controls.Add(CreateNameEditRow(), 1, 1);

        _validationLabel.Dock = DockStyle.Fill;
        _validationLabel.TextAlign = ContentAlignment.MiddleLeft;
        fields.Controls.Add(_validationLabel, 1, 2);

        fields.Controls.Add(CreateLabel(Localizer.Get("AdvancedNameRecommendations")), 0, 3);
        _recommendationPanel.Dock = DockStyle.Fill;
        _recommendationPanel.AutoScroll = true;
        _recommendationPanel.WrapContents = true;
        fields.Controls.Add(_recommendationPanel, 1, 3);
        fields.SetRowSpan(_recommendationPanel, 2);

        return fields;
    }

    private Control CreateNameEditRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));

        _suggestedNameBox.Dock = DockStyle.Fill;
        _suggestedNameBox.TextChanged += (_, _) => ValidateName();
        row.Controls.Add(_suggestedNameBox, 0, 0);

        var originalButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("ButtonRestoreOriginalName"),
            Margin = new Padding(8, 0, 0, 0)
        };
        originalButton.Click += (_, _) => UseOriginalName();
        row.Controls.Add(originalButton, 1, 0);

        var automaticButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("ButtonUseAutomaticCorrection"),
            Margin = new Padding(8, 0, 0, 0)
        };
        automaticButton.Click += (_, _) => UseAutomaticName();
        row.Controls.Add(automaticButton, 2, 0);

        return row;
    }

    private Control CreateButtons()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };

        var cancelButton = new Button
        {
            Text = Localizer.Get("ButtonCancel"),
            DialogResult = DialogResult.Cancel,
            Width = 92,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        _okButton.Text = Localizer.Get("ButtonOK");
        _okButton.Width = 92;
        _okButton.Height = 30;
        _okButton.Click += (_, _) => Confirm();

        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(_okButton);
        CancelButton = cancelButton;
        return buttons;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private void LoadRequest()
    {
        _updatingEditor = true;
        try
        {
            _originalNameBox.Text = _request.OriginalName;
            _suggestedNameBox.Text = _request.SuggestedName;
            RebuildRecommendationButtons();
        }
        finally
        {
            _updatingEditor = false;
        }
    }

    private void RebuildRecommendationButtons()
    {
        _recommendationPanel.Controls.Clear();
        if (_recommendations.Count == 0)
        {
            _recommendationPanel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = Localizer.Get("AdvancedNameNoRecommendations"),
                ForeColor = Color.FromArgb(71, 85, 105),
                Margin = new Padding(0, 4, 0, 0)
            });
            return;
        }

        foreach (var recommendation in _recommendations)
        {
            var button = new Button
            {
                AutoSize = true,
                AutoEllipsis = true,
                Text = recommendation,
                Tag = recommendation,
                Height = 28,
                MaximumSize = new Size(240, 28),
                Margin = new Padding(0, 0, 8, 8)
            };
            button.Click += (_, _) => InsertRecommendation(recommendation);
            _recommendationPanel.Controls.Add(button);
        }
    }

    private void InsertRecommendation(string value)
    {
        var selectionStart = _suggestedNameBox.SelectionStart;
        _suggestedNameBox.Text = _suggestedNameBox.Text
            .Remove(selectionStart, _suggestedNameBox.SelectionLength)
            .Insert(selectionStart, value);
        _suggestedNameBox.SelectionStart = selectionStart + value.Length;
        _suggestedNameBox.Focus();
    }

    private void UseAutomaticName()
    {
        var correctedName = CreateAutomaticCorrectionForName(_suggestedNameBox.Text, _request.RequiredExtension)
            ?? CreateAutomaticCorrectionForName(_request.OriginalName, _request.RequiredExtension)
            ?? _request.AutomaticName;
        _suggestedNameBox.Text = correctedName;
        _suggestedNameBox.SelectAll();
        _suggestedNameBox.Focus();
    }

    private void UseOriginalName()
    {
        _suggestedNameBox.Text = _request.OriginalName;
        _suggestedNameBox.SelectAll();
        _suggestedNameBox.Focus();
    }

    private void Confirm()
    {
        var normalized = ValidateName();
        if (normalized is null)
        {
            return;
        }

        ResultName = normalized;
        DialogResult = DialogResult.OK;
        Close();
    }

    private string? ValidateName()
    {
        if (_updatingEditor)
        {
            return null;
        }

        var suggestedName = _suggestedNameBox.Text.Trim();
        var safeName = WindowsFileNameSafety.MakeSafeFileName(suggestedName);
        var normalizedName = EnsureRequiredExtension(safeName, _request.RequiredExtension);
        var isInvalid = string.IsNullOrWhiteSpace(suggestedName) ||
            suggestedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(suggestedName, safeName, StringComparison.Ordinal);

        _okButton.Enabled = !isInvalid;
        _validationLabel.ForeColor = isInvalid ? Color.Firebrick : Color.FromArgb(55, 65, 81);
        _validationLabel.Text = isInvalid
            ? Localizer.Get("RenameInvalidNameMessage")
            : Localizer.Get("RenameStatusReady");

        return isInvalid ? null : normalizedName;
    }

    private static string EnsureRequiredExtension(string safeName, string? requiredExtension)
    {
        if (!string.IsNullOrWhiteSpace(requiredExtension) &&
            !safeName.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
        {
            safeName += requiredExtension;
        }

        return safeName;
    }

    internal static IReadOnlyList<string> BuildRecommendationsForName(string originalName, string? requiredExtension = null)
    {
        var sourceName = StripRequiredExtension(originalName.Trim(), requiredExtension);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recommendations = new List<string>();

        void Add(string? value)
        {
            var normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
            {
                return;
            }

            recommendations.Add(normalized);
        }

        foreach (Match match in BracketTokenRegex.Matches(sourceName))
        {
            Add(match.Value);
            Add(match.Value.Trim('[', ']', '(', ')', '{', '}'));
        }

        foreach (Match match in EpisodeRangeTokenRegex.Matches(sourceName))
        {
            Add(match.Value);
        }

        foreach (Match match in DateTokenRegex.Matches(sourceName))
        {
            Add(match.Value);
        }

        foreach (var token in sourceName.Split(
            [' ', '\t', '\r', '\n', '_', '-', ',', ';', '[', ']', '(', ')', '{', '}', '~', '.'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Add(token);
        }

        if (recommendations.Count == 0)
        {
            Add(sourceName);
        }

        return recommendations;
    }

    internal static string? CreateAutomaticCorrectionForName(string name, string? requiredExtension = null)
    {
        var sourceName = name.Trim();
        if (sourceName.Length == 0)
        {
            return null;
        }

        var suffix = GetRequiredExtensionSuffix(sourceName, requiredExtension);
        var nameWithoutSuffix = suffix.Length == 0
            ? sourceName
            : sourceName[..^suffix.Length];
        var normalized = KoreanJamoNormalizer.Normalize(nameWithoutSuffix);
        var yaminCandidate = new ObfuscatedHangulCandidateGenerator()
            .Generate(normalized)
            .FirstOrDefault()?.Value;
        var corrected = yaminCandidate ?? normalized;
        if (string.Equals(corrected, nameWithoutSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        return corrected + suffix;
    }

    private static string StripRequiredExtension(string name, string? requiredExtension)
    {
        var suffix = GetRequiredExtensionSuffix(name, requiredExtension);
        return suffix.Length == 0 ? name : name[..^suffix.Length];
    }

    private static string GetRequiredExtensionSuffix(string name, string? requiredExtension)
    {
        return !string.IsNullOrWhiteSpace(requiredExtension) &&
            name.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase)
            ? name[^requiredExtension.Length..]
            : "";
    }

    private static readonly Regex BracketTokenRegex = new(@"(\[[^\]]+\]|\([^)]+\)|\{[^}]+\})", RegexOptions.Compiled);

    private static readonly Regex EpisodeRangeTokenRegex = new(
        @"\b\d+\s*(?:권|화|회)\s*[-~]\s*\d+\s*(?:권|화|회)\b",
        RegexOptions.Compiled);

    private static readonly Regex DateTokenRegex = new(@"\b\d{1,4}[.]\d{1,2}(?:[.]\d{1,2})?\b", RegexOptions.Compiled);
}
