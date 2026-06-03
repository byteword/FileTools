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
        foreach (var preview in previews)
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

    private static KoreanFileNameCorrector CreateFileNameCorrector(FileToolsSettings settings)
    {
        var dictionary = RenameDictionaryStore.Load();
        var rules = RenameRuleStore.Load();
        return new KoreanFileNameCorrector(new CorrectionOptions
        {
            RenameDictionary = settings.RenameUseDictionary ? dictionary.Replacements : [],
            CommonPhrases = settings.RenameUseDictionary ? dictionary.CommonPhrases.ToArray() : [],
            Rules = rules.Rules
        });
    }
}
