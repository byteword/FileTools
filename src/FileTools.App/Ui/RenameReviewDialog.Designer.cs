using System.Drawing;
using System.Windows.Forms;

#nullable enable

namespace FileTools;

internal sealed partial class RenameReviewDialog
{
    private const int CommonPhraseCollapsedRowHeight = 72;
    private const int TokenButtonMinWidth = 64;
    private const int TokenButtonMaxWidth = 180;
    private const int TokenButtonHeight = 28;
    private const int TokenButtonRightMargin = 6;
    private const int TokenButtonBottomMargin = 6;
    private const int FooterRowHeight = 56;
    private const int FooterButtonHeight = 32;
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _nextIssueButton = new();
    private readonly Button _skipButton = new();
    private readonly Button _ruleTraceButton = new();
    private readonly Button _useOriginalButton = new();
    private readonly Button _useAutomaticButton = new();
    private readonly DataGridView _grid = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _validationLabel = new();
    private readonly TextBox _originalNameBox = new();
    private readonly TextBox _suggestedNameBox = new();
    private readonly TextBox _titleBox = new();
    private readonly TextBox _episodeBox = new();
    private readonly TextBox _authorBox = new();
    private readonly TextBox _tagsBox = new();
    private readonly TextBox _extensionBox = new();
    private readonly FlowLayoutPanel _tokenPanel = new();
    private readonly FlowLayoutPanel _commonPhrasePanel = new();
    private readonly ToolTip _toolTip = new();
    private TableLayoutPanel? _selectedEditorPanel;
    private TextBox? _activeTextBox;
    private bool _commonPhrasesExpanded;

    private void BuildLayout(bool applyOnOk)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterRowHeight));
        Controls.Add(panel);

        panel.Controls.Add(BuildHeader(), 0, 0);
        panel.Controls.Add(BuildBody(), 0, 1);
        panel.Controls.Add(BuildButtons(applyOnOk), 0, 2);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("DialogRenameTitle"),
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(titleLabel, 0, 0);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.Font = new Font(Font, FontStyle.Bold);
        _summaryLabel.TextAlign = ContentAlignment.MiddleRight;
        header.Controls.Add(_summaryLabel, 1, 0);
        return header;
    }

    private Control BuildBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Controls.Add(BuildRowList(), 0, 0);
        body.Controls.Add(BuildSelectedEditor(), 1, 0);
        return body;
    }

    private Control BuildRowList()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0, 0, 10, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameEditorItems"),
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label, 0, 0);

        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.CellToolTipTextNeeded += (_, args) => SetCellToolTip(args);
        _grid.DataBindingComplete += (_, _) => ApplyRowStyles();
        _grid.SelectionChanged += (_, _) => SyncEditorFromSelection();
        _grid.CellDoubleClick += (_, _) => _suggestedNameBox.Focus();
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.Status),
            HeaderText = Localizer.Get("ColumnRenameStatus"),
            Width = 92
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.OriginalName),
            HeaderText = Localizer.Get("ColumnOriginalName"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 48
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RenameRow.SuggestedName),
            HeaderText = Localizer.Get("ColumnSuggestedName"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 52
        });
        _grid.DataSource = _rows;
        panel.Controls.Add(_grid, 0, 1);
        return panel;
    }

    private Control BuildSelectedEditor()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(2, 0, 0, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, CommonPhraseCollapsedRowHeight));
        _selectedEditorPanel = panel;

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameEditorSelectedItem"),
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(BuildNameComparison(), 0, 1);
        panel.Controls.Add(BuildPartsEditor(), 0, 2);
        panel.Controls.Add(BuildTokenPanel(), 0, 3);
        panel.Controls.Add(BuildCommonPhrasePanel(), 0, 4);
        return panel;
    }

    private Control BuildNameComparison()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        panel.Controls.Add(CreateFieldLabel(Localizer.Get("ColumnOriginalName")), 0, 0);
        _originalNameBox.Dock = DockStyle.Fill;
        _originalNameBox.ReadOnly = true;
        _originalNameBox.HideSelection = false;
        panel.Controls.Add(_originalNameBox, 1, 0);

        _useOriginalButton.Dock = DockStyle.Fill;
        _useOriginalButton.Text = Localizer.Get("ButtonUseOriginalName");
        _useOriginalButton.Click += (_, _) => UseOriginalName();
        panel.Controls.Add(_useOriginalButton, 2, 0);

        panel.Controls.Add(CreateFieldLabel(Localizer.Get("ColumnSuggestedName")), 0, 1);
        _suggestedNameBox.Dock = DockStyle.Fill;
        _suggestedNameBox.TextChanged += (_, _) => HandleSuggestedNameChanged();
        _suggestedNameBox.Leave += (_, _) => NormalizeSelectedRow();
        RegisterEditableTextBox(_suggestedNameBox);
        panel.Controls.Add(_suggestedNameBox, 1, 1);

        _useAutomaticButton.Dock = DockStyle.Fill;
        _useAutomaticButton.Text = Localizer.Get("ButtonUseAutomaticName");
        _useAutomaticButton.Click += (_, _) => UseAutomaticName();
        panel.Controls.Add(_useAutomaticButton, 2, 1);

        _validationLabel.Dock = DockStyle.Fill;
        _validationLabel.Padding = new Padding(4, 4, 4, 0);
        _validationLabel.TextAlign = ContentAlignment.MiddleLeft;
        panel.Controls.Add(_validationLabel, 1, 2);
        panel.SetColumnSpan(_validationLabel, 2);
        return panel;
    }

    private Control BuildPartsEditor()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameEditorExtractedParts"),
            Padding = new Padding(10, 8, 10, 10)
        };

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        panel.Controls.Add(CreateSmallLabel(Localizer.Get("RenameEditorTitle")), 0, 0);
        panel.Controls.Add(CreateSmallLabel(Localizer.Get("RenameEditorEpisode")), 1, 0);
        panel.Controls.Add(CreateSmallLabel(Localizer.Get("RenameEditorAuthor")), 2, 0);
        panel.Controls.Add(CreateSmallLabel(Localizer.Get("RenameEditorExtension")), 3, 0);

        _titleBox.Dock = DockStyle.Fill;
        _episodeBox.Dock = DockStyle.Fill;
        _authorBox.Dock = DockStyle.Fill;
        _extensionBox.Dock = DockStyle.Fill;
        _extensionBox.ReadOnly = true;
        _extensionBox.HideSelection = false;
        RegisterPartEditor(_titleBox);
        RegisterPartEditor(_episodeBox);
        RegisterPartEditor(_authorBox);
        panel.Controls.Add(_titleBox, 0, 1);
        panel.Controls.Add(_episodeBox, 1, 1);
        panel.Controls.Add(_authorBox, 2, 1);
        panel.Controls.Add(_extensionBox, 3, 1);

        panel.Controls.Add(CreateSmallLabel(Localizer.Get("RenameEditorTags")), 0, 2);
        _tagsBox.Dock = DockStyle.Fill;
        RegisterPartEditor(_tagsBox);
        panel.Controls.Add(_tagsBox, 0, 3);
        panel.SetColumnSpan(_tagsBox, 4);

        group.Controls.Add(panel);
        return group;
    }

    private Control BuildTokenPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameEditorInsertTokens"),
            Padding = new Padding(10, 8, 10, 10)
        };

        _tokenPanel.Dock = DockStyle.Fill;
        _tokenPanel.AutoScroll = true;
        _tokenPanel.WrapContents = true;
        group.Controls.Add(_tokenPanel);
        return group;
    }

    private Control BuildCommonPhrasePanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = Localizer.Get("RenameEditorCommonPhrases"),
            Padding = new Padding(10, 8, 10, 10)
        };

        _commonPhrasePanel.Dock = DockStyle.Fill;
        _commonPhrasePanel.AutoScroll = false;
        _commonPhrasePanel.WrapContents = false;
        _commonPhrasePanel.Resize += (_, _) => RebuildCommonPhrasePanel();
        group.Controls.Add(_commonPhrasePanel);
        return group;
    }

    private Control BuildButtons(bool applyOnOk)
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 8, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));

        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _nextIssueButton.Text = Localizer.Get("ButtonNextIssue");
        _nextIssueButton.Width = 112;
        _nextIssueButton.Height = FooterButtonHeight;
        _nextIssueButton.Click += (_, _) => SelectNextIssue();
        leftButtons.Controls.Add(_nextIssueButton);

        _skipButton.Text = Localizer.Get("ButtonRenameSkip");
        _skipButton.Width = 86;
        _skipButton.Height = FooterButtonHeight;
        _skipButton.Click += (_, _) => SkipSelectedRow();
        leftButtons.Controls.Add(_skipButton);

        _ruleTraceButton.Text = Localizer.Get("ButtonRuleTrace");
        _ruleTraceButton.Width = 104;
        _ruleTraceButton.Height = FooterButtonHeight;
        _ruleTraceButton.Click += (_, _) => ShowSelectedRuleTrace();
        leftButtons.Controls.Add(_ruleTraceButton);
        footer.Controls.Add(leftButtons, 0, 0);

        var rightButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        footer.Controls.Add(rightButtons, 1, 0);

        _okButton.Text = applyOnOk ? Localizer.Get("ButtonApply") : "OK";
        _okButton.Width = 96;
        _okButton.Height = FooterButtonHeight;
        _okButton.Click += (_, _) => Confirm();
        rightButtons.Controls.Add(_okButton);

        _cancelButton.Text = Localizer.Get("ButtonCancel");
        _cancelButton.Width = 96;
        _cancelButton.Height = FooterButtonHeight;
        _cancelButton.DialogResult = DialogResult.Cancel;
        rightButtons.Controls.Add(_cancelButton);
        return footer;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateSmallLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.BottomLeft
        };
    }

    private void RegisterEditableTextBox(TextBox textBox)
    {
        textBox.HideSelection = false;
        textBox.Enter += (_, _) => _activeTextBox = textBox;
    }

    private void RegisterPartEditor(TextBox textBox)
    {
        RegisterEditableTextBox(textBox);
        textBox.TextChanged += (_, _) => HandlePartTextChanged();
    }

    private void RebuildTokenPanel(RenameRow? row)
    {
        _tokenPanel.Controls.Clear();
        if (row is null)
        {
            return;
        }

        foreach (var token in BuildTokens(row).Take(24))
        {
            AddTokenButton(_tokenPanel, token);
        }
    }

    private void RebuildCommonPhrasePanel()
    {
        _commonPhrasePanel.Controls.Clear();
        if (_commonPhrases.Length == 0)
        {
            _commonPhrasePanel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = Localizer.Get("RenameEditorNoCommonPhrases"),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 5, 0, 0)
            });
            return;
        }

        _commonPhrasePanel.AutoScroll = _commonPhrasesExpanded;
        _commonPhrasePanel.WrapContents = _commonPhrasesExpanded;

        var visiblePhrases = _commonPhrases.AsEnumerable();
        var hiddenCount = 0;
        if (!_commonPhrasesExpanded)
        {
            var unreservedCount = CountCommonPhrasesThatFit(0);
            if (unreservedCount < _commonPhrases.Length)
            {
                var toggleReserve = GetTokenButtonWidth(Localizer.Get("ButtonShowMoreCommonPhrases")) + TokenButtonRightMargin;
                var visibleCount = CountCommonPhrasesThatFit(toggleReserve);
                hiddenCount = _commonPhrases.Length - visibleCount;
                visiblePhrases = _commonPhrases.Take(visibleCount);
            }
        }

        foreach (var phrase in visiblePhrases)
        {
            AddTokenButton(_commonPhrasePanel, phrase);
        }

        if (_commonPhrasesExpanded)
        {
            AddCommonPhraseToggleButton(Localizer.Get("ButtonCollapseCommonPhrases"), Localizer.Get("ButtonCollapseCommonPhrasesTooltip"));
        }
        else if (hiddenCount > 0)
        {
            AddCommonPhraseToggleButton(Localizer.Get("ButtonShowMoreCommonPhrases"), Localizer.Get("ButtonShowMoreCommonPhrasesTooltip"));
        }
    }

    private void AddTokenButton(FlowLayoutPanel panel, string value)
    {
        var width = GetTokenButtonWidth(value);
        var button = new NonFocusableButton
        {
            Text = value,
            AutoEllipsis = true,
            Width = width,
            Height = TokenButtonHeight,
            Margin = new Padding(0, 0, TokenButtonRightMargin, TokenButtonBottomMargin)
        };
        _toolTip.SetToolTip(button, value);
        button.Click += (_, _) => InsertToken(value);
        panel.Controls.Add(button);
    }

    private void AddCommonPhraseToggleButton(string text, string toolTip)
    {
        var button = new NonFocusableButton
        {
            Text = text,
            AutoEllipsis = true,
            Width = GetTokenButtonWidth(text),
            Height = TokenButtonHeight,
            Margin = new Padding(0, 0, TokenButtonRightMargin, TokenButtonBottomMargin)
        };
        _toolTip.SetToolTip(button, toolTip);
        button.Click += (_, _) => ToggleCommonPhraseExpansion();
        _commonPhrasePanel.Controls.Add(button);
    }

    private int CountCommonPhrasesThatFit(int reservedWidth)
    {
        var panelWidth = _commonPhrasePanel.ClientSize.Width;
        if (panelWidth <= 0)
        {
            panelWidth = Math.Max(240, ClientSize.Width - 460);
        }

        var availableWidth = Math.Max(0, panelWidth - reservedWidth);
        var usedWidth = 0;
        var count = 0;
        foreach (var phrase in _commonPhrases)
        {
            var itemWidth = GetTokenButtonWidth(phrase) + TokenButtonRightMargin;
            if (count > 0 && usedWidth + itemWidth > availableWidth)
            {
                break;
            }

            if (count == 0 && itemWidth > availableWidth)
            {
                return availableWidth >= TokenButtonMinWidth ? 1 : 0;
            }

            usedWidth += itemWidth;
            count++;
        }

        return count;
    }

    private int GetTokenButtonWidth(string value)
    {
        return Math.Min(TokenButtonMaxWidth, Math.Max(TokenButtonMinWidth, TextRenderer.MeasureText(value, Font).Width + 24));
    }

    private void ToggleCommonPhraseExpansion()
    {
        _commonPhrasesExpanded = !_commonPhrasesExpanded;
        UpdateCommonPhraseRowHeight();
        RebuildCommonPhrasePanel();
    }

    private void UpdateCommonPhraseRowHeight()
    {
        var selectedEditorPanel = _selectedEditorPanel;
        if (selectedEditorPanel is null || selectedEditorPanel.RowStyles.Count < 5)
        {
            return;
        }

        var tokenRow = selectedEditorPanel.RowStyles[3];
        var phraseRow = selectedEditorPanel.RowStyles[4];
        if (_commonPhrasesExpanded)
        {
            tokenRow.SizeType = SizeType.Percent;
            tokenRow.Height = 50;
            phraseRow.SizeType = SizeType.Percent;
            phraseRow.Height = 50;
        }
        else
        {
            tokenRow.SizeType = SizeType.Percent;
            tokenRow.Height = 100;
            phraseRow.SizeType = SizeType.Absolute;
            phraseRow.Height = CommonPhraseCollapsedRowHeight;
        }

        selectedEditorPanel.PerformLayout();
    }

    private void SetCellToolTip(DataGridViewCellToolTipTextNeededEventArgs args)
    {
        if (args.RowIndex < 0 || args.ColumnIndex < 0 ||
            _grid.Rows[args.RowIndex].DataBoundItem is not RenameRow row)
        {
            return;
        }

        var columnName = _grid.Columns[args.ColumnIndex].DataPropertyName;
        args.ToolTipText = columnName switch
        {
            nameof(RenameRow.OriginalName) => row.Preview.OriginalPath,
            nameof(RenameRow.SuggestedName) => string.IsNullOrWhiteSpace(row.TargetPath)
                ? row.SuggestedName
                : row.TargetPath,
            nameof(RenameRow.Status) => string.IsNullOrWhiteSpace(row.ValidationMessage)
                ? row.Status
                : row.ValidationMessage,
            _ => ""
        };
    }
}
