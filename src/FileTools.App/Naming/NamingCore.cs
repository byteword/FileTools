using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FileTools;

internal sealed record CorrectionOptions
{
    public string[] KnownTags { get; init; } =
    [
        "완결",
        "번역",
        "단편",
        "컬러",
        "무삭제",
        "개정판",
        "외전",
        "직번"
    ];
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

    public KoreanFileNameCorrector(CorrectionOptions? options = null)
    {
        _options = options ?? new CorrectionOptions();
        _knownTags = new HashSet<string>(_options.KnownTags, StringComparer.OrdinalIgnoreCase);
        _obfuscatedHangulCandidateGenerator = new ObfuscatedHangulCandidateGenerator();
    }

    public RenamePreview CreatePreview(string path)
    {
        var originalFileName = Path.GetFileName(path);
        var isDirectory = Directory.Exists(path);
        var extension = isDirectory ? "" : Path.GetExtension(originalFileName);
        var rawStem = isDirectory ? originalFileName : Path.GetFileNameWithoutExtension(originalFileName);
        var reasons = new List<string>();

        var normalizedStem = NormalizeStem(rawStem, reasons);
        var candidates = _obfuscatedHangulCandidateGenerator.Generate(normalizedStem);
        if (candidates.Count > 0)
        {
            reasons.Add($"왜곡 한글 복원 후보: {candidates[0].Value}");
        }

        var parts = ParseParts(normalizedStem, extension, reasons);
        var composed = parts.Compose();
        var suggested = WindowsFileNameSafety.MakeSafeFileName(composed);
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
        else if (candidates.Count > 0)
        {
            status = RenamePreviewStatus.NeedsReview;
            reasons.Add("왜곡 한글 복원 후보 검수 필요");
        }

        return new RenamePreview
        {
            OriginalPath = path,
            OriginalFileName = originalFileName,
            Parts = parts,
            SuggestedFileName = suggested,
            SuggestedPath = Path.Combine(directory, suggested),
            Status = status,
            Reasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Candidates = candidates
        };
    }

    public FileNameParts ParseParts(string stem, string extension, List<string>? reasons = null)
    {
        var working = NormalizeSeparators(stem);
        var tags = new List<string>();
        string? author = null;

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
                reasons?.Add($"태그 추출: {content}");
            }
            else if (author is null)
            {
                author = content;
                reasons?.Add($"작가 후보 추출: {content}");
            }
            else
            {
                tags.Add(content);
                reasons?.Add($"추가 표식 추출: {content}");
            }

            return " ";
        });

        var authorMatch = AuthorRegex().Match(working);
        if (authorMatch.Success && author is null)
        {
            author = CleanupToken(authorMatch.Groups["author"].Value);
            working = working.Remove(authorMatch.Index, authorMatch.Length).Insert(authorMatch.Index, " ");
            reasons?.Add($"작가 후보 추출: {author}");
        }

        string? episode = null;
        var rangeMatch = EpisodeUnitRangeRegex().Matches(working).LastOrDefault()
            ?? EpisodeCompoundRegex().Matches(working).LastOrDefault();
        if (rangeMatch is not null)
        {
            episode = NormalizeEpisodeToken(rangeMatch.Groups["episode"].Value);
            working = working.Remove(rangeMatch.Index, rangeMatch.Length).Insert(rangeMatch.Index, " ");
            reasons?.Add($"회차 추출: {episode}");
        }
        else
        {
            var singleMatch = EpisodePrefixedSingleRegex().Matches(working).LastOrDefault()
                ?? EpisodeSingleRegex().Matches(working).LastOrDefault();
            if (singleMatch is not null)
            {
                episode = NormalizeEpisodeToken(singleMatch.Groups["episode"].Value);
                working = working.Remove(singleMatch.Index, singleMatch.Length).Insert(singleMatch.Index, " ");
                reasons?.Add($"회차 추출: {episode}");
            }
        }

        var title = CleanupTitle(working);
        return new FileNameParts
        {
            Title = title,
            EpisodeRange = episode,
            Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Author = string.IsNullOrWhiteSpace(author) ? null : author,
            Extension = extension
        };
    }

    private string NormalizeStem(string stem, List<string> reasons)
    {
        var result = KoreanJamoNormalizer.Normalize(stem);
        if (!string.Equals(stem, result, StringComparison.Ordinal))
        {
            reasons.Add("Unicode NFC/한글 자모 결합");
        }

        var mojibakeCandidate = TryRecoverUtf8AsLatin1(result);
        if (mojibakeCandidate is not null)
        {
            result = mojibakeCandidate;
            reasons.Add("UTF-8/Latin-1 깨짐 후보 복구");
        }

        return NormalizeSeparators(result);
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

    private static string NormalizeEpisodeToken(string value)
    {
        var normalized = EpisodePrefixInsideTokenRegex().Replace(value.Trim(), "$1");
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

    private static string CleanupTitle(string value)
    {
        var title = CleanupToken(value);
        title = TitleNoiseRegex().Replace(title, " ");
        return WhitespaceRegex().Replace(title, " ").Trim(' ', '.', ',');
    }

    private static string CleanupToken(string value)
    {
        return WhitespaceRegex().Replace(value.Trim(), " ").Trim(' ', '-', '.', ',', '[', ']', '(', ')');
    }

    [GeneratedRegex("[\\[\\(\\{]([^\\]\\)\\}]{1,80})[\\]\\)\\}]")]
    private static partial Regex BracketContentRegex();

    [GeneratedRegex("(?<!\\d)(?:제|第)?\\s*(?:ep\\.?\\s*)?(?<episode>\\d{1,5}\\s*(?:화|話|회|편|권|巻|부)\\s*[-~]\\s*(?:제|第)?\\s*\\d{1,5}\\s*(?:화|話|회|편|권|巻|부))(?!\\d)", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeUnitRangeRegex();

    [GeneratedRegex("(?<!\\d)(?:제|第)?\\s*(?:ep\\.?\\s*)?(?<episode>\\d{1,5}\\s*(?:화|話|회|편|권|巻|부|ep|episode)?(?:\\s*[.-]\\s*(?:제|第)?\\s*\\d{1,5}\\s*(?:화|話|회|편|권|巻|부|ep|episode)?)+)(?!\\d)", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeCompoundRegex();

    [GeneratedRegex("(?:제|第)?\\s*(?:ep\\.?\\s*)?(?<episode>\\d{1,5}\\s*(?:화|話|회|편|권|巻|부|ep|episode))", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeSingleRegex();

    [GeneratedRegex("(?<!\\d)(?:ep\\.?|episode\\s*)\\s*(?<episode>\\d{1,5})(?!\\d)", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodePrefixedSingleRegex();

    [GeneratedRegex("(?:작가|저자|by)\\s*[:：-]?\\s*(?<author>[가-힣A-Za-z0-9_. ]{2,40})", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("(^|[.-])(?:제|第)\\s*")]
    private static partial Regex EpisodePrefixInsideTokenRegex();

    [GeneratedRegex("\\b(?:완결|번역|단편|컬러|무삭제|개정판|외전)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex TitleNoiseRegex();
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
