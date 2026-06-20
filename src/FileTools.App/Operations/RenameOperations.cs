namespace FileTools;

/// <summary>
/// 이름 변경 계획 생성/적용의 진입점이다.
/// </summary>
internal static class RenameOperations
{
    /// <summary>
    /// 경로 동등성 비교 기준(운영체제별 대소문자 규칙 반영).
    /// </summary>
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// 경로 목록을 입력으로 받아 이름 변경 미리보기(후보) 계획을 생성한다.
    /// </summary>
    /// <remarks>
    /// 입력 경로를 기반으로 플래너를 만들고, 플러그인 후보를 추가 결합한다.
    /// </remarks>
    /// <param name="paths">입력 경로</param>
    /// <param name="settings">이름 변경/교정 설정</param>
    /// <returns>미리보기 후보 목록</returns>
    public static IReadOnlyList<RenamePreview> CreatePlan(IEnumerable<string> paths, FileToolsSettings settings)
    {
        var planner = new RenamePlanner(CreateFileNameCorrector(settings));
        var previews = planner.CreatePlan(paths);
        return NameCorrectionPluginHost.AddPluginCandidates(previews, settings);
    }

    /// <summary>
    /// 단일 경로의 수동 파일명을 기준으로 미리보기를 생성한다.
    /// </summary>
    /// <remarks>
    /// 기존 자동 생성 후보를 하나 재생성한 뒤 수동 파일명으로 덮어쓴다.
    /// </remarks>
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

    /// <summary>
    /// 미리보기 결과를 실제 파일/폴더 이동으로 적용한다.
    /// </summary>
    /// <remarks>
    /// 후보 집계 → 유효성 검사 → 실제 이동/실패 집계를 한 루프로 처리해 중간 실패가 전체를 멈추지 않게 한다.
    /// </remarks>
    /// <param name="previews">적용 대상 미리보기 목록</param>
    /// <returns>적용 결과 집계</returns>
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

    /// <summary>
    /// 한 미리보기에 대해 적용 가능한지 블로킹 사유를 계산한다.
    /// </summary>
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

    /// <summary>
    /// 이름 교정기 인스턴스를 사용자 설정 기반으로 생성한다.
    /// </summary>
    private static KoreanFileNameCorrector CreateFileNameCorrector(FileToolsSettings settings)
    {
        return NameCorrectionFactory.Create(settings);
    }
}
