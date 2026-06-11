namespace FileTools;

/// <summary>패턴 후보 피드백/랭킹에 쓰이는 핵심 DTO와 계산기.</summary>
internal sealed record FileNamePatternFeedback
{
    public required string OriginalFileName { get; init; }

    public required string SelectedFileName { get; init; }

    public required string ParsePattern { get; init; }

    public required string RenderPattern { get; init; }

    public IReadOnlyList<string> CandidatePatterns { get; init; } = [];

    public DateTimeOffset ConfirmedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record FileNamePatternRankCandidate
{
    public required string ParsePattern { get; init; }

    public required string RenderPattern { get; init; }

    public string CandidateFileName { get; init; } = "";

    public double BaseScore { get; init; } = 0.5;

    public static FileNamePatternRankCandidate FromRenderCandidate(
        string parsePattern,
        FileNameRenderCandidate candidate)
    {
        return new FileNamePatternRankCandidate
        {
            ParsePattern = parsePattern,
            RenderPattern = candidate.Pattern.Template,
            CandidateFileName = candidate.FileName,
            BaseScore = candidate.Score
        };
    }
}

internal sealed record FileNamePatternRankResult
{
    public required FileNamePatternRankCandidate Candidate { get; init; }

    public required double Score { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];
}

internal sealed class FileNamePatternStatisticsRankerOptions
{
    public double FeedbackHalfLifeDays { get; init; } = 45;
}

/// <summary>파일명 패턴 피드백 정규화.</summary>
internal static class FileNamePatternFeedbackNormalizer
{
    public static IReadOnlyList<FileNamePatternFeedback> Normalize(IEnumerable<FileNamePatternFeedback> feedback)
    {
        return feedback
            .Select(NormalizeOne)
            .Where(static item => item is not null)
            .Cast<FileNamePatternFeedback>()
            .OrderBy(static item => item.ConfirmedAtUtc)
            .ToArray();
    }

    /// <summary>
    /// 공백/중복 제거 후 시간 정규화로 통계 신뢰도를 통일한다.
    /// </summary>
    private static FileNamePatternFeedback? NormalizeOne(FileNamePatternFeedback feedback)
    {
        var parsePattern = feedback.ParsePattern.Trim();
        var renderPattern = feedback.RenderPattern.Trim();
        if (parsePattern.Length == 0 || renderPattern.Length == 0)
        {
            return null;
        }

        return feedback with
        {
            OriginalFileName = feedback.OriginalFileName.Trim(),
            SelectedFileName = feedback.SelectedFileName.Trim(),
            ParsePattern = parsePattern,
            RenderPattern = renderPattern,
            CandidatePatterns = feedback.CandidatePatterns
                .Select(static pattern => pattern.Trim())
                .Where(static pattern => pattern.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            ConfirmedAtUtc = feedback.ConfirmedAtUtc.ToUniversalTime()
        };
    }
}

/// <summary>과거 피드백 기반으로 후보 점수를 산정한다.</summary>
internal static class FileNamePatternStatisticsRanker
{
    public static IReadOnlyList<FileNamePatternRankResult> Rank(
        IEnumerable<FileNamePatternRankCandidate> candidates,
        IEnumerable<FileNamePatternFeedback> feedback,
        DateTimeOffset? now = null,
        FileNamePatternStatisticsRankerOptions? options = null)
    {
        options ??= new FileNamePatternStatisticsRankerOptions();
        var normalizedCandidates = candidates
            .Where(static candidate =>
                !string.IsNullOrWhiteSpace(candidate.ParsePattern) &&
                !string.IsNullOrWhiteSpace(candidate.RenderPattern))
            .ToArray();
        if (normalizedCandidates.Length == 0)
        {
            return [];
        }

        var referenceTime = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var normalizedFeedback = FileNamePatternFeedbackNormalizer.Normalize(feedback);
        var weightedFeedback = normalizedFeedback
            .Select(item => new WeightedPatternFeedback(
                item,
                CalculateWeight(item.ConfirmedAtUtc, referenceTime, options.FeedbackHalfLifeDays)))
            .Where(static item => item.Weight > 0)
            .ToArray();
        var totalWeight = weightedFeedback.Sum(static item => item.Weight);

        return normalizedCandidates
            .Select(candidate => RankOne(candidate, weightedFeedback, totalWeight))
            .OrderByDescending(static result => result.Score)
            .ThenBy(static result => result.Candidate.CandidateFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>단일 후보에 대해 parse/render 히스토리를 반영해 점수를 만든다.</summary>
    private static FileNamePatternRankResult RankOne(
        FileNamePatternRankCandidate candidate,
        IReadOnlyList<WeightedPatternFeedback> feedback,
        double totalWeight)
    {
        var pairWeight = feedback
            .Where(item =>
                string.Equals(item.Feedback.ParsePattern, candidate.ParsePattern, StringComparison.Ordinal) &&
                string.Equals(item.Feedback.RenderPattern, candidate.RenderPattern, StringComparison.Ordinal))
            .Sum(static item => item.Weight);
        var renderWeight = feedback
            .Where(item => string.Equals(item.Feedback.RenderPattern, candidate.RenderPattern, StringComparison.Ordinal))
            .Sum(static item => item.Weight);
        var parseWeight = feedback
            .Where(item => string.Equals(item.Feedback.ParsePattern, candidate.ParsePattern, StringComparison.Ordinal))
            .Sum(static item => item.Weight);

        var pairScore = totalWeight <= 0 ? 0 : pairWeight / totalWeight;
        var renderScore = totalWeight <= 0 ? 0 : renderWeight / totalWeight;
        var parseScore = totalWeight <= 0 ? 0 : parseWeight / totalWeight;
        var score = Math.Clamp(
            Math.Clamp(candidate.BaseScore, 0, 1) * 0.55 +
            pairScore * 0.30 +
            renderScore * 0.10 +
            parseScore * 0.05,
            0,
            1);

        var reasons = new List<string>
        {
            $"base render score {candidate.BaseScore:0.00}"
        };
        if (pairWeight > 0)
        {
            reasons.Add($"parse/render history weight {pairWeight:0.00}");
        }

        if (renderWeight > pairWeight)
        {
            reasons.Add($"render pattern history weight {renderWeight:0.00}");
        }

        if (parseWeight > pairWeight)
        {
            reasons.Add($"parse pattern history weight {parseWeight:0.00}");
        }

        return new FileNamePatternRankResult
        {
            Candidate = candidate,
            Score = score,
            Reasons = reasons
        };
    }

    private static double CalculateWeight(DateTimeOffset confirmedAtUtc, DateTimeOffset nowUtc, double halfLifeDays)
    {
        if (halfLifeDays <= 0)
        {
            return 1;
        }

        var ageDays = Math.Max(0, (nowUtc - confirmedAtUtc.ToUniversalTime()).TotalDays);
        return Math.Pow(0.5, ageDays / halfLifeDays);
    }

    private sealed record WeightedPatternFeedback(FileNamePatternFeedback Feedback, double Weight);
}
