using System.Text.RegularExpressions;

namespace FileTools;

internal enum NameMergeAnalysisKind
{
    Empty,
    Exact,
    NumericRange,
    TextRange,
    CommonToken,
    Prefix
}

internal sealed record NameMergeAnalysis(
    string Stem,
    NameMergeAnalysisKind Kind,
    IReadOnlyList<string> Candidates,
    IReadOnlyList<string> Reasons)
{
    public bool IsReady => !string.IsNullOrWhiteSpace(Stem);
}

/// <summary>
/// 여러 선택 이름에서 공통 텍스트와 구간값을 함께 분석한다.
/// </summary>
internal static class NameMergeAnalyzer
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ExplicitNumericRangeRegex = new(
        @"(?<!\d)(?<start>\d{1,8})(?<startUnit>[\p{L}]{0,8})\s*(?:~|-|–|—|to)\s*(?<end>\d{1,8})(?<endUnit>[\p{L}]{0,8})(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SingleNumericTokenRegex = new(
        @"(?<!\d)(?<start>\d{1,8})(?<startUnit>[\p{L}]{0,8})(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string CreateCommonStem(IEnumerable<string?> stems, string fallback = "")
    {
        return Analyze(stems, fallback).Stem;
    }

    public static NameMergeAnalysis Analyze(IEnumerable<string?> stems, string fallback = "")
    {
        var normalized = stems
            .Where(static stem => !string.IsNullOrWhiteSpace(stem))
            .Select(static stem => NormalizeStem(stem!))
            .Where(static stem => stem.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
        {
            return CreateFallback(fallback);
        }

        if (normalized.Length == 1)
        {
            var single = WindowsFileNameSafety.MakeSafeFileName(normalized[0]);
            return new NameMergeAnalysis(
                single,
                NameMergeAnalysisKind.Exact,
                [single],
                ["Single selected stem."]);
        }

        if (TryCreateNumericRangeStem(normalized, out var numericStem))
        {
            return new NameMergeAnalysis(
                numericStem,
                NameMergeAnalysisKind.NumericRange,
                [numericStem],
                ["Merged numeric sequence ranges from selected names."]);
        }

        if (TryCreateTextRangeStem(normalized, out var textRangeStem))
        {
            return new NameMergeAnalysis(
                textRangeStem,
                NameMergeAnalysisKind.TextRange,
                [textRangeStem],
                ["Merged variable text token between shared text parts."]);
        }

        var prefixStem = CreateCommonPrefixStem(normalized);
        if (!string.IsNullOrWhiteSpace(prefixStem))
        {
            return new NameMergeAnalysis(
                prefixStem,
                NameMergeAnalysisKind.Prefix,
                [prefixStem],
                ["Used common prefix as fallback."]);
        }

        if (TryCreateCommonTokenStem(normalized, out var tokenStem))
        {
            return new NameMergeAnalysis(
                tokenStem,
                NameMergeAnalysisKind.CommonToken,
                [tokenStem],
                ["Used the strongest common text token found anywhere in selected names."]);
        }

        return CreateFallback(fallback);
    }

    private static NameMergeAnalysis CreateFallback(string fallback)
    {
        if (string.IsNullOrWhiteSpace(fallback))
        {
            return new NameMergeAnalysis("", NameMergeAnalysisKind.Empty, [], ["No useful common stem was found."]);
        }

        var safe = WindowsFileNameSafety.MakeSafeFileName(fallback);
        return string.IsNullOrWhiteSpace(safe)
            ? new NameMergeAnalysis("", NameMergeAnalysisKind.Empty, [], ["No useful common stem was found."])
            : new NameMergeAnalysis(safe, NameMergeAnalysisKind.Empty, [safe], ["Used fallback stem."]);
    }

    private static bool TryCreateNumericRangeStem(IReadOnlyList<string> stems, out string value)
    {
        value = "";
        var candidatesByStem = stems
            .Select(CreateNumericRangeCandidates)
            .ToArray();
        if (candidatesByStem.Any(static candidates => candidates.Count == 0))
        {
            return false;
        }

        foreach (var first in candidatesByStem[0])
        {
            var selected = new List<NumericRangeNamePart> { first };
            for (var index = 1; index < candidatesByStem.Length; index++)
            {
                var matching = candidatesByStem[index].FirstOrDefault(part => IsSameNumericRangeShape(first, part));
                if (matching is null)
                {
                    selected.Clear();
                    break;
                }

                selected.Add(matching);
            }

            if (selected.Count != candidatesByStem.Length)
            {
                continue;
            }

            var summary = FormatRangeSummary(selected.Select(static part => part.Range).ToArray(), first.Unit);
            if (string.IsNullOrWhiteSpace(summary))
            {
                continue;
            }

            var rawValue = JoinNameParts(first.Prefix, summary, first.Suffix);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            value = WindowsFileNameSafety.MakeSafeFileName(rawValue);
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static IReadOnlyList<NumericRangeNamePart> CreateNumericRangeCandidates(string stem)
    {
        var candidates = new List<NumericRangeNamePart>();
        AddNumericRangeCandidates(stem, ExplicitNumericRangeRegex, candidates);
        AddNumericRangeCandidates(stem, SingleNumericTokenRegex, candidates);
        return candidates;
    }

    private static void AddNumericRangeCandidates(
        string stem,
        Regex regex,
        List<NumericRangeNamePart> candidates)
    {
        foreach (Match match in regex.Matches(stem))
        {
            if (!TryCreateRange(match, out var range, out var unit))
            {
                continue;
            }

            candidates.Add(new NumericRangeNamePart(
                CleanPrefix(stem[..match.Index]),
                CleanSuffix(stem[(match.Index + match.Length)..]),
                unit,
                range));
        }
    }

    private static bool TryCreateRange(Match match, out MergeRange range, out string unit)
    {
        range = default;
        unit = "";
        if (!int.TryParse(match.Groups["start"].Value, out var start))
        {
            return false;
        }

        var endText = match.Groups["end"].Success ? match.Groups["end"].Value : match.Groups["start"].Value;
        if (!int.TryParse(endText, out var end))
        {
            return false;
        }

        var startUnit = match.Groups["startUnit"].Success ? match.Groups["startUnit"].Value : "";
        var endUnit = match.Groups["endUnit"].Success ? match.Groups["endUnit"].Value : "";
        if (!string.IsNullOrWhiteSpace(startUnit) &&
            !string.IsNullOrWhiteSpace(endUnit) &&
            !string.Equals(startUnit, endUnit, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        unit = string.IsNullOrWhiteSpace(startUnit) ? endUnit : startUnit;

        if (end < start)
        {
            (start, end) = (end, start);
        }

        var width = Math.Max(match.Groups["start"].Value.Length, endText.Length);
        range = new MergeRange(start, end, width);
        return true;
    }

    private static bool IsSameNumericRangeShape(NumericRangeNamePart first, NumericRangeNamePart next)
    {
        return string.Equals(next.Prefix, first.Prefix, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(next.Suffix, first.Suffix, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(next.Unit, first.Unit, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatRangeSummary(IReadOnlyList<MergeRange> ranges, string unit)
    {
        if (ranges.Count == 0)
        {
            return "";
        }

        var width = ranges.Max(static range => range.Width);
        var ordered = ranges
            .OrderBy(static range => range.Start)
            .ThenBy(static range => range.End)
            .ToArray();
        var merged = new List<MergeRange>();
        foreach (var range in ordered)
        {
            if (merged.Count == 0)
            {
                merged.Add(range);
                continue;
            }

            var previous = merged[^1];
            if (range.Start <= previous.End + 1)
            {
                merged[^1] = previous with
                {
                    End = Math.Max(previous.End, range.End),
                    Width = Math.Max(previous.Width, range.Width)
                };
                continue;
            }

            merged.Add(range);
        }

        return string.Join(", ", merged.Select(range => FormatRange(range, width, unit)));
    }

    private static string FormatRange(MergeRange range, int width, string unit)
    {
        var start = range.Start.ToString(new string('0', width));
        if (!string.IsNullOrWhiteSpace(unit))
        {
            if (range.Start == range.End)
            {
                return start + unit;
            }

            var endWithUnit = range.End.ToString(new string('0', width)) + unit;
            return start + unit + " - " + endWithUnit;
        }

        if (range.Start == range.End)
        {
            return start;
        }

        var end = range.End.ToString(new string('0', width));
        return start + "~" + end;
    }

    private static bool TryCreateTextRangeStem(IReadOnlyList<string> stems, out string value)
    {
        value = "";
        var prefixLength = CommonPrefixLength(stems);
        var suffixLength = CommonSuffixLength(stems, prefixLength);
        if (prefixLength == 0 && suffixLength == 0)
        {
            return false;
        }

        var variables = stems
            .Select(stem => stem.Substring(prefixLength, stem.Length - prefixLength - suffixLength))
            .Select(CleanVariable)
            .ToArray();
        if (variables.Any(static variable => variable.Length == 0) ||
            !TryFormatTextRange(variables, out var variableSummary))
        {
            return false;
        }

        var prefix = CleanPrefix(stems[0][..prefixLength]);
        var suffix = CleanSuffix(stems[0][(stems[0].Length - suffixLength)..]);
        if (!ContainsUsefulLetter(prefix) && !ContainsUsefulLetter(suffix))
        {
            return false;
        }

        var rawValue = JoinNameParts(prefix, variableSummary, suffix);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        value = WindowsFileNameSafety.MakeSafeFileName(rawValue);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryFormatTextRange(IReadOnlyList<string> variables, out string value)
    {
        value = "";
        if (variables.Any(static variable => variable.Length != 1 || !char.IsLetter(variable[0])))
        {
            return false;
        }

        var chars = variables
            .Select(static variable => variable[0])
            .Distinct()
            .OrderBy(static ch => ch)
            .ToArray();
        if (chars.Length == 0)
        {
            return false;
        }

        var groups = new List<(char Start, char End)>();
        foreach (var ch in chars)
        {
            if (groups.Count == 0)
            {
                groups.Add((ch, ch));
                continue;
            }

            var last = groups[^1];
            if (ch == last.End + 1)
            {
                groups[^1] = (last.Start, ch);
                continue;
            }

            groups.Add((ch, ch));
        }

        value = string.Join(", ", groups.Select(static group =>
            group.Start == group.End ? group.Start.ToString() : group.Start + "~" + group.End));
        return value.Length > 0;
    }

    private static bool TryCreateCommonTokenStem(IReadOnlyList<string> stems, out string value)
    {
        value = "";
        var shortest = stems.OrderBy(static stem => stem.Length).First();
        for (var length = shortest.Length; length >= 2; length--)
        {
            for (var start = 0; start <= shortest.Length - length; start++)
            {
                var candidate = CleanVariable(shortest.Substring(start, length));
                if (!ContainsUsefulLetter(candidate))
                {
                    continue;
                }

                if (stems.All(stem => stem.Contains(candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    value = WindowsFileNameSafety.MakeSafeFileName(candidate);
                    return !string.IsNullOrWhiteSpace(value);
                }
            }
        }

        return false;
    }

    private static string CreateCommonPrefixStem(IReadOnlyList<string> stems)
    {
        var prefix = stems[0];
        foreach (var stem in stems.Skip(1))
        {
            var length = 0;
            var max = Math.Min(prefix.Length, stem.Length);
            while (length < max && char.ToUpperInvariant(prefix[length]) == char.ToUpperInvariant(stem[length]))
            {
                length++;
            }

            prefix = prefix[..length];
            if (prefix.Length == 0)
            {
                break;
            }
        }

        var cleaned = CleanPrefix(prefix);
        return string.IsNullOrWhiteSpace(cleaned)
            ? ""
            : WindowsFileNameSafety.MakeSafeFileName(cleaned);
    }

    private static int CommonPrefixLength(IReadOnlyList<string> values)
    {
        var minLength = values.Min(static value => value.Length);
        var length = 0;
        while (length < minLength)
        {
            var ch = char.ToUpperInvariant(values[0][length]);
            if (values.Any(value => char.ToUpperInvariant(value[length]) != ch))
            {
                break;
            }

            length++;
        }

        return length;
    }

    private static int CommonSuffixLength(IReadOnlyList<string> values, int prefixLength)
    {
        var minLength = values.Min(static value => value.Length) - prefixLength;
        var length = 0;
        while (length < minLength)
        {
            var ch = char.ToUpperInvariant(values[0][values[0].Length - 1 - length]);
            if (values.Any(value => char.ToUpperInvariant(value[value.Length - 1 - length]) != ch))
            {
                break;
            }

            length++;
        }

        return length;
    }

    private static string NormalizeStem(string stem)
    {
        return WhitespaceRegex.Replace(stem.Trim(), " ");
    }

    private static string CleanPrefix(string value)
    {
        return NormalizeStem(value).Trim().TrimEnd(' ', '.', '-', '_', '[', '(', '{', '#');
    }

    private static string CleanSuffix(string value)
    {
        return NormalizeStem(value).Trim().TrimStart(' ', '.', '-', '_', ']', ')', '}', '#');
    }

    private static string CleanVariable(string value)
    {
        return NormalizeStem(value).Trim().Trim(' ', '.', '-', '_', '[', '(', '{', ']', ')', '}', '#');
    }

    private static string JoinNameParts(params string[] parts)
    {
        var result = "";
        foreach (var part in parts.Where(static part => !string.IsNullOrWhiteSpace(part)).Select(static part => part.Trim()))
        {
            if (result.Length == 0)
            {
                result = part;
                continue;
            }

            result += ShouldAttachWithoutSpace(part) ? part : " " + part;
        }

        return result.Trim();
    }

    private static bool ShouldAttachWithoutSpace(string value)
    {
        return value.Length > 0 && (value[0] == '[' || value[0] == '(' || value[0] == '{');
    }

    private static bool ContainsUsefulLetter(string value)
    {
        return value.Any(char.IsLetter);
    }

    private sealed record NumericRangeNamePart(string Prefix, string Suffix, string Unit, MergeRange Range);

    private readonly record struct MergeRange(int Start, int End, int Width);
}
