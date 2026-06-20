namespace FileTools;

internal sealed record MergeNameProposal(
    string Stem,
    IReadOnlyList<string> OriginalStems,
    IReadOnlyList<string> CorrectedStems,
    NameMergeAnalysis Analysis)
{
    public bool UsedCorrection =>
        CorrectedStems.Count == OriginalStems.Count &&
        !CorrectedStems.SequenceEqual(OriginalStems, StringComparer.OrdinalIgnoreCase);
}

internal static class MergeNameProposalBuilder
{
    public static MergeNameProposal CreateForPaths(
        IReadOnlyList<string> sourcePaths,
        FileToolsSettings settings,
        string fallback = "")
    {
        var originalStems = sourcePaths
            .Select(GetPathStem)
            .Where(static stem => !string.IsNullOrWhiteSpace(stem))
            .Select(static stem => stem.Trim())
            .ToArray();

        var correctedStems = CreateCorrectedStems(sourcePaths, settings);
        var analysisInputs = correctedStems.Length == originalStems.Length
            ? correctedStems
            : originalStems;
        var analysis = NameMergeAnalyzer.Analyze(analysisInputs, fallback);
        if (!analysis.IsReady && !ReferenceEquals(analysisInputs, originalStems))
        {
            analysis = NameMergeAnalyzer.Analyze(originalStems, fallback);
        }

        return new MergeNameProposal(
            analysis.Stem,
            originalStems,
            correctedStems,
            analysis);
    }

    public static MergeNameProposal CreateForStems(IEnumerable<string?> stems, string fallback = "")
    {
        var originalStems = stems
            .Where(static stem => !string.IsNullOrWhiteSpace(stem))
            .Select(static stem => stem!.Trim())
            .ToArray();
        var analysis = NameMergeAnalyzer.Analyze(originalStems, fallback);
        return new MergeNameProposal(analysis.Stem, originalStems, [], analysis);
    }

    private static string[] CreateCorrectedStems(IReadOnlyList<string> sourcePaths, FileToolsSettings settings)
    {
        if (sourcePaths.Count == 0)
        {
            return [];
        }

        KoreanFileNameCorrector corrector;
        try
        {
            corrector = NameCorrectionFactory.Create(settings);
        }
        catch
        {
            return [];
        }

        var corrected = new List<string>(sourcePaths.Count);
        foreach (var path in sourcePaths)
        {
            try
            {
                var preview = corrector.CreatePreview(path);
                if (preview.Status is RenamePreviewStatus.NeedsReview or RenamePreviewStatus.Conflict or RenamePreviewStatus.Skipped)
                {
                    return [];
                }

                var stem = Directory.Exists(path)
                    ? preview.SuggestedFileName
                    : Path.GetFileNameWithoutExtension(preview.SuggestedFileName);
                if (string.IsNullOrWhiteSpace(stem))
                {
                    return [];
                }

                corrected.Add(stem.Trim());
            }
            catch
            {
                return [];
            }
        }

        return corrected.ToArray();
    }

    private static string GetPathStem(string path)
    {
        return Directory.Exists(path)
            ? Path.GetFileName(path)
            : Path.GetFileNameWithoutExtension(path);
    }
}
