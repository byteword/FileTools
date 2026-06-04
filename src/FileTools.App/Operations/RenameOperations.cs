namespace FileTools;

internal static class RenameOperations
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static IReadOnlyList<RenamePreview> CreatePlan(IEnumerable<string> paths, FileToolsSettings settings)
    {
        var planner = new RenamePlanner(CreateFileNameCorrector(settings));
        return planner.CreatePlan(paths);
    }

    public static RenamePreview CreateManualPreview(string path, string fileName, FileToolsSettings settings)
    {
        var preview = CreatePlan([path], settings).First();
        var directory = Path.GetDirectoryName(preview.OriginalPath) ?? "";
        var safeFileName = WindowsFileNameSafety.MakeSafeFileName(fileName);
        return preview with
        {
            SuggestedFileName = safeFileName,
            SuggestedPath = Path.Combine(directory, safeFileName),
            Status = PathComparer.Equals(preview.OriginalFileName, safeFileName)
                ? RenamePreviewStatus.Unchanged
                : RenamePreviewStatus.Ready
        };
    }

    public static OperationResult Apply(IEnumerable<RenamePreview> previews)
    {
        var result = new OperationResult();
        var previewList = previews.ToList();
        var targetGroups = previewList
            .Where(static preview => preview.Status != RenamePreviewStatus.Unchanged)
            .Where(static preview => preview.Status != RenamePreviewStatus.Skipped)
            .Where(static preview => !PathComparer.Equals(preview.OriginalPath, preview.SuggestedPath))
            .GroupBy(static preview => preview.SuggestedPath, PathComparer)
            .ToDictionary(static group => group.Key, static group => group.Count(), PathComparer);

        foreach (var preview in previewList)
        {
            result.AddCandidate();
            try
            {
                if (preview.Status == RenamePreviewStatus.Unchanged)
                {
                    result.AddSkipped(preview.OriginalFileName + " 변경 없음");
                    continue;
                }

                if (preview.Status == RenamePreviewStatus.Skipped)
                {
                    result.AddSkipped(preview.OriginalFileName + " 스킵됨");
                    continue;
                }

                if (PathComparer.Equals(preview.OriginalPath, preview.SuggestedPath))
                {
                    result.AddSkipped(preview.OriginalFileName + " 대상 경로 동일");
                    continue;
                }

                var blockingMessage = GetBlockingApplyMessage(preview, targetGroups);
                if (!string.IsNullOrWhiteSpace(blockingMessage))
                {
                    result.AddSkipped(preview.OriginalFileName + " " + blockingMessage);
                    continue;
                }

                if (Directory.Exists(preview.OriginalPath))
                {
                    Directory.Move(preview.OriginalPath, preview.SuggestedPath);
                }
                else if (File.Exists(preview.OriginalPath))
                {
                    File.Move(preview.OriginalPath, preview.SuggestedPath);
                }
                else
                {
                    result.AddSkipped(preview.OriginalFileName + " 원본 없음");
                    continue;
                }

                result.AddApplied($"{preview.OriginalFileName} -> {preview.SuggestedFileName}");
                FileToolsEnvironment.Log("RENAME", preview.OriginalPath + " -> " + preview.SuggestedPath);
            }
            catch (Exception ex)
            {
                result.AddError(preview.OriginalPath + " | " + ex.Message);
            }
        }

        return result;
    }

    private static string GetBlockingApplyMessage(
        RenamePreview preview,
        IReadOnlyDictionary<string, int> targetGroups)
    {
        var suggestedName = preview.SuggestedFileName.Trim();
        var safeName = WindowsFileNameSafety.MakeSafeFileName(suggestedName);
        if (string.IsNullOrWhiteSpace(suggestedName) ||
            suggestedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(suggestedName, safeName, StringComparison.Ordinal))
        {
            return Localizer.Get("RenameInvalidNameMessage");
        }

        if (string.IsNullOrWhiteSpace(preview.SuggestedPath) ||
            string.IsNullOrWhiteSpace(Path.GetDirectoryName(preview.SuggestedPath)))
        {
            return Localizer.Get("RenameInvalidNameMessage");
        }

        if (targetGroups.TryGetValue(preview.SuggestedPath, out var count) && count > 1)
        {
            return Localizer.Get("RenameDuplicateNameMessage");
        }

        if (!PathComparer.Equals(preview.OriginalPath, preview.SuggestedPath) &&
            (File.Exists(preview.SuggestedPath) || Directory.Exists(preview.SuggestedPath)))
        {
            return Localizer.Format("PlanPreviewTargetExistsFormat", preview.SuggestedPath);
        }

        return "";
    }

    private static KoreanFileNameCorrector CreateFileNameCorrector(FileToolsSettings settings)
    {
        var dictionary = RenameDictionaryStore.Load();
        var parserProfile = RenameParserProfileStore.Load();
        var candidateProfile = RenameCandidateProfileStore.Load(dictionary.CommonPhrases);
        var rules = RenameRuleStore.Load();
        return new KoreanFileNameCorrector(new CorrectionOptions
        {
            ParserProfile = parserProfile,
            RenameDictionary = settings.RenameUseDictionary ? dictionary.Replacements : [],
            CommonPhrases = settings.RenameUseDictionary ? dictionary.CommonPhrases.ToArray() : [],
            CandidateProfile = settings.RenameUseDictionary ? candidateProfile : RenameCandidateProfileStore.CreateDefaultDocument(),
            Rules = rules.Rules
        });
    }
}
