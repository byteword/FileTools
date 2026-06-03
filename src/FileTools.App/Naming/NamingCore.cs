using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FileTools;

internal sealed record CorrectionOptions
{
    public RenameParserProfileDocument ParserProfile { get; init; } = RenameParserProfileStore.CreateDefaultDocument();

    public IReadOnlyList<RenameDictionaryEntry> RenameDictionary { get; init; } = [];

    public string[] CommonPhrases { get; init; } = [];

    public IReadOnlyList<RenameCorrectionRule> Rules { get; init; } = RenameRuleStore.CreateDefaultDocument().Rules;
}

internal sealed record FileNameParts
{
    public required string Title { get; init; }
    public string? EpisodeRange { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? Author { get; init; }
    public required string Extension { get; init; }

    public string Compose()
    {
        var tokens = new List<string>();
        if (!string.IsNullOrWhiteSpace(Title))
        {
            tokens.Add(Title.Trim());
        }

        if (!string.IsNullOrWhiteSpace(EpisodeRange))
        {
            tokens.Add(EpisodeRange.Trim());
        }

        var name = string.Join(' ', tokens);
        var bracketParts = new List<string>();
        foreach (var tag in Tags.Where(static tag => !string.IsNullOrWhiteSpace(tag)))
        {
            bracketParts.Add($"[{tag.Trim()}]");
        }

        if (!string.IsNullOrWhiteSpace(Author))
        {
            bracketParts.Add($"[{Author.Trim()}]");
        }

        if (bracketParts.Count > 0)
        {
            name += " " + string.Concat(bracketParts);
        }

        return name + Extension;
    }
}

internal enum RenamePreviewStatus
{
    Unchanged,
    Ready,
    NeedsReview,
    Conflict,
    Skipped
}

internal sealed record RenamePreview
{
    public required string OriginalPath { get; init; }
    public required string OriginalFileName { get; init; }
    public required FileNameParts Parts { get; init; }
    public required string SuggestedFileName { get; init; }
    public required string SuggestedPath { get; init; }
    public required RenamePreviewStatus Status { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = [];
    public IReadOnlyList<NameCorrectionCandidate> Candidates { get; init; } = [];
    public IReadOnlyList<RenameRuleTrace> RuleTraces { get; init; } = [];
}

internal sealed record RenameRuleTrace
{
    public required string RuleId { get; init; }
    public required string RuleName { get; init; }
    public required RenameCorrectionRuleStage Stage { get; init; }
    public required RenameCorrectionRuleMode Mode { get; init; }
    public required string Before { get; init; }
    public required string After { get; init; }
    public required string Reason { get; init; }
    public bool Applied { get; init; } = true;
    public bool RequiresReview { get; init; }
}

internal sealed record NameCorrectionCandidate
{
    public required string Value { get; init; }
    public required double Score { get; init; }
    public required string Reason { get; init; }
    public bool RequiresReview { get; init; } = true;
}

internal sealed partial class KoreanFileNameCorrector
{
    private readonly CorrectionOptions _options;
    private readonly HashSet<string> _knownTags;
    private readonly ObfuscatedHangulCandidateGenerator _obfuscatedHangulCandidateGenerator;
    private readonly IReadOnlyList<RenameCorrectionRule> _rules;
    private readonly Regex _episodeUnitRangeRegex;
    private readonly Regex _episodeCompoundRegex;
    private readonly Regex _episodeSingleRegex;
    private readonly Regex _episodePrefixedSingleRegex;
    private readonly Regex _authorRegex;
    private readonly Regex _episodePrefixInsideTokenRegex;
    private readonly Regex _titleNoiseRegex;

    public KoreanFileNameCorrector(CorrectionOptions? options = null)
    {
        _options = options ?? new CorrectionOptions();
        var parserProfile = RenameParserProfileStore.Normalize(_options.ParserProfile);
        _knownTags = new HashSet<string>(parserProfile.KnownTags, StringComparer.OrdinalIgnoreCase);
        _obfuscatedHangulCandidateGenerator = new ObfuscatedHangulCandidateGenerator(new KoreanLexicon(_options.CommonPhrases));
        _rules = RenameRuleStore.NormalizeRules(_options.Rules);
        _episodeUnitRangeRegex = CreateEpisodeUnitRangeRegex(parserProfile);
        _episodeCompoundRegex = CreateEpisodeCompoundRegex(parserProfile);
        _episodeSingleRegex = CreateEpisodeSingleRegex(parserProfile);
        _episodePrefixedSingleRegex = CreateEpisodePrefixedSingleRegex(parserProfile);
        _authorRegex = CreateAuthorRegex(parserProfile);
        _episodePrefixInsideTokenRegex = CreateEpisodePrefixInsideTokenRegex(parserProfile);
        _titleNoiseRegex = CreateTitleNoiseRegex(parserProfile);
    }

    public RenamePreview CreatePreview(string path)
    {
        var originalFileName = Path.GetFileName(path);
        var isDirectory = Directory.Exists(path);
        var extension = isDirectory ? "" : Path.GetExtension(originalFileName);
        var rawStem = isDirectory ? originalFileName : Path.GetFileNameWithoutExtension(originalFileName);
        var reasons = new List<string>();
        var ruleTraces = new List<RenameRuleTrace>();
        var stemCandidates = new List<NameCorrectionCandidate>();
        var requiresReview = false;

        var normalizedStem = NormalizeStem(rawStem, reasons, ruleTraces, stemCandidates, ref requiresReview);
        normalizedStem = CreateConfiguredCandidates(normalizedStem, reasons, ruleTraces, stemCandidates, ref requiresReview);

        var parts = ParseParts(normalizedStem, extension, reasons, ruleTraces, ref requiresReview);
        var composed = parts.Compose();
        var suggested = ApplyWindowsFileNameSafety(composed, reasons, ruleTraces, ref requiresReview);
        if (!string.Equals(suggested, composed, StringComparison.Ordinal))
        {
            reasons.Add("Windows 파일명 금지 문자 또는 예약어 보정");
        }

        var directory = Path.GetDirectoryName(path) ?? "";
        var status = string.Equals(originalFileName, suggested, StringComparison.Ordinal)
            ? RenamePreviewStatus.Unchanged
            : RenamePreviewStatus.Ready;

        if (string.IsNullOrWhiteSpace(parts.Title) || string.IsNullOrWhiteSpace(parts.EpisodeRange))
        {
            status = RenamePreviewStatus.NeedsReview;
            reasons.Add("제목 또는 회차 추출 확인 필요");
        }
        else if (requiresReview || stemCandidates.Any(static candidate => candidate.RequiresReview))
        {
            status = RenamePreviewStatus.NeedsReview;
            reasons.Add("이름 보정 규칙 검수 필요");
        }

        var fileNameCandidates = CreateFileNameCandidates(originalFileName, suggested, extension, stemCandidates);

        return new RenamePreview
        {
            OriginalPath = path,
            OriginalFileName = originalFileName,
            Parts = parts,
            SuggestedFileName = suggested,
            SuggestedPath = Path.Combine(directory, suggested),
            Status = status,
            Reasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Candidates = fileNameCandidates,
            RuleTraces = ruleTraces
        };
    }

    private static IReadOnlyList<NameCorrectionCandidate> CreateFileNameCandidates(
        string originalFileName,
        string suggestedFileName,
        string extension,
        IEnumerable<NameCorrectionCandidate> stemCandidates)
    {
        var candidates = new List<NameCorrectionCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddCandidate(suggestedFileName, 1.0, "자동 교정 결과", requiresReview: false);

        foreach (var candidate in stemCandidates)
        {
            var fileName = ToFileNameCandidate(candidate.Value, extension);
            AddCandidate(fileName, candidate.Score, candidate.Reason, candidate.RequiresReview);
        }

        AddCandidate(originalFileName, 0.0, "원본 이름", requiresReview: false);
        return candidates;

        void AddCandidate(string fileName, double score, string reason, bool requiresReview)
        {
            var safeFileName = WindowsFileNameSafety.MakeSafeFileName(fileName.Trim());
            if (string.IsNullOrWhiteSpace(safeFileName) || !seen.Add(safeFileName))
            {
                return;
            }

            candidates.Add(new NameCorrectionCandidate
            {
                Value = safeFileName,
                Score = score,
                Reason = reason,
                RequiresReview = requiresReview
            });
        }
    }

    private static string ToFileNameCandidate(string value, string extension)
    {
        var fileName = Path.GetFileName(value.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "";
        }

        if (string.IsNullOrWhiteSpace(extension) ||
            string.Equals(Path.GetExtension(fileName), extension, StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        return fileName + extension;
    }

    public FileNameParts ParseParts(string stem, string extension, List<string>? reasons = null)
    {
        var reasonList = reasons ?? [];
        var trace = new List<RenameRuleTrace>();
        var review = false;
        var candidates = new List<NameCorrectionCandidate>();
        var normalizedStem = NormalizeStem(stem, reasonList, trace, candidates, ref review);
        return ParseParts(normalizedStem, extension, reasonList, trace, ref review);
    }

    private FileNameParts ParseParts(
        string stem,
        string extension,
        List<string> reasons,
        List<RenameRuleTrace> ruleTraces,
        ref bool requiresReview)
    {
        var working = stem;
        var tags = new List<string>();
        string? author = null;

        var bracketRule = GetRule(RenameCorrectionRuleKind.BuiltInBracketMetadataExtraction);
        if (IsRuleActive(bracketRule))
        {
            var before = working;
            working = BracketContentRegex().Replace(working, match =>
            {
                var content = CleanupToken(match.Groups[1].Value);
                if (content.Length == 0)
                {
                    return " ";
                }

                if (_knownTags.Contains(content))
                {
                    tags.Add(content);
                    reasons.Add($"태그 추출: {content}");
                }
                else if (author is null)
                {
                    author = content;
                    reasons.Add($"작가 후보 추출: {content}");
                }
                else
                {
                    tags.Add(content);
                    reasons.Add($"추가 표식 추출: {content}");
                }

                return " ";
            });
            RecordRuleChange(bracketRule!, before, working, "괄호 메타데이터 추출", reasons, ruleTraces, ref requiresReview);
        }

        var authorRule = GetRule(RenameCorrectionRuleKind.BuiltInAuthorExtraction);
        if (IsRuleActive(authorRule))
        {
            var authorMatch = _authorRegex.Match(working);
            if (authorMatch.Success && author is null)
            {
                var before = working;
                author = CleanupToken(authorMatch.Groups["author"].Value);
                working = working.Remove(authorMatch.Index, authorMatch.Length).Insert(authorMatch.Index, " ");
                reasons.Add($"작가 후보 추출: {author}");
                RecordRuleChange(authorRule!, before, working, $"작가 후보 추출: {author}", reasons, ruleTraces, ref requiresReview);
            }
        }

        string? episode = null;
        var episodeRule = GetRule(RenameCorrectionRuleKind.BuiltInEpisodeExtraction);
        if (IsRuleActive(episodeRule))
        {
            var rangeMatch = _episodeUnitRangeRegex.Matches(working).LastOrDefault()
                ?? _episodeCompoundRegex.Matches(working).LastOrDefault();
            if (rangeMatch is not null)
            {
                var before = working;
                episode = NormalizeEpisodeToken(rangeMatch.Groups["episode"].Value);
                working = working.Remove(rangeMatch.Index, rangeMatch.Length).Insert(rangeMatch.Index, " ");
                reasons.Add($"회차 추출: {episode}");
                RecordRuleChange(episodeRule!, before, working, $"회차 추출: {episode}", reasons, ruleTraces, ref requiresReview);
            }
            else
            {
                var singleMatch = _episodePrefixedSingleRegex.Matches(working).LastOrDefault()
                    ?? _episodeSingleRegex.Matches(working).LastOrDefault();
                if (singleMatch is not null)
                {
                    var before = working;
                    episode = NormalizeEpisodeToken(singleMatch.Groups["episode"].Value);
                    working = working.Remove(singleMatch.Index, singleMatch.Length).Insert(singleMatch.Index, " ");
                    reasons.Add($"회차 추출: {episode}");
                    RecordRuleChange(episodeRule!, before, working, $"회차 추출: {episode}", reasons, ruleTraces, ref requiresReview);
                }
            }
        }

        var title = working;
        var titleRule = GetRule(RenameCorrectionRuleKind.BuiltInTitleCleanup);
        if (IsRuleActive(titleRule))
        {
            var before = title;
            title = CleanupTitle(title);
            RecordRuleChange(titleRule!, before, title, "제목 노이즈 정리", reasons, ruleTraces, ref requiresReview);
        }
        else
        {
            title = WhitespaceRegex().Replace(title.Trim(), " ");
        }

        return new FileNameParts
        {
            Title = title,
            EpisodeRange = episode,
            Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Author = string.IsNullOrWhiteSpace(author) ? null : author,
            Extension = extension
        };
    }

    private string NormalizeStem(
        string stem,
        List<string> reasons,
        List<RenameRuleTrace> ruleTraces,
        List<NameCorrectionCandidate> stemCandidates,
        ref bool requiresReview)
    {
        var result = stem;
        foreach (var rule in GetRules(RenameCorrectionRuleStage.Preprocess)
            .Concat(GetRules(RenameCorrectionRuleStage.UserRewrite)))
        {
            result = ApplyStemRule(rule, result, reasons, ruleTraces, stemCandidates, ref requiresReview);
        }

        return result;
    }

    private string ApplyStemRule(
        RenameCorrectionRule rule,
        string value,
        List<string> reasons,
        List<RenameRuleTrace> ruleTraces,
        List<NameCorrectionCandidate> stemCandidates,
        ref bool requiresReview)
    {
        if (!IsRuleActive(rule))
        {
            return value;
        }

        var before = value;
        var after = rule.Kind switch
        {
            RenameCorrectionRuleKind.BuiltInUnicodeJamo => KoreanJamoNormalizer.Normalize(value),
            RenameCorrectionRuleKind.BuiltInMojibakeRecovery => TryRecoverUtf8AsLatin1(value) ?? value,
            RenameCorrectionRuleKind.BuiltInRenameDictionary => ApplyRenameDictionary(value, reasons),
            RenameCorrectionRuleKind.BuiltInSeparatorNormalization => NormalizeSeparators(value),
            RenameCorrectionRuleKind.LiteralReplace => ApplyLiteralReplaceRule(rule, value),
            RenameCorrectionRuleKind.PrefixTrim => ApplyPrefixTrimRule(rule, value),
            RenameCorrectionRuleKind.SuffixTrim => ApplySuffixTrimRule(rule, value),
            RenameCorrectionRuleKind.WhitespaceNormalize => WhitespaceRegex().Replace(value, " ").Trim(),
            RenameCorrectionRuleKind.SeparatorNormalize => NormalizeSeparators(value),
            RenameCorrectionRuleKind.RegexReplace => ApplyRegexReplaceRule(rule, value),
            _ => value
        };

        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return value;
        }

        if (rule.Mode == RenameCorrectionRuleMode.CandidateOnly)
        {
            stemCandidates.Add(new NameCorrectionCandidate
            {
                Value = after,
                Score = 0.70,
                Reason = $"{rule.DisplayName} 후보",
                RequiresReview = true
            });
            RecordRuleTrace(rule, before, after, "후보 생성", false, true, reasons, ruleTraces);
            requiresReview = true;
            return value;
        }

        RecordRuleTrace(
            rule,
            before,
            after,
            rule.Kind == RenameCorrectionRuleKind.BuiltInMojibakeRecovery
                ? "UTF-8/Latin-1 깨짐 후보 복구"
                : rule.DisplayName,
            true,
            rule.Mode == RenameCorrectionRuleMode.Review,
            reasons,
            ruleTraces);
        if (rule.Mode == RenameCorrectionRuleMode.Review)
        {
            requiresReview = true;
        }

        return after;
    }

    private string CreateConfiguredCandidates(
        string stem,
        List<string> reasons,
        List<RenameRuleTrace> ruleTraces,
        List<NameCorrectionCandidate> stemCandidates,
        ref bool requiresReview)
    {
        var result = stem;
        foreach (var rule in GetRules(RenameCorrectionRuleStage.Candidate))
        {
            if (!IsRuleActive(rule) || rule.Kind != RenameCorrectionRuleKind.BuiltInObfuscatedHangulCandidate)
            {
                continue;
            }

            var candidates = _obfuscatedHangulCandidateGenerator.Generate(result);
            if (candidates.Count == 0)
            {
                continue;
            }

            var candidate = candidates[0];
            if (rule.Mode == RenameCorrectionRuleMode.CandidateOnly)
            {
                stemCandidates.Add(candidate);
                RecordRuleTrace(rule, result, candidate.Value, candidate.Reason, false, candidate.RequiresReview, reasons, ruleTraces);
                requiresReview = requiresReview || candidate.RequiresReview;
                continue;
            }

            RecordRuleTrace(
                rule,
                result,
                candidate.Value,
                candidate.Reason,
                true,
                rule.Mode == RenameCorrectionRuleMode.Review || candidate.RequiresReview,
                reasons,
                ruleTraces);
            result = candidate.Value;
            requiresReview = requiresReview || rule.Mode == RenameCorrectionRuleMode.Review || candidate.RequiresReview;
        }

        return result;
    }

    private string ApplyWindowsFileNameSafety(
        string value,
        List<string> reasons,
        List<RenameRuleTrace> ruleTraces,
        ref bool requiresReview)
    {
        var rule = GetRule(RenameCorrectionRuleKind.BuiltInWindowsSafeFileName);
        var safe = WindowsFileNameSafety.MakeSafeFileName(value);
        if (IsRuleActive(rule))
        {
            RecordRuleChange(rule!, value, safe, "Windows 파일명 안전화", reasons, ruleTraces, ref requiresReview);
            return safe;
        }

        return safe;
    }

    private static string ApplyLiteralReplaceRule(RenameCorrectionRule rule, string value)
    {
        if (string.IsNullOrWhiteSpace(rule.Source))
        {
            return value;
        }

        return value.Replace(
            rule.Source,
            rule.Replacement,
            rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string ApplyPrefixTrimRule(RenameCorrectionRule rule, string value)
    {
        var source = rule.Source.Trim();
        if (source.Length == 0 ||
            !value.StartsWith(source, rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return value;
        }

        return value[source.Length..].TrimStart();
    }

    private static string ApplySuffixTrimRule(RenameCorrectionRule rule, string value)
    {
        var source = rule.Source.Trim();
        if (source.Length == 0 ||
            !value.EndsWith(source, rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return value;
        }

        return value[..^source.Length].TrimEnd();
    }

    private static string ApplyRegexReplaceRule(RenameCorrectionRule rule, string value)
    {
        if (string.IsNullOrWhiteSpace(rule.Source))
        {
            return value;
        }

        try
        {
            var options = rule.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            var regex = new Regex(rule.Source, options, TimeSpan.FromMilliseconds(100));
            return regex.Replace(value, rule.Replacement);
        }
        catch (ArgumentException)
        {
            return value;
        }
        catch (RegexMatchTimeoutException)
        {
            return value;
        }
    }

    private IEnumerable<RenameCorrectionRule> GetRules(RenameCorrectionRuleStage stage)
    {
        return _rules
            .Where(rule => rule.Stage == stage)
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.DisplayName, StringComparer.CurrentCultureIgnoreCase);
    }

    private RenameCorrectionRule? GetRule(RenameCorrectionRuleKind kind)
    {
        return _rules.FirstOrDefault(rule => rule.Kind == kind);
    }

    private static bool IsRuleActive(RenameCorrectionRule? rule)
    {
        return rule is not null && (rule.Enabled || rule.IsRequired);
    }

    private static void RecordRuleChange(
        RenameCorrectionRule rule,
        string before,
        string after,
        string reason,
        List<string> reasons,
        List<RenameRuleTrace> ruleTraces,
        ref bool requiresReview)
    {
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return;
        }

        var review = rule.Mode == RenameCorrectionRuleMode.Review;
        RecordRuleTrace(rule, before, after, reason, true, review, reasons, ruleTraces);
        requiresReview = requiresReview || review;
    }

    private static void RecordRuleTrace(
        RenameCorrectionRule rule,
        string before,
        string after,
        string reason,
        bool applied,
        bool requiresReview,
        List<string> reasons,
        List<RenameRuleTrace> ruleTraces)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            reasons.Add($"{rule.DisplayName}: {reason}");
        }

        ruleTraces.Add(new RenameRuleTrace
        {
            RuleId = rule.Id,
            RuleName = rule.DisplayName,
            Stage = rule.Stage,
            Mode = rule.Mode,
            Before = before,
            After = after,
            Reason = reason,
            Applied = applied,
            RequiresReview = requiresReview
        });
    }

    private string ApplyRenameDictionary(string value, List<string> reasons)
    {
        var result = value;
        foreach (var entry in _options.RenameDictionary)
        {
            if (string.IsNullOrWhiteSpace(entry.Source))
            {
                continue;
            }

            var next = result.Replace(entry.Source.Trim(), entry.Replacement.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(result, next, StringComparison.Ordinal))
            {
                reasons.Add($"사용자 사전 적용: {entry.Source} -> {entry.Replacement}");
                result = next;
            }
        }

        return result;
    }

    private static string? TryRecoverUtf8AsLatin1(string value)
    {
        if (!value.Any(static ch => ch is 'Ã' or 'Â' or '¤' or '¸'))
        {
            return null;
        }

        try
        {
            var bytes = Encoding.Latin1.GetBytes(value);
            var decoded = Encoding.UTF8.GetString(bytes);
            return decoded.Any(static ch => ch >= 0xAC00 && ch <= 0xD7A3) ? decoded : null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private string NormalizeEpisodeToken(string value)
    {
        var normalized = _episodePrefixInsideTokenRegex.Replace(value.Trim(), "$1");
        return WhitespaceRegex().Replace(normalized, "");
    }

    private static string NormalizeSeparators(string value)
    {
        var normalized = value
            .Replace('（', '(').Replace('）', ')')
            .Replace('［', '[').Replace('］', ']')
            .Replace('~', '-').Replace('～', '-')
            .Replace('_', ' ');

        return WhitespaceRegex().Replace(normalized, " ").Trim();
    }

    private string CleanupTitle(string value)
    {
        var title = CleanupToken(value);
        title = _titleNoiseRegex.Replace(title, " ");
        return WhitespaceRegex().Replace(title, " ").Trim(' ', '.', ',');
    }

    private static string CleanupToken(string value)
    {
        return WhitespaceRegex().Replace(value.Trim(), " ").Trim(' ', '-', '.', ',', '[', ']', '(', ')');
    }

    [GeneratedRegex("[\\[\\(\\{]([^\\]\\)\\}]{1,80})[\\]\\)\\}]")]
    private static partial Regex BracketContentRegex();

    private static Regex CreateEpisodeUnitRangeRegex(RenameParserProfileDocument profile)
    {
        var units = BuildAlternation(profile.EpisodeUnits);
        if (units.Length == 0)
        {
            return CreateNeverMatchRegex();
        }

        var prefix = BuildOptionalEpisodePrefixPattern(profile.EpisodePrefixes);
        return CreateConfiguredRegex(
            $@"(?<!\d){prefix}(?<episode>\d{{1,5}}\s*(?:{units})\s*[-~]\s*{prefix}\d{{1,5}}\s*(?:{units}))(?!\d)",
            RegexOptions.IgnoreCase);
    }

    private static Regex CreateEpisodeCompoundRegex(RenameParserProfileDocument profile)
    {
        var unit = BuildOptionalUnitPattern(profile.EpisodeUnits);
        var prefix = BuildOptionalEpisodePrefixPattern(profile.EpisodePrefixes);
        return CreateConfiguredRegex(
            $@"(?<!\d){prefix}(?<episode>\d{{1,5}}{unit}(?:\s*[.-]\s*{prefix}\d{{1,5}}{unit})+)(?!\d)",
            RegexOptions.IgnoreCase);
    }

    private static Regex CreateEpisodeSingleRegex(RenameParserProfileDocument profile)
    {
        var units = BuildAlternation(profile.EpisodeUnits);
        if (units.Length == 0)
        {
            return CreateNeverMatchRegex();
        }

        var prefix = BuildOptionalEpisodePrefixPattern(profile.EpisodePrefixes);
        return CreateConfiguredRegex(
            $@"{prefix}(?<episode>\d{{1,5}}\s*(?:{units}))",
            RegexOptions.IgnoreCase);
    }

    private static Regex CreateEpisodePrefixedSingleRegex(RenameParserProfileDocument profile)
    {
        var prefixes = BuildAlternation(profile.EpisodePrefixes.Where(ContainsAsciiLetter), allowOptionalDotForAscii: true);
        if (prefixes.Length == 0)
        {
            return CreateNeverMatchRegex();
        }

        return CreateConfiguredRegex(
            $@"(?<!\d)(?:{prefixes})\s*(?<episode>\d{{1,5}})(?!\d)",
            RegexOptions.IgnoreCase);
    }

    private static Regex CreateAuthorRegex(RenameParserProfileDocument profile)
    {
        var prefixes = BuildAlternation(profile.AuthorPrefixes);
        if (prefixes.Length == 0)
        {
            return CreateNeverMatchRegex();
        }

        return CreateConfiguredRegex(
            $@"(?:{prefixes})\s*[:：-]?\s*(?<author>[가-힣A-Za-z0-9_. ]{{2,40}})",
            RegexOptions.IgnoreCase);
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    private static Regex CreateEpisodePrefixInsideTokenRegex(RenameParserProfileDocument profile)
    {
        var prefixes = BuildAlternation(profile.EpisodePrefixes.Where(static prefix => !ContainsAsciiLetter(prefix)));
        if (prefixes.Length == 0)
        {
            return CreateNeverMatchRegex();
        }

        return CreateConfiguredRegex($@"(^|[.-])(?:{prefixes})\s*", RegexOptions.IgnoreCase);
    }

    private static Regex CreateTitleNoiseRegex(RenameParserProfileDocument profile)
    {
        var words = BuildAlternation(profile.TitleNoiseWords);
        if (words.Length == 0)
        {
            return CreateNeverMatchRegex();
        }

        return CreateConfiguredRegex($@"(?<![\p{{L}}\p{{N}}_])(?:{words})(?![\p{{L}}\p{{N}}_])", RegexOptions.IgnoreCase);
    }

    private static string BuildOptionalEpisodePrefixPattern(IEnumerable<string> prefixes)
    {
        var pattern = BuildAlternation(prefixes, allowOptionalDotForAscii: true);
        return pattern.Length == 0 ? "" : $@"(?:(?:{pattern})\s*)?";
    }

    private static string BuildOptionalUnitPattern(IEnumerable<string> units)
    {
        var pattern = BuildAlternation(units);
        return pattern.Length == 0 ? "" : $@"\s*(?:{pattern})?";
    }

    private static string BuildAlternation(IEnumerable<string> values, bool allowOptionalDotForAscii = false)
    {
        return string.Join("|", values
            .Select(static value => value.Trim())
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static value => value.Length)
            .Select(value => Regex.Escape(value) + (allowOptionalDotForAscii && IsAsciiLetters(value) ? @"\.?" : "")));
    }

    private static bool IsAsciiLetters(string value)
    {
        return value.All(static ch => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    }

    private static bool ContainsAsciiLetter(string value)
    {
        return value.Any(static ch => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    }

    private static Regex CreateConfiguredRegex(string pattern, RegexOptions options = RegexOptions.None)
    {
        return new Regex(pattern, RegexOptions.Compiled | options);
    }

    private static Regex CreateNeverMatchRegex()
    {
        return CreateConfiguredRegex("(?!)");
    }
}

internal sealed class RenamePlanner
{
    private readonly KoreanFileNameCorrector _corrector;

    public RenamePlanner(KoreanFileNameCorrector? corrector = null)
    {
        _corrector = corrector ?? new KoreanFileNameCorrector();
    }

    public IReadOnlyList<RenamePreview> CreatePlan(IEnumerable<string> paths)
    {
        var previews = paths.Select(_corrector.CreatePreview).ToList();
        return ResolveConflicts(previews);
    }

    public IReadOnlyList<RenamePreview> ResolveConflicts(IEnumerable<RenamePreview> previews)
    {
        var result = new List<RenamePreview>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var preview in previews)
        {
            var candidate = preview;
            var originalDirectory = Path.GetDirectoryName(preview.OriginalPath) ?? "";
            var targetPath = Path.Combine(originalDirectory, preview.SuggestedFileName);

            if (!used.Add(targetPath) ||
                PathExists(targetPath) && !string.Equals(targetPath, preview.OriginalPath, StringComparison.OrdinalIgnoreCase))
            {
                var uniqueName = CreateUniqueFileName(originalDirectory, preview.SuggestedFileName, used, preview.OriginalPath);
                candidate = preview with
                {
                    SuggestedFileName = uniqueName,
                    SuggestedPath = Path.Combine(originalDirectory, uniqueName),
                    Status = RenamePreviewStatus.Conflict,
                    Reasons = preview.Reasons.Concat(["중복 파일명 충돌 방지 suffix 적용"]).Distinct(StringComparer.Ordinal).ToArray()
                };
                used.Add(candidate.SuggestedPath);
            }

            result.Add(candidate);
        }

        return result;
    }

    private static string CreateUniqueFileName(string directory, string fileName, HashSet<string> used, string originalPath)
    {
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);

        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{stem} ({index}){extension}";
            var candidatePath = Path.Combine(directory, candidate);
            if (!used.Contains(candidatePath)
                && (!PathExists(candidatePath) || string.Equals(candidatePath, originalPath, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"중복 파일명을 해결할 수 없습니다: {fileName}");
    }

    private static bool PathExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }
}

internal static class KoreanJamoNormalizer
{
    private static readonly Dictionary<char, int> Leading = new()
    {
        ['ㄱ'] = 0, ['ㄲ'] = 1, ['ㄴ'] = 2, ['ㄷ'] = 3, ['ㄸ'] = 4, ['ㄹ'] = 5, ['ㅁ'] = 6, ['ㅂ'] = 7, ['ㅃ'] = 8,
        ['ㅅ'] = 9, ['ㅆ'] = 10, ['ㅇ'] = 11, ['ㅈ'] = 12, ['ㅉ'] = 13, ['ㅊ'] = 14, ['ㅋ'] = 15, ['ㅌ'] = 16,
        ['ㅍ'] = 17, ['ㅎ'] = 18
    };

    private static readonly Dictionary<char, int> Vowels = new()
    {
        ['ㅏ'] = 0, ['ㅐ'] = 1, ['ㅑ'] = 2, ['ㅒ'] = 3, ['ㅓ'] = 4, ['ㅔ'] = 5, ['ㅕ'] = 6, ['ㅖ'] = 7, ['ㅗ'] = 8,
        ['ㅘ'] = 9, ['ㅙ'] = 10, ['ㅚ'] = 11, ['ㅛ'] = 12, ['ㅜ'] = 13, ['ㅝ'] = 14, ['ㅞ'] = 15, ['ㅟ'] = 16,
        ['ㅠ'] = 17, ['ㅡ'] = 18, ['ㅢ'] = 19, ['ㅣ'] = 20
    };

    private static readonly Dictionary<char, int> Trailing = new()
    {
        ['ㄱ'] = 1, ['ㄲ'] = 2, ['ㄳ'] = 3, ['ㄴ'] = 4, ['ㄵ'] = 5, ['ㄶ'] = 6, ['ㄷ'] = 7, ['ㄹ'] = 8,
        ['ㄺ'] = 9, ['ㄻ'] = 10, ['ㄼ'] = 11, ['ㄽ'] = 12, ['ㄾ'] = 13, ['ㄿ'] = 14, ['ㅀ'] = 15, ['ㅁ'] = 16,
        ['ㅂ'] = 17, ['ㅄ'] = 18, ['ㅅ'] = 19, ['ㅆ'] = 20, ['ㅇ'] = 21, ['ㅈ'] = 22, ['ㅊ'] = 23, ['ㅋ'] = 24,
        ['ㅌ'] = 25, ['ㅍ'] = 26, ['ㅎ'] = 27
    };

    public static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(normalized.Length);

        for (var i = 0; i < normalized.Length; i++)
        {
            var current = normalized[i];
            if (Leading.TryGetValue(current, out var leadingIndex)
                && i + 1 < normalized.Length
                && Vowels.TryGetValue(normalized[i + 1], out var vowelIndex))
            {
                i++;
                var trailingIndex = 0;

                if (i + 1 < normalized.Length
                    && Trailing.TryGetValue(normalized[i + 1], out var candidateTrailing)
                    && !IsNextSyllableStart(normalized, i + 1))
                {
                    trailingIndex = candidateTrailing;
                    i++;
                }

                builder.Append((char)(0xAC00 + ((leadingIndex * 21) + vowelIndex) * 28 + trailingIndex));
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool IsNextSyllableStart(string value, int consonantIndex)
    {
        return consonantIndex + 1 < value.Length && Vowels.ContainsKey(value[consonantIndex + 1]);
    }
}

internal sealed partial class ObfuscatedHangulCandidateGenerator
{
    private readonly KoreanLexicon _lexicon;

    public ObfuscatedHangulCandidateGenerator(KoreanLexicon? lexicon = null)
    {
        _lexicon = lexicon ?? new KoreanLexicon();
    }

    public IReadOnlyList<NameCorrectionCandidate> Generate(string value)
    {
        if (!LooksLikeObfuscatedHangul(value))
        {
            return [];
        }

        var compacted = SplitJamoWhitespaceRegex().Replace(value, match =>
            match.Value.Any(static ch => ch >= 0x3130 && ch <= 0x318F)
                ? match.Value.Replace(" ", "", StringComparison.Ordinal)
                : match.Value);
        var replaced = ObfuscatedTokenRegex().Replace(compacted, match => RestoreToken(match.Value));
        if (string.Equals(replaced, value, StringComparison.Ordinal))
        {
            return [];
        }

        var normalized = KoreanJamoNormalizer.Normalize(replaced);
        if (string.Equals(normalized, value, StringComparison.Ordinal))
        {
            return [];
        }

        var score = Score(value, normalized);
        if (score < 0.45)
        {
            return [];
        }

        return
        [
            new NameCorrectionCandidate
            {
                Value = normalized,
                Score = score,
                Reason = "왜곡 한글 복원 후보",
                RequiresReview = true
            }
        ];
    }

    private double Score(string original, string candidate)
    {
        var originalHangul = CountHangulSyllables(original);
        var candidateHangul = CountHangulSyllables(candidate);
        var converted = original.Where(IsObfuscationCharacter).Count();
        var lexiconMatches = _lexicon.CountMatches(candidate);

        var score = 0.0;
        if (candidateHangul > originalHangul)
        {
            score += 0.35;
        }

        if (converted > 0)
        {
            score += Math.Min(0.25, converted * 0.08);
        }

        if (lexiconMatches > 0)
        {
            score += Math.Min(0.35, lexiconMatches * 0.18);
        }

        if (ContainsProtectedEnglishWord(original))
        {
            score -= 0.30;
        }

        return Math.Clamp(score, 0, 1);
    }

    private static string RestoreToken(string token)
    {
        var chars = token.ToCharArray();
        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = chars[index] switch
            {
                'r' when HasKoreanNeighbor(chars, index) => 'ㅏ',
                'R' when HasKoreanNeighbor(chars, index) => 'ㅏ',
                'o' when HasKoreanNeighbor(chars, index) => 'ㅇ',
                'O' when HasKoreanNeighbor(chars, index) => 'ㅇ',
                '0' when HasKoreanNeighbor(chars, index) => 'ㅇ',
                'l' when HasKoreanNeighbor(chars, index) => 'ㅣ',
                'I' when HasKoreanNeighbor(chars, index) => 'ㅣ',
                '|' when HasKoreanNeighbor(chars, index) => 'ㅣ',
                _ => chars[index]
            };
        }

        return CombineVowelDigraphs(new string(chars));
    }

    private static string CombineVowelDigraphs(string value)
    {
        return value
            .Replace("ㅓㅣ", "ㅔ", StringComparison.Ordinal)
            .Replace("ㅏㅣ", "ㅐ", StringComparison.Ordinal)
            .Replace("ㅕㅣ", "ㅖ", StringComparison.Ordinal)
            .Replace("ㅑㅣ", "ㅒ", StringComparison.Ordinal);
    }

    private static bool HasKoreanNeighbor(char[] chars, int index)
    {
        return index > 0 && IsKoreanLike(chars[index - 1])
            || index + 1 < chars.Length && IsKoreanLike(chars[index + 1]);
    }

    private static bool LooksLikeObfuscatedHangul(string value)
    {
        return value.Any(IsKoreanLike) && (value.Any(IsObfuscationCharacter) || SplitJamoWhitespaceRegex().IsMatch(value));
    }

    private static int CountHangulSyllables(string value)
    {
        return value.Count(static ch => ch >= 0xAC00 && ch <= 0xD7A3);
    }

    private static bool IsKoreanLike(char ch)
    {
        return ch >= 0xAC00 && ch <= 0xD7A3 || ch >= 0x3130 && ch <= 0x318F;
    }

    private static bool IsObfuscationCharacter(char ch)
    {
        return ch is 'r' or 'R' or 'o' or 'O' or '0' or 'l' or 'I' or '|';
    }

    private static bool ContainsProtectedEnglishWord(string value)
    {
        return ProtectedEnglishRegex().IsMatch(value);
    }

    [GeneratedRegex("[가-힣ㄱ-ㅎㅏ-ㅣA-Za-z0-9|]{2,}")]
    private static partial Regex ObfuscatedTokenRegex();

    [GeneratedRegex("[가-힣ㄱ-ㅎㅏ-ㅣ](?:\\s+[가-힣ㄱ-ㅎㅏ-ㅣ]){1,}")]
    private static partial Regex SplitJamoWhitespaceRegex();

    [GeneratedRegex("\\b(?:idol|lol|no|vol|season|special|episode|ep)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProtectedEnglishRegex();
}

internal sealed class KoreanLexicon
{
    private readonly HashSet<string> _words;

    public KoreanLexicon(IEnumerable<string>? additionalWords = null)
    {
        _words = new HashSet<string>(DefaultWords, StringComparer.Ordinal);
        if (additionalWords is null)
        {
            return;
        }

        foreach (var word in additionalWords.Where(static word => !string.IsNullOrWhiteSpace(word)))
        {
            _words.Add(word.Trim());
        }
    }

    public int CountMatches(string value)
    {
        var count = 0;
        foreach (var word in _words)
        {
            if (word.Length >= 2 && value.Contains(word, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static readonly string[] DefaultWords =
    [
        "아이돌",
        "에피소드",
        "아름다운",
        "제목",
        "작품",
        "완결",
        "번역",
        "외전",
        "단편",
        "개정판",
        "특별판",
        "고화질",
        "사랑",
        "마법",
        "전생",
        "회귀",
        "이세계",
        "용사",
        "마왕",
        "학교",
        "친구",
        "가족",
        "비밀",
        "소녀",
        "소년",
        "여왕",
        "왕자",
        "공주",
        "기사",
        "작가"
    ];
}

internal static partial class WindowsFileNameSafety
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string MakeSafeFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        stem = InvalidCharactersRegex().Replace(stem, " ");
        stem = WhitespaceRegex().Replace(stem, " ").Trim().TrimEnd('.');

        if (stem.Length == 0)
        {
            stem = "untitled";
        }

        if (ReservedNames.Contains(stem))
        {
            stem += "_";
        }

        var safeExtension = new StringBuilder();
        foreach (var ch in extension)
        {
            safeExtension.Append(Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch);
        }

        return stem + safeExtension.ToString().TrimEnd(' ', '.');
    }

    [GeneratedRegex("[<>:\"/\\\\|?*\\u0000-\\u001F]")]
    private static partial Regex InvalidCharactersRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
