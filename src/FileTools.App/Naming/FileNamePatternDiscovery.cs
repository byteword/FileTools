using System.Globalization;
using System.Text;

namespace FileTools;

internal enum FileNamePatternTokenKind
{
    Text,
    Number,
    Separator,
    BracketedText
}

internal sealed record FileNamePatternToken(
    FileNamePatternTokenKind Kind,
    string Text,
    string Open = "",
    string Close = "");

internal sealed record DiscoveredFileNamePattern
{
    public required string Signature { get; init; }

    public required int MatchCount { get; init; }

    public required double Score { get; init; }

    public bool HasSequentialNumberSlot { get; init; }

    public int StableValueSlotCount { get; init; }

    public IReadOnlyList<string> SampleFileNames { get; init; } = [];

    public IReadOnlyList<string> Reasons { get; init; } = [];
}

internal sealed class FileNamePatternDiscoveryOptions
{
    public int MaxPatterns { get; init; } = 12;

    public int MaxSamplesPerPattern { get; init; } = 5;
}

internal static class FileNamePatternDiscovery
{
    public static IReadOnlyList<FileNamePatternToken> Tokenize(string fileNameOrPath)
    {
        var fileName = Path.GetFileName(fileNameOrPath.Trim());
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return [];
        }

        var tokens = new List<FileNamePatternToken>();
        for (var index = 0; index < stem.Length;)
        {
            var ch = stem[index];
            var closing = GetClosingBracket(ch);
            if (closing.HasValue)
            {
                var end = stem.IndexOf(closing.Value, index + 1);
                if (end > index + 1 && end - index <= 80)
                {
                    var bracketText = NormalizeText(stem[(index + 1)..end]);
                    if (bracketText.Length > 0)
                    {
                        tokens.Add(new FileNamePatternToken(
                            FileNamePatternTokenKind.BracketedText,
                            bracketText,
                            ch.ToString(),
                            closing.Value.ToString()));
                        index = end + 1;
                        continue;
                    }
                }
            }

            if (char.IsDigit(ch))
            {
                var start = index;
                while (index < stem.Length && char.IsDigit(stem[index]))
                {
                    index++;
                }

                tokens.Add(new FileNamePatternToken(
                    FileNamePatternTokenKind.Number,
                    stem[start..index]));
                continue;
            }

            if (IsSeparator(ch))
            {
                var start = index;
                while (index < stem.Length && IsSeparator(stem[index]))
                {
                    index++;
                }

                tokens.Add(new FileNamePatternToken(
                    FileNamePatternTokenKind.Separator,
                    NormalizeSeparator(stem[start..index])));
                continue;
            }

            var textStart = index;
            while (index < stem.Length &&
                   !char.IsDigit(stem[index]) &&
                   !IsSeparator(stem[index]) &&
                   !GetClosingBracket(stem[index]).HasValue)
            {
                index++;
            }

            var text = NormalizeText(stem[textStart..index]);
            if (text.Length > 0)
            {
                tokens.Add(new FileNamePatternToken(FileNamePatternTokenKind.Text, text));
            }
        }

        return tokens;
    }

    public static IReadOnlyList<DiscoveredFileNamePattern> Discover(
        IEnumerable<string> fileNamesOrPaths,
        FileNamePatternDiscoveryOptions? options = null)
    {
        options ??= new FileNamePatternDiscoveryOptions();
        var tokenized = fileNamesOrPaths
            .Select(fileName => new TokenizedFileName(Path.GetFileName(fileName), Tokenize(fileName)))
            .Where(static item => item.Tokens.Count > 0)
            .ToArray();
        if (tokenized.Length == 0)
        {
            return [];
        }

        return tokenized
            .GroupBy(static item => BuildGroupingSignature(item.Tokens), StringComparer.Ordinal)
            .Select(group => BuildPattern(group.ToArray(), tokenized.Length, options))
            .OrderByDescending(static pattern => pattern.Score)
            .ThenByDescending(static pattern => pattern.MatchCount)
            .ThenBy(static pattern => pattern.Signature, StringComparer.Ordinal)
            .Take(Math.Max(1, options.MaxPatterns))
            .ToArray();
    }

    private static DiscoveredFileNamePattern BuildPattern(
        IReadOnlyList<TokenizedFileName> files,
        int totalFileCount,
        FileNamePatternDiscoveryOptions options)
    {
        var tokens = files[0].Tokens;
        var signature = BuildDisplaySignature(files);
        var hasSequentialNumberSlot = HasSequentialNumberSlot(files);
        var stableValueSlots = CountStableValueSlots(files);
        var coverageScore = (double)files.Count / totalFileCount;
        var valueTokenCount = tokens.Count(static token => token.Kind != FileNamePatternTokenKind.Separator);
        var stableScore = valueTokenCount == 0
            ? 0
            : (double)stableValueSlots / valueTokenCount;
        var simplicityScore = Math.Max(0, 1.0 - Math.Max(0, tokens.Count - 4) * 0.08);
        var score = Math.Clamp(
            coverageScore * 0.65 +
            stableScore * 0.15 +
            simplicityScore * 0.08 +
            (hasSequentialNumberSlot ? 0.12 : 0),
            0,
            1);

        var reasons = new List<string>
        {
            $"matches {files.Count} of {totalFileCount} file(s)"
        };
        if (hasSequentialNumberSlot)
        {
            reasons.Add("contains a sequential number slot");
        }

        if (stableValueSlots > 0)
        {
            reasons.Add($"contains {stableValueSlots} stable value slot(s)");
        }

        return new DiscoveredFileNamePattern
        {
            Signature = signature,
            MatchCount = files.Count,
            Score = score,
            HasSequentialNumberSlot = hasSequentialNumberSlot,
            StableValueSlotCount = stableValueSlots,
            SampleFileNames = files
                .Select(static file => file.FileName)
                .Take(Math.Max(1, options.MaxSamplesPerPattern))
                .ToArray(),
            Reasons = reasons
        };
    }

    private static string BuildGroupingSignature(IReadOnlyList<FileNamePatternToken> tokens)
    {
        var builder = new StringBuilder();
        foreach (var token in tokens)
        {
            builder.Append(token.Kind);
            if (token.Kind == FileNamePatternTokenKind.BracketedText)
            {
                builder.Append(':').Append(token.Open).Append(token.Close);
            }
            else if (token.Kind == FileNamePatternTokenKind.Separator)
            {
                builder.Append(':').Append(token.Text);
            }

            builder.Append('|');
        }

        return builder.ToString();
    }

    private static string BuildDisplaySignature(IReadOnlyList<TokenizedFileName> files)
    {
        var builder = new StringBuilder();
        var tokenCount = files[0].Tokens.Count;
        for (var index = 0; index < tokenCount; index++)
        {
            var column = files.Select(file => file.Tokens[index]).ToArray();
            var first = column[0];
            builder.Append(first.Kind switch
            {
                FileNamePatternTokenKind.Text => "{Text}",
                FileNamePatternTokenKind.Number => CreateNumberSlot(column),
                FileNamePatternTokenKind.Separator => CreateSeparatorSlot(column),
                FileNamePatternTokenKind.BracketedText => first.Open + "{BracketedText}" + first.Close,
                _ => "{Value}"
            });
        }

        return builder.ToString();
    }

    private static string CreateNumberSlot(IReadOnlyList<FileNamePatternToken> column)
    {
        var widths = column.Select(static token => token.Text.Length).Distinct().ToArray();
        if (widths.Length == 1 && widths[0] > 1)
        {
            return "{Number:" + new string('0', widths[0]) + "}";
        }

        return "{Number}";
    }

    private static string CreateSeparatorSlot(IReadOnlyList<FileNamePatternToken> column)
    {
        var values = column.Select(static token => token.Text).Distinct(StringComparer.Ordinal).ToArray();
        return values.Length == 1 ? values[0] : "{Separator}";
    }

    private static bool HasSequentialNumberSlot(IReadOnlyList<TokenizedFileName> files)
    {
        if (files.Count < 2)
        {
            return false;
        }

        var tokenCount = files[0].Tokens.Count;
        for (var index = 0; index < tokenCount; index++)
        {
            if (files[0].Tokens[index].Kind != FileNamePatternTokenKind.Number)
            {
                continue;
            }

            var values = new List<int>();
            foreach (var file in files)
            {
                if (!int.TryParse(file.Tokens[index].Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                {
                    values.Clear();
                    break;
                }

                values.Add(value);
            }

            var sorted = values.Distinct().Order().ToArray();
            if (sorted.Length >= 2 && sorted.Zip(sorted.Skip(1)).All(static pair => pair.Second - pair.First == 1))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountStableValueSlots(IReadOnlyList<TokenizedFileName> files)
    {
        var count = 0;
        var tokenCount = files[0].Tokens.Count;
        for (var index = 0; index < tokenCount; index++)
        {
            if (files[0].Tokens[index].Kind == FileNamePatternTokenKind.Separator)
            {
                continue;
            }

            if (files
                .Select(file => NormalizeStableValue(file.Tokens[index]))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == 1)
            {
                count++;
            }
        }

        return count;
    }

    private static string NormalizeStableValue(FileNamePatternToken token)
    {
        return token.Kind == FileNamePatternTokenKind.Number
            ? token.Text.TrimStart('0')
            : token.Text;
    }

    private static char? GetClosingBracket(char ch)
    {
        return ch switch
        {
            '[' => ']',
            '(' => ')',
            '{' => '}',
            _ => null
        };
    }

    private static bool IsSeparator(char ch)
    {
        return char.IsWhiteSpace(ch) || ch is '-' or '_' or '.' or '~' or '!' or '+';
    }

    private static string NormalizeSeparator(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(ch);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }

    private static string NormalizeText(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private sealed record TokenizedFileName(string FileName, IReadOnlyList<FileNamePatternToken> Tokens);
}
