using System.Globalization;
using System.Text;

namespace FileTools;

/// <summary>파일명 구성 요소로부터 문자열 후보를 렌더링하는 패턴 생성기.</summary>
internal sealed record FileNameRenderPattern
{
    public required string DisplayName { get; init; }

    public required string Template { get; init; }

    public double BaseScore { get; init; } = 0.5;

    public string Reason { get; init; } = "";
}

internal sealed record FileNameRenderCandidate
{
    public required string FileName { get; init; }

    public required FileNameRenderPattern Pattern { get; init; }

    public double Score { get; init; }

    public IReadOnlyDictionary<string, string> Fields { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

internal sealed class FileNameRenderPatternOptions
{
    public int MaxCandidates { get; init; } = 12;
}

/// <summary>템플릿을 점수순으로 렌더링해 고유한 파일명 후보를 만든다.</summary>
internal static class FileNameRenderPatternGenerator
{
    /// <summary>
    /// 파일명 요소를 파싱해 기본 렌더 패턴을 만든다.
    /// </summary>
    public static IReadOnlyList<FileNameRenderPattern> CreateDefaultPatterns(string fileNameOrPath)
    {
        var fields = FileNamePatternFields.FromFileName(fileNameOrPath);
        return CreateDefaultPatterns(fields);
    }

    /// <summary>
    /// 템플릿을 텍스트 후보로 렌더링하고 중복/비정상 값을 걸러낸다.
    /// </summary>
    public static IReadOnlyList<FileNameRenderCandidate> Generate(
        string fileNameOrPath,
        IEnumerable<FileNameRenderPattern>? patterns = null,
        FileNameRenderPatternOptions? options = null)
    {
        options ??= new FileNameRenderPatternOptions();
        var fields = FileNamePatternFields.FromFileName(fileNameOrPath);
        var availablePatterns = (patterns ?? CreateDefaultPatterns(fields))
            .OrderByDescending(static pattern => pattern.BaseScore)
            .ThenBy(static pattern => pattern.Template, StringComparer.Ordinal)
            .ToArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<FileNameRenderCandidate>();

        foreach (var pattern in availablePatterns)
        {
            if (!TryRender(pattern.Template, fields, out var rendered))
            {
                continue;
            }

            var safeFileName = WindowsFileNameSafety.MakeSafeFileName(rendered);
            if (string.IsNullOrWhiteSpace(safeFileName) || !seen.Add(safeFileName))
            {
                continue;
            }

            candidates.Add(new FileNameRenderCandidate
            {
                FileName = safeFileName,
                Pattern = pattern,
                Score = pattern.BaseScore,
                Fields = fields.ToDictionary()
            });

            if (candidates.Count >= Math.Max(1, options.MaxCandidates))
            {
                break;
            }
        }

        return candidates;
    }

    /// <summary>파일명 요소를 기반으로 괄호/텍스트/숫자 조합 템플릿을 구성한다.</summary>
    private static IReadOnlyList<FileNameRenderPattern> CreateDefaultPatterns(FileNamePatternFields fields)
    {
        var patterns = new List<FileNameRenderPattern>();
        var numberSlot = fields.NumberWidth > 1
            ? "{Number:" + new string('0', fields.NumberWidth) + "}"
            : "{Number}";

        if (fields.HasText && fields.HasNumber && fields.HasBracketedText)
        {
            patterns.Add(new FileNameRenderPattern
            {
                DisplayName = "Bracket prefix",
                Template = "[{BracketedText}] {Text} " + numberSlot + "{Extension}",
                BaseScore = 0.70,
                Reason = "Preserves bracketed text before title and number."
            });
            patterns.Add(new FileNameRenderPattern
            {
                DisplayName = "Bracketed text first",
                Template = "{BracketedText} - {Text} " + numberSlot + "{Extension}",
                BaseScore = 0.68,
                Reason = "Moves bracketed text into a plain leading field."
            });
            patterns.Add(new FileNameRenderPattern
            {
                DisplayName = "Bracket suffix",
                Template = "{Text} " + numberSlot + " [{BracketedText}]{Extension}",
                BaseScore = 0.64,
                Reason = "Keeps bracketed text as a suffix field."
            });
            patterns.Add(new FileNameRenderPattern
            {
                DisplayName = "Parenthesized suffix",
                Template = "{Text} - " + numberSlot + " ({BracketedText}){Extension}",
                BaseScore = 0.62,
                Reason = "Keeps bracketed text as a parenthesized suffix."
            });
        }

        if (fields.HasText && fields.HasNumber)
        {
            patterns.Add(new FileNameRenderPattern
            {
                DisplayName = "Title number",
                Template = "{Text} " + numberSlot + "{Extension}",
                BaseScore = 0.60,
                Reason = "Uses title and number only."
            });
            patterns.Add(new FileNameRenderPattern
            {
                DisplayName = "Title dash number",
                Template = "{Text} - " + numberSlot + "{Extension}",
                BaseScore = 0.58,
                Reason = "Uses a dash separator between title and number."
            });
        }

        return patterns;
    }

    /// <summary>단일 템플릿을 순회해 토큰 치환이 가능한지 검증한다.</summary>
    private static bool TryRender(string template, FileNamePatternFields fields, out string value)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < template.Length; index++)
        {
            var ch = template[index];
            if (ch != '{')
            {
                builder.Append(ch);
                continue;
            }

            var endIndex = template.IndexOf('}', index + 1);
            if (endIndex < 0)
            {
                value = "";
                return false;
            }

            var token = template[(index + 1)..endIndex].Trim();
            if (!TryResolve(token, fields, out var resolved))
            {
                value = "";
                return false;
            }

            builder.Append(resolved);
            index = endIndex;
        }

        value = builder.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>필드 이름/포맷을 해석해 렌더 문자열을 반환한다.</summary>
    private static bool TryResolve(string token, FileNamePatternFields fields, out string value)
    {
        var separatorIndex = token.IndexOf(':', StringComparison.Ordinal);
        var name = separatorIndex < 0 ? token : token[..separatorIndex];
        var format = separatorIndex < 0 ? "" : token[(separatorIndex + 1)..];
        switch (name.Trim().ToUpperInvariant())
        {
            case "BRACKETEDTEXT":
                return TryResolveText(fields.BracketedText, out value);
            case "TEXT":
                return TryResolveText(fields.Text, out value);
            case "NUMBER":
                return TryResolveNumber(fields.Number, format, out value);
            case "EXTENSION":
                value = fields.Extension;
                return true;
            default:
                value = "";
                return false;
        }
    }

    private static bool TryResolveText(string value, out string resolved)
    {
        resolved = value.Trim();
        return resolved.Length > 0;
    }

    private static bool TryResolveNumber(string value, string format, out string resolved)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            resolved = "";
            return false;
        }

        if (string.IsNullOrWhiteSpace(format) ||
            !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            resolved = value;
            return true;
        }

        resolved = number.ToString(format, CultureInfo.InvariantCulture);
        return true;
    }

    private sealed record FileNamePatternFields(
        string BracketedText,
        string Text,
        string Number,
        int NumberWidth,
        string Extension)
    {
        public bool HasBracketedText => BracketedText.Length > 0;

        public bool HasText => Text.Length > 0;

        public bool HasNumber => Number.Length > 0;

        public static FileNamePatternFields FromFileName(string fileNameOrPath)
        {
            var fileName = Path.GetFileName(fileNameOrPath.Trim());
            var tokens = FileNamePatternDiscovery.Tokenize(fileName);
            var bracketedText = tokens
                .FirstOrDefault(static token => token.Kind == FileNamePatternTokenKind.BracketedText)
                ?.Text ?? "";
            var text = string.Join(
                ' ',
                tokens
                    .Where(static token => token.Kind == FileNamePatternTokenKind.Text)
                    .Select(static token => token.Text));
            var number = tokens
                .Where(static token => token.Kind == FileNamePatternTokenKind.Number)
                .Select(static token => token.Text)
                .LastOrDefault() ?? "";

            return new FileNamePatternFields(
                bracketedText,
                text,
                number,
                number.Length,
                Path.GetExtension(fileName));
        }

        public IReadOnlyDictionary<string, string> ToDictionary()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["BracketedText"] = BracketedText,
                ["Text"] = Text,
                ["Number"] = Number,
                ["Extension"] = Extension
            };
        }
    }
}
