using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FileTools;

internal sealed partial class RenameReviewDialog : Form
{

    private static readonly Regex CandidateBracketMetadataRegex = new(
        @"\[[^\]\r\n]{1,80}\]|\([^\)\r\n]{1,80}\)|\{[^\}\r\n]{1,80}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CandidateEpisodeSuffixRegex = new(
        @"(?:^|[\s._~-])(?:제\s*)?\d+(?:\.\d+)?(?:\s*[-~]\s*\d+(?:\.\d+)?)?\s*(?:화|회|권|권째|부|편|장|vol(?:ume)?|v|ep(?:isode)?|ch(?:apter)?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CandidateWhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly char[] CandidateTitleTrimChars = [' ', '\t', '\r', '\n', '_', '-', '.', ',', '~'];

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly BindingList<RenameRow> _rows = [];
    private readonly string[] _commonPhrases;
    private readonly bool _applyOnOk;

    private RenameRow? _selectedRow;
    private bool _updatingRows;
    private bool _updatingEditor;

    public OperationResult Result { get; private set; } = new();

    private RenameReviewDialog(IEnumerable<RenamePreview> previews, bool applyOnOk)
    {
        _applyOnOk = applyOnOk;
        _commonPhrases = LoadCommonPhrases();

        Text = Localizer.Get("DialogRenameTitle");
        StartPosition = FormStartPosition.CenterParent;
        Width = 1180;
        Height = 680;
        MinimumSize = new Size(960, 540);
        MinimizeBox = false;

        BuildLayout(applyOnOk);
        LoadRows(previews);
        ValidateRows();
        SelectInitialRow();
    }

    public static OperationResult ShowAndApply(IEnumerable<string> paths, FileToolsSettings settings)
    {
        var previews = RenameOperations.CreatePlan(paths, settings);
        if (settings.RenameReviewMode == RenameReviewMode.IssuesOnly &&
            !previews.Any(static preview => preview.Status is RenamePreviewStatus.NeedsReview or RenamePreviewStatus.Conflict))
        {
            return RenameOperations.Apply(previews);
        }

        using var dialog = new RenameReviewDialog(previews, applyOnOk: true);
        return dialog.ShowDialog() == DialogResult.OK ? dialog.Result : new OperationResult();
    }

    public static bool EditPlanStep(IWin32Window owner, string path, WorkPlanStep step, FileToolsSettings settings)
    {
        using var dialog = new RenameReviewDialog(RenameOperations.CreatePlan([path], settings), applyOnOk: false);
        if (!string.IsNullOrWhiteSpace(step.ManualRenameFileName) && dialog._rows.Count > 0)
        {
            var row = dialog._rows[0];
            row.SuggestedName = step.ManualRenameFileName;
            row.UserEdited = true;
            dialog.NormalizeEditedRow(row);
            dialog.ValidateRows();
            dialog.SelectRow(row);
        }

        if (dialog.ShowDialog(owner) != DialogResult.OK || dialog._rows.Count == 0)
        {
            return false;
        }

        var editedRow = dialog._rows[0];
        step.ManualRenameFileName = editedRow.IsSkipped
            ? editedRow.OriginalName
            : WindowsFileNameSafety.MakeSafeFileName(editedRow.SuggestedName.Trim());
        return true;
    }

    public static IReadOnlyDictionary<string, string>? EditPlanSteps(
        IWin32Window owner,
        IEnumerable<string> paths,
        FileToolsSettings settings)
    {
        using var dialog = new RenameReviewDialog(RenameOperations.CreatePlan(paths, settings), applyOnOk: false);
        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return null;
        }

        return dialog._rows
            .Where(static row => !row.IsSkipped)
            .Where(static row => !PathComparer.Equals(row.Preview.OriginalPath, row.TargetPath))
            .ToDictionary(
                static row => row.Preview.OriginalPath,
                static row => WindowsFileNameSafety.MakeSafeFileName(row.SuggestedName.Trim()),
                PathComparer);
    }

    private void LoadRows(IEnumerable<RenamePreview> previews)
    {
        foreach (var preview in previews.OrderBy(GetInitialSortPriority))
        {
            _rows.Add(new RenameRow(preview)
            {
                OriginalName = preview.OriginalFileName,
                SuggestedName = preview.SuggestedFileName
            });
        }
    }

    private void SelectInitialRow()
    {
        var row = _rows.FirstOrDefault(static row => IsIssueState(row.State)) ?? _rows.FirstOrDefault();
        if (row is not null)
        {
            SelectRow(row);
        }
        else
        {
            SyncEditorFromRow(null);
        }
    }

    private void SelectRow(RenameRow row)
    {
        for (var index = 0; index < _grid.Rows.Count; index++)
        {
            if (!ReferenceEquals(_grid.Rows[index].DataBoundItem, row))
            {
                continue;
            }

            _grid.ClearSelection();
            _grid.Rows[index].Selected = true;
            _grid.CurrentCell = _grid.Rows[index].Cells[0];
            SyncEditorFromRow(row);
            return;
        }

        SyncEditorFromRow(row);
    }

    private void SyncEditorFromSelection()
    {
        if (_grid.CurrentRow?.DataBoundItem is RenameRow row)
        {
            SyncEditorFromRow(row);
            return;
        }

        SyncEditorFromRow(null);
    }

    private void SyncEditorFromRow(RenameRow? row)
    {
        _selectedRow = row;
        SyncEditorFieldsFromRow(row);
        RebuildTokenPanel(row);
        RebuildCommonPhrasePanel();
        UpdateSelectedValidation();
        UpdateCommandState();
    }

    private void SyncEditorFieldsFromRow(RenameRow? row)
    {
        _updatingEditor = true;
        try
        {
            var hasRow = row is not null;
            _originalNameBox.Enabled = hasRow;
            _suggestedNameBox.Enabled = hasRow;
            _titleBox.Enabled = hasRow;
            _episodeBox.Enabled = hasRow;
            _authorBox.Enabled = hasRow;
            _tagsBox.Enabled = hasRow;
            _extensionBox.Enabled = hasRow;
            _useOriginalButton.Enabled = hasRow;
            _useAutomaticButton.Enabled = hasRow;

            _originalNameBox.Text = row?.OriginalName ?? "";
            _suggestedNameBox.Text = row?.SuggestedName ?? "";
            _titleBox.Text = row?.Draft.Title ?? "";
            _episodeBox.Text = row?.Draft.EpisodeRange ?? "";
            _authorBox.Text = row?.Draft.Author ?? "";
            _tagsBox.Text = row?.Draft.TagsText ?? "";
            _extensionBox.Text = row?.Draft.Extension ?? "";
        }
        finally
        {
            _updatingEditor = false;
        }
    }

    private void HandleSuggestedNameChanged()
    {
        if (_updatingEditor || _selectedRow is null)
        {
            return;
        }

        _selectedRow.UserEdited = true;
        _selectedRow.SuggestedName = _suggestedNameBox.Text;
        ValidateRows();
    }

    private void HandlePartTextChanged()
    {
        if (_updatingEditor || _selectedRow is null)
        {
            return;
        }

        _selectedRow.UserEdited = true;
        _selectedRow.Draft.Title = _titleBox.Text;
        _selectedRow.Draft.EpisodeRange = _episodeBox.Text;
        _selectedRow.Draft.Author = _authorBox.Text;
        _selectedRow.Draft.TagsText = _tagsBox.Text;

        var composed = WindowsFileNameSafety.MakeSafeFileName(_selectedRow.Draft.Compose());
        _selectedRow.SuggestedName = composed;

        _updatingEditor = true;
        try
        {
            _suggestedNameBox.Text = composed;
        }
        finally
        {
            _updatingEditor = false;
        }

        ValidateRows();
    }

    private void UseOriginalName()
    {
        if (_selectedRow is null)
        {
            return;
        }

        SetSelectedSuggestedName(_selectedRow.OriginalName, userEdited: true, resetDraft: false);
    }

    private void UseAutomaticName()
    {
        if (_selectedRow is null)
        {
            return;
        }

        SetSelectedSuggestedName(_selectedRow.AutomaticName, userEdited: true, resetDraft: true);
    }

    private void SkipSelectedRow()
    {
        if (_selectedRow is null)
        {
            return;
        }

        _selectedRow.SetSkipped();
        SyncEditorFromRow(_selectedRow);
        ValidateRows();
    }

    private void ShowSelectedRuleTrace()
    {
        if (_selectedRow is null)
        {
            return;
        }

        var lines = BuildRuleTraceLines(_selectedRow.Preview).ToArray();
        if (lines.Length == 0)
        {
            ShowValidation(Localizer.Get("RenameRuleTraceEmptyMessage"));
            return;
        }

        MessageBox.Show(
            string.Join(Environment.NewLine, lines),
            Localizer.Get("DialogRenameRuleTraceTitle"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void SetSelectedSuggestedName(string fileName, bool userEdited, bool resetDraft)
    {
        if (_selectedRow is null)
        {
            return;
        }

        if (resetDraft)
        {
            _selectedRow.ResetDraft();
        }

        _selectedRow.UserEdited = userEdited;
        _selectedRow.SuggestedName = fileName;
        SyncEditorFromRow(_selectedRow);
        ValidateRows();
        _suggestedNameBox.Focus();
        _suggestedNameBox.SelectAll();
    }

    private void InsertToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var target = _activeTextBox;
        if (target is null || target.ReadOnly || !target.Enabled)
        {
            target = _suggestedNameBox;
        }

        var selectionStart = target.SelectionStart;
        target.Text = target.Text.Remove(selectionStart, target.SelectionLength).Insert(selectionStart, value);
        target.SelectionStart = selectionStart + value.Length;
        target.SelectionLength = 0;
        target.Focus();
    }

    private void NormalizeSelectedRow()
    {
        var row = _selectedRow;
        if (row is null)
        {
            return;
        }

        NormalizeEditedRow(row);
        SyncSuggestedNameBoxFromRow(row);
        ValidateRows();
    }

    private void SyncSuggestedNameBoxFromRow(RenameRow row)
    {
        if (string.Equals(_suggestedNameBox.Text, row.SuggestedName, StringComparison.Ordinal))
        {
            return;
        }

        var selectionStart = Math.Min(_suggestedNameBox.SelectionStart, row.SuggestedName.Length);
        var selectionLength = Math.Min(_suggestedNameBox.SelectionLength, row.SuggestedName.Length - selectionStart);

        _updatingEditor = true;
        try
        {
            _suggestedNameBox.Text = row.SuggestedName;
            _suggestedNameBox.SelectionStart = selectionStart;
            _suggestedNameBox.SelectionLength = selectionLength;
        }
        finally
        {
            _updatingEditor = false;
        }
    }

    private void NormalizeEditedRow(RenameRow row)
    {
        var suggestedName = row.SuggestedName.Trim();
        if (string.IsNullOrWhiteSpace(suggestedName))
        {
            row.SuggestedName = suggestedName;
            return;
        }

        var safeName = WindowsFileNameSafety.MakeSafeFileName(suggestedName);
        if (!string.Equals(suggestedName, safeName, StringComparison.Ordinal))
        {
            row.SuggestedName = safeName;
        }
    }

    private void ValidateRows()
    {
        if (_updatingRows)
        {
            return;
        }

        _updatingRows = true;
        try
        {
            var targetGroups = new Dictionary<string, List<RenameRow>>(PathComparer);
            foreach (var row in _rows)
            {
                row.BlockingError = false;
                row.ValidationMessage = "";
                row.TargetPath = "";

                if (row.IsSkipped)
                {
                    row.State = RenameRowState.Skipped;
                    row.TargetPath = row.Preview.OriginalPath;
                    row.Status = Localizer.Get("RenameStatusSkipped");
                    continue;
                }

                var suggestedName = row.SuggestedName.Trim();
                if (string.IsNullOrWhiteSpace(suggestedName) ||
                    suggestedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    row.State = RenameRowState.Invalid;
                    row.BlockingError = true;
                    row.Status = Localizer.Get("RenameStatusInvalid");
                    row.ValidationMessage = Localizer.Get("RenameInvalidNameMessage");
                    continue;
                }

                row.State = RenameRowState.Ready;
                var safeName = WindowsFileNameSafety.MakeSafeFileName(suggestedName);
                var directory = Path.GetDirectoryName(row.Preview.OriginalPath) ?? "";
                row.TargetPath = Path.Combine(directory, safeName);
                if (!targetGroups.TryGetValue(row.TargetPath, out var group))
                {
                    group = [];
                    targetGroups.Add(row.TargetPath, group);
                }

                group.Add(row);
            }

            foreach (var row in _rows.Where(static row =>
                row.State != RenameRowState.Invalid &&
                row.State != RenameRowState.Skipped))
            {
                var hasDuplicateTarget = targetGroups.TryGetValue(row.TargetPath, out var group) && group.Count > 1;
                var targetExists = !PathComparer.Equals(row.Preview.OriginalPath, row.TargetPath) &&
                    (File.Exists(row.TargetPath) || Directory.Exists(row.TargetPath));

                if (hasDuplicateTarget || targetExists)
                {
                    row.State = RenameRowState.Conflict;
                    row.BlockingError = true;
                    row.Status = Localizer.Get("RenameStatusConflict");
                    row.ValidationMessage = hasDuplicateTarget
                        ? Localizer.Get("RenameDuplicateNameMessage")
                        : Localizer.Format("PlanPreviewTargetExistsFormat", row.TargetPath);
                    continue;
                }

                if (row.Preview.Status == RenamePreviewStatus.Conflict)
                {
                    row.State = row.UserEdited ? RenameRowState.Resolved : RenameRowState.Conflict;
                    row.Status = row.UserEdited
                        ? Localizer.Get("RenameStatusResolved")
                        : Localizer.Get("RenameStatusConflict");
                    continue;
                }

                if (row.Preview.Status == RenamePreviewStatus.NeedsReview && !row.UserEdited)
                {
                    row.State = RenameRowState.NeedsReview;
                    row.Status = Localizer.Get("RenameStatusNeedsReview");
                    continue;
                }

                if (PathComparer.Equals(row.Preview.OriginalPath, row.TargetPath))
                {
                    row.State = RenameRowState.Unchanged;
                    row.Status = Localizer.Get("RenameStatusUnchanged");
                    continue;
                }

                row.State = RenameRowState.Ready;
                row.Status = Localizer.Get("RenameStatusReady");
            }
        }
        finally
        {
            _updatingRows = false;
        }

        ApplyRowStyles();
        UpdateSummary();
        UpdateCommandState();
        UpdateSelectedValidation();
    }

    private void ApplyRowStyles()
    {
        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is not RenameRow row)
            {
                continue;
            }

            var style = gridRow.DefaultCellStyle;
            style.SelectionForeColor = SystemColors.HighlightText;
            switch (row.State)
            {
                case RenameRowState.Invalid:
                    style.BackColor = Color.FromArgb(255, 232, 232);
                    style.ForeColor = Color.FromArgb(128, 23, 23);
                    break;
                case RenameRowState.Conflict:
                    style.BackColor = row.BlockingError
                        ? Color.FromArgb(255, 232, 232)
                        : Color.FromArgb(255, 243, 205);
                    style.ForeColor = row.BlockingError
                        ? Color.FromArgb(128, 23, 23)
                        : Color.FromArgb(112, 73, 0);
                    break;
                case RenameRowState.NeedsReview:
                    style.BackColor = Color.FromArgb(255, 249, 219);
                    style.ForeColor = Color.FromArgb(86, 65, 0);
                    break;
                case RenameRowState.Resolved:
                    style.BackColor = Color.FromArgb(226, 246, 232);
                    style.ForeColor = Color.FromArgb(18, 92, 54);
                    break;
                case RenameRowState.Unchanged:
                case RenameRowState.Skipped:
                    style.BackColor = Color.FromArgb(245, 246, 248);
                    style.ForeColor = Color.FromArgb(93, 99, 108);
                    break;
                default:
                    style.BackColor = Color.White;
                    style.ForeColor = SystemColors.ControlText;
                    break;
            }
        }
    }

    private void UpdateSummary()
    {
        var totalCount = _rows.Count;
        var changeCount = _rows.Count(static row =>
            row.State != RenameRowState.Invalid &&
            !PathComparer.Equals(row.Preview.OriginalPath, row.TargetPath));
        var reviewCount = _rows.Count(static row => row.State == RenameRowState.NeedsReview);
        var conflictCount = _rows.Count(static row => row.State == RenameRowState.Conflict);
        var resolvedCount = _rows.Count(static row => row.State == RenameRowState.Resolved);
        _summaryLabel.Text = Localizer.Format(
            "RenameSummaryFormat",
            totalCount,
            changeCount,
            reviewCount,
            conflictCount,
            resolvedCount);
    }

    private void UpdateCommandState()
    {
        _okButton.Enabled = !_rows.Any(static row => row.BlockingError);
        _nextIssueButton.Enabled = _rows.Any(static row => IsIssueState(row.State));
        _skipButton.Enabled = _selectedRow is not null && !_selectedRow.IsSkipped;
        _ruleTraceButton.Enabled = _selectedRow?.Preview.RuleTraces.Count > 0;
    }

    private void UpdateSelectedValidation()
    {
        var row = _selectedRow;
        if (row is null)
        {
            _validationLabel.Text = "";
            _validationLabel.BackColor = SystemColors.Control;
            _validationLabel.ForeColor = SystemColors.ControlText;
            return;
        }

        _validationLabel.Text = string.IsNullOrWhiteSpace(row.ValidationMessage)
            ? row.Status
            : row.ValidationMessage;

        switch (row.State)
        {
            case RenameRowState.Invalid:
            case RenameRowState.Conflict when row.BlockingError:
                _validationLabel.BackColor = Color.FromArgb(255, 232, 232);
                _validationLabel.ForeColor = Color.FromArgb(128, 23, 23);
                break;
            case RenameRowState.Conflict:
            case RenameRowState.NeedsReview:
                _validationLabel.BackColor = Color.FromArgb(255, 249, 219);
                _validationLabel.ForeColor = Color.FromArgb(86, 65, 0);
                break;
            case RenameRowState.Resolved:
                _validationLabel.BackColor = Color.FromArgb(226, 246, 232);
                _validationLabel.ForeColor = Color.FromArgb(18, 92, 54);
                break;
            default:
                _validationLabel.BackColor = SystemColors.Control;
                _validationLabel.ForeColor = SystemColors.ControlText;
                break;
        }
    }

    private void SelectNextIssue()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var start = _selectedRow is null ? -1 : _rows.IndexOf(_selectedRow);
        for (var offset = 1; offset <= _rows.Count; offset++)
        {
            var index = (start + offset + _rows.Count) % _rows.Count;
            var row = _rows[index];
            if (!IsIssueState(row.State))
            {
                continue;
            }

            SelectRow(row);
            _suggestedNameBox.Focus();
            return;
        }
    }

    private void Confirm()
    {
        foreach (var row in _rows)
        {
            NormalizeEditedRow(row);
        }

        ValidateRows();
        var blockingRow = _rows.FirstOrDefault(static row => row.BlockingError);
        if (blockingRow is not null)
        {
            SelectRow(blockingRow);
            ShowValidation(blockingRow.ValidationMessage);
            return;
        }

        var previews = CreateEditedPreviews();
        if (_applyOnOk)
        {
            Result = RenameOperations.Apply(previews);
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

    private IReadOnlyList<RenamePreview> CreateEditedPreviews()
    {
        var previews = new List<RenamePreview>();
        foreach (var row in _rows)
        {
            if (row.IsSkipped)
            {
                previews.Add(row.Preview with
                {
                    SuggestedFileName = row.OriginalName,
                    SuggestedPath = row.Preview.OriginalPath,
                    Status = RenamePreviewStatus.Skipped
                });
                continue;
            }

            var safeName = WindowsFileNameSafety.MakeSafeFileName(row.SuggestedName.Trim());
            var status = row.State switch
            {
                RenameRowState.Unchanged => RenamePreviewStatus.Unchanged,
                RenameRowState.NeedsReview => RenamePreviewStatus.NeedsReview,
                RenameRowState.Conflict => RenamePreviewStatus.Conflict,
                _ => RenamePreviewStatus.Ready
            };

            previews.Add(row.Preview with
            {
                SuggestedFileName = safeName,
                SuggestedPath = row.TargetPath,
                Status = status
            });
        }

        return previews;
    }

    private static IEnumerable<string> BuildTokens(RenameRow row)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in GetTokenCandidates(row))
        {
            var normalized = token.Trim();
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                continue;
            }

            yield return normalized;
        }
    }

    private static IEnumerable<string> BuildRuleTraceLines(RenamePreview preview)
    {
        foreach (var trace in preview.RuleTraces)
        {
            var action = trace.Applied
                ? Localizer.Get("RenameRuleTraceApplied")
                : Localizer.Get("RenameRuleTraceCandidateOnly");
            var review = trace.RequiresReview
                ? " / " + RenameCorrectionRuleText.GetModeDisplayName(RenameCorrectionRuleMode.Review)
                : "";
            yield return $"{action}{review}: [{RenameCorrectionRuleText.GetStageDisplayName(trace.Stage)}] {trace.RuleName}";
            yield return $"  {Localizer.Get("RenameRuleTraceBefore")}: {trace.Before}";
            yield return $"  {Localizer.Get("RenameRuleTraceAfter")}: {trace.After}";
        }
    }

    private static IEnumerable<string> GetTokenCandidates(RenameRow row)
    {
        var originalStem = Path.GetFileNameWithoutExtension(row.OriginalName);
        yield return originalStem;

        foreach (var token in SplitUsefulTokens(originalStem))
        {
            yield return token;
        }

        yield return row.Preview.Parts.Title;
        if (!string.IsNullOrWhiteSpace(row.Preview.Parts.EpisodeRange))
        {
            yield return row.Preview.Parts.EpisodeRange;
        }

        if (!string.IsNullOrWhiteSpace(row.Preview.Parts.Author))
        {
            yield return row.Preview.Parts.Author;
        }

        foreach (var tag in row.Preview.Parts.Tags)
        {
            yield return tag;
        }

        foreach (var candidate in row.Preview.Candidates)
        {
            foreach (var token in GetCorrectionCandidateTokens(candidate.Value))
            {
                yield return token;
            }
        }

        foreach (var candidate in row.Preview.Candidates)
        {
            yield return candidate.Value;
        }
    }

    private static IEnumerable<string> GetCorrectionCandidateTokens(string candidateFileName)
    {
        var stem = Path.GetFileNameWithoutExtension(candidateFileName);
        var title = NormalizeCandidateTitleToken(stem);
        if (string.IsNullOrWhiteSpace(title))
        {
            yield break;
        }

        yield return title;
        foreach (var token in SplitUsefulTokens(title))
        {
            yield return token;
        }
    }

    private static string NormalizeCandidateTitleToken(string value)
    {
        var result = CandidateBracketMetadataRegex.Replace(value, " ");
        while (true)
        {
            var next = CandidateEpisodeSuffixRegex.Replace(result, " ");
            if (string.Equals(next, result, StringComparison.Ordinal))
            {
                break;
            }

            result = next;
        }

        return CandidateWhitespaceRegex.Replace(result, " ").Trim(CandidateTitleTrimChars);
    }

    private static IEnumerable<string> SplitUsefulTokens(string value)
    {
        var separators = new[] { ' ', '\t', '_', '-', '.', ',', ';', '[', ']', '(', ')', '{', '}', '~' };
        return value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static token => token.Length >= 2);
    }

    private static int GetInitialSortPriority(RenamePreview preview)
    {
        return preview.Status switch
        {
            RenamePreviewStatus.Conflict => 0,
            RenamePreviewStatus.NeedsReview => 1,
            RenamePreviewStatus.Ready => 2,
            RenamePreviewStatus.Unchanged => 3,
            _ => 4
        };
    }

    private static bool IsIssueState(RenameRowState state)
    {
        return state is RenameRowState.Invalid or RenameRowState.Conflict or RenameRowState.NeedsReview;
    }

    private static string[] LoadCommonPhrases()
    {
        try
        {
            return File.Exists(RenameDictionaryStore.DictionaryPath)
                ? RenameDictionaryStore.Load().CommonPhrases
                    .Where(static phrase => !string.IsNullOrWhiteSpace(phrase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileToolsEnvironment.Log("RENAME-COMMON-PHRASES", ex.Message);
            return [];
        }
    }

    private static void ShowValidation(string message)
    {
        MessageBox.Show(
            message,
            FileToolsEnvironment.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private enum RenameRowState
    {
        Ready,
        Unchanged,
        NeedsReview,
        Conflict,
        Resolved,
        Skipped,
        Invalid
    }

    private sealed class NonFocusableButton : Button
    {
        public NonFocusableButton()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        protected override bool ShowFocusCues => false;
    }

    private sealed class RenamePartDraft
    {
        public string Title { get; set; } = "";

        public string EpisodeRange { get; set; } = "";

        public string Author { get; set; } = "";

        public string Extension { get; init; } = "";

        public string[] Tags { get; private set; } = [];

        public string TagsText
        {
            get => string.Join(", ", Tags);
            set => Tags = ParseTags(value);
        }

        public static RenamePartDraft From(FileNameParts parts)
        {
            return new RenamePartDraft
            {
                Title = parts.Title,
                EpisodeRange = parts.EpisodeRange ?? "",
                Author = parts.Author ?? "",
                Extension = parts.Extension,
                Tags = parts.Tags.ToArray()
            };
        }

        public string Compose()
        {
            return new FileNameParts
            {
                Title = Title.Trim(),
                EpisodeRange = string.IsNullOrWhiteSpace(EpisodeRange) ? null : EpisodeRange.Trim(),
                Author = string.IsNullOrWhiteSpace(Author) ? null : Author.Trim(),
                Tags = Tags,
                Extension = Extension
            }.Compose();
        }

        private static string[] ParseTags(string value)
        {
            return value
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static tag => tag.Trim('[', ']', '(', ')'))
                .Where(static tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private sealed class RenameRow : INotifyPropertyChanged
    {
        private string _suggestedName = "";
        private bool _isSkipped;
        private string _status = "";

        public RenameRow(RenamePreview preview)
        {
            Preview = preview;
            AutomaticName = preview.SuggestedFileName;
            Draft = RenamePartDraft.From(preview.Parts);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public RenamePreview Preview { get; }

        public RenamePartDraft Draft { get; private set; }

        public string AutomaticName { get; }

        public string OriginalName { get; init; } = "";

        public string SuggestedName
        {
            get => _suggestedName;
            set
            {
                if (_suggestedName == value)
                {
                    return;
                }

                _suggestedName = value;
                IsSkipped = false;
                OnPropertyChanged(nameof(SuggestedName));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value)
                {
                    return;
                }

                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public RenameRowState State { get; set; } = RenameRowState.Ready;

        public bool BlockingError { get; set; }

        public bool UserEdited { get; set; }

        public bool IsSkipped
        {
            get => _isSkipped;
            private set
            {
                if (_isSkipped == value)
                {
                    return;
                }

                _isSkipped = value;
                OnPropertyChanged(nameof(IsSkipped));
            }
        }

        public string TargetPath { get; set; } = "";

        public string ValidationMessage { get; set; } = "";

        public void ResetDraft()
        {
            Draft = RenamePartDraft.From(Preview.Parts);
        }

        public void SetSkipped()
        {
            IsSkipped = true;
            UserEdited = true;
            _suggestedName = OriginalName;
            OnPropertyChanged(nameof(SuggestedName));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
