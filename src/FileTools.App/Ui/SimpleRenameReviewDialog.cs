using System.Windows.Forms;

namespace FileTools;

internal sealed class SimpleRenameReviewDialog : Form
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly List<RenameItem> _items = [];
    private readonly bool _applyOnOk;
    private readonly ListBox _itemList = new();
    private readonly TextBox _originalNameBox = new();
    private readonly TextBox _suggestedNameBox = new();
    private readonly Label _validationLabel = new();
    private readonly Button _okButton = new();
    private bool _updatingEditor;

    public SimpleRenameReviewDialog(IEnumerable<RenamePreview> previews, bool applyOnOk)
    {
        _applyOnOk = applyOnOk;
        _items.AddRange(previews.Select(static preview => new RenameItem(preview)));

        Text = Localizer.Get("DialogRenameTitle");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(780, 320);
        MinimumSize = new Size(640, 260);
        MinimizeBox = false;
        ShowInTaskbar = false;

        BuildLayout();
        ValidateItems();
        if (_items.Count > 0)
        {
            _itemList.SelectedIndex = 0;
        }
    }

    public OperationResult Result { get; private set; } = new();

    public IReadOnlyList<RenamePreview> EditedPreviews { get; private set; } = [];

    private void BuildLayout()
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
            Text = Localizer.Get("SimpleRenameHeader"),
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        root.Controls.Add(CreateBody(), 0, 1);
        root.Controls.Add(CreateButtons(), 0, 2);

        AcceptButton = _okButton;
    }

    private Control CreateBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _items.Count > 1 ? 250 : 0));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _itemList.Dock = DockStyle.Fill;
        _itemList.IntegralHeight = false;
        _itemList.DisplayMember = nameof(RenameItem.DisplayText);
        _itemList.DataSource = _items;
        _itemList.SelectedIndexChanged += (_, _) => SyncEditorFromSelection();
        body.Controls.Add(_itemList, 0, 0);
        _itemList.Visible = _items.Count > 1;

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(_items.Count > 1 ? 12 : 0, 0, 0, 0)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        fields.Controls.Add(CreateLabel(Localizer.Get("ColumnOriginalName")), 0, 0);
        _originalNameBox.Dock = DockStyle.Fill;
        _originalNameBox.ReadOnly = true;
        _originalNameBox.HideSelection = false;
        fields.Controls.Add(_originalNameBox, 1, 0);

        fields.Controls.Add(CreateLabel(Localizer.Get("ColumnSuggestedName")), 0, 1);
        _suggestedNameBox.Dock = DockStyle.Fill;
        _suggestedNameBox.TextChanged += (_, _) => HandleSuggestedNameChanged();
        fields.Controls.Add(_suggestedNameBox, 1, 1);

        _validationLabel.Dock = DockStyle.Fill;
        _validationLabel.TextAlign = ContentAlignment.MiddleLeft;
        fields.Controls.Add(_validationLabel, 1, 2);
        body.Controls.Add(fields, 1, 0);
        return body;
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

        var advancedButton = new Button
        {
            Text = Localizer.Get("ButtonAdvanced"),
            Width = 92,
            Height = 30,
            Margin = new Padding(8, 0, 0, 0)
        };
        advancedButton.Click += (_, _) => OpenAdvancedEditor();

        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(advancedButton);
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

    private void SyncEditorFromSelection()
    {
        _updatingEditor = true;
        try
        {
            var item = SelectedItem;
            _originalNameBox.Text = item?.OriginalName ?? "";
            _suggestedNameBox.Text = item?.SuggestedName ?? "";
            _suggestedNameBox.Enabled = item is not null;
            _validationLabel.Text = item?.ValidationMessage ?? "";
            _validationLabel.ForeColor = item?.BlockingError == true ? Color.Firebrick : Color.FromArgb(55, 65, 81);
        }
        finally
        {
            _updatingEditor = false;
        }
    }

    private void HandleSuggestedNameChanged()
    {
        if (_updatingEditor || SelectedItem is not { } item)
        {
            return;
        }

        item.SuggestedName = _suggestedNameBox.Text;
        item.IsSkipped = false;
        ValidateItems();
    }

    private void OpenAdvancedEditor()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        var editedName = AdvancedNameEditDialog.EditName(
            this,
            Localizer.Get("AdvancedNameDialogTitle"),
            Localizer.Get("AdvancedNameRenameHeader"),
            new NameEditRequest(
                OriginalName: item.OriginalName,
                SuggestedName: item.SuggestedName,
                AutomaticName: item.AutomaticName,
                Recommendations: BuildRecommendations(item.Preview)));
        if (editedName is null)
        {
            return;
        }

        item.SuggestedName = editedName;
        item.IsSkipped = false;
        ValidateItems();
    }

    private void Confirm()
    {
        ValidateItems();
        var blockingItem = _items.FirstOrDefault(static item => item.BlockingError);
        if (blockingItem is not null)
        {
            _itemList.SelectedItem = blockingItem;
            SyncEditorFromSelection();
            return;
        }

        EditedPreviews = CreateEditedPreviews(allowBlocking: false);
        if (_applyOnOk)
        {
            Result = RenameOperations.Apply(EditedPreviews);
            if (Result.HasErrors)
            {
                MessageBox.Show(
                    Result.ToUserMessage(Localizer.Get("DialogRenameTitle")),
                    FileToolsEnvironment.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private IReadOnlyList<RenamePreview> CreateEditedPreviews(bool allowBlocking)
    {
        var previews = new List<RenamePreview>(_items.Count);
        foreach (var item in _items)
        {
            if (item.IsSkipped)
            {
                previews.Add(item.Preview with
                {
                    SuggestedFileName = item.OriginalName,
                    SuggestedPath = item.Preview.OriginalPath,
                    Status = RenamePreviewStatus.Skipped
                });
                continue;
            }

            var safeName = WindowsFileNameSafety.MakeSafeFileName(item.SuggestedName.Trim());
            var suggestedPath = Path.Combine(Path.GetDirectoryName(item.Preview.OriginalPath) ?? "", safeName);
            var status = item.BlockingError && !allowBlocking
                ? RenamePreviewStatus.Conflict
                : PathComparer.Equals(item.Preview.OriginalPath, suggestedPath)
                    ? RenamePreviewStatus.Unchanged
                    : RenamePreviewStatus.Ready;
            previews.Add(item.Preview with
            {
                SuggestedFileName = safeName,
                SuggestedPath = suggestedPath,
                Status = status
            });
        }

        return previews;
    }

    private void ValidateItems()
    {
        var targetGroups = new Dictionary<string, List<RenameItem>>(PathComparer);
        foreach (var item in _items)
        {
            item.BlockingError = false;
            item.ValidationMessage = "";
            item.TargetPath = item.Preview.OriginalPath;

            if (item.IsSkipped)
            {
                item.ValidationMessage = Localizer.Get("RenameStatusSkipped");
                continue;
            }

            var suggestedName = item.SuggestedName.Trim();
            var safeName = WindowsFileNameSafety.MakeSafeFileName(suggestedName);
            if (string.IsNullOrWhiteSpace(suggestedName) ||
                suggestedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !string.Equals(suggestedName, safeName, StringComparison.Ordinal))
            {
                item.BlockingError = true;
                item.ValidationMessage = Localizer.Get("RenameInvalidNameMessage");
                continue;
            }

            var directory = Path.GetDirectoryName(item.Preview.OriginalPath) ?? "";
            item.TargetPath = Path.Combine(directory, safeName);
            if (!targetGroups.TryGetValue(item.TargetPath, out var group))
            {
                group = [];
                targetGroups.Add(item.TargetPath, group);
            }

            group.Add(item);
        }

        foreach (var item in _items.Where(static item => !item.BlockingError && !item.IsSkipped))
        {
            var hasDuplicateTarget = targetGroups.TryGetValue(item.TargetPath, out var group) && group.Count > 1;
            var targetExists = !PathComparer.Equals(item.Preview.OriginalPath, item.TargetPath) &&
                (File.Exists(item.TargetPath) || Directory.Exists(item.TargetPath));
            if (hasDuplicateTarget || targetExists)
            {
                item.BlockingError = true;
                item.ValidationMessage = hasDuplicateTarget
                    ? Localizer.Get("RenameDuplicateNameMessage")
                    : Localizer.Format("PlanPreviewTargetExistsFormat", item.TargetPath);
                continue;
            }

            item.ValidationMessage = PathComparer.Equals(item.Preview.OriginalPath, item.TargetPath)
                ? Localizer.Get("RenameStatusUnchanged")
                : Localizer.Get("RenameStatusReady");
        }

        _okButton.Enabled = _items.Count > 0 && _items.All(static item => !item.BlockingError);
        SyncEditorFromSelection();
        _itemList.Refresh();
    }

    private RenameItem? SelectedItem => _itemList.SelectedItem as RenameItem;

    private static IReadOnlyList<string> BuildRecommendations(RenamePreview preview)
    {
        var values = new List<string>();
        AddIfNotBlank(Path.GetFileNameWithoutExtension(preview.OriginalFileName));
        AddIfNotBlank(preview.Parts.Title);
        AddIfNotBlank(preview.Parts.EpisodeRange);
        AddIfNotBlank(preview.Parts.Author);
        foreach (var tag in preview.Parts.Tags)
        {
            AddIfNotBlank(tag);
        }

        foreach (var candidate in preview.Candidates)
        {
            AddIfNotBlank(candidate.Value);
            AddIfNotBlank(Path.GetFileNameWithoutExtension(candidate.Value));
        }

        return values;

        void AddIfNotBlank(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }
    }

    private sealed class RenameItem
    {
        public RenameItem(RenamePreview preview)
        {
            Preview = preview;
            OriginalName = preview.OriginalFileName;
            AutomaticName = preview.SuggestedFileName;
            SuggestedName = preview.SuggestedFileName;
        }

        public RenamePreview Preview { get; }

        public string OriginalName { get; }

        public string AutomaticName { get; }

        public string SuggestedName { get; set; }

        public string TargetPath { get; set; } = "";

        public string ValidationMessage { get; set; } = "";

        public bool BlockingError { get; set; }

        public bool IsSkipped { get; set; }

        public string DisplayText => BlockingError
            ? $"{OriginalName} - {Localizer.Get("RenameStatusConflict")}"
            : OriginalName;
    }
}
