using FileTools.Correction;

namespace FileTools;

/// <summary>
/// 이름 교정 플러그인 결과를 기존 미리보기 후보에 합성한다.
/// </summary>
internal static class NameCorrectionPluginHost
{
    /// <summary>플랫폼별 경로 비교 정책에 맞는 후보 중복 비교자.</summary>
    private static readonly StringComparer CandidateComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// 미리보기에 대해 교정 플러그인 후보를 추가해 결과 목록을 반환한다.
    /// </summary>
    public static IReadOnlyList<RenamePreview> AddPluginCandidates(
        IReadOnlyList<RenamePreview> previews,
        FileToolsSettings settings)
    {
        var options = RenameCorrectionPluginDefaults.Normalize(settings.RenameCorrectionPlugins);
        if (!options.Enabled || previews.Count == 0)
        {
            return previews;
        }

        var plugins = NameCorrectionPluginCatalog.Discover()
            .Where(plugin => IsPluginEnabled(plugin, options))
            .Where(plugin => SupportsLanguage(plugin.Descriptor, options.Language))
            .ToArray();
        if (plugins.Length == 0)
        {
            return previews;
        }

        var commonPhrases = LoadCommonPhrases(settings);
        var result = new List<RenamePreview>(previews.Count);
        foreach (var preview in previews)
        {
            result.Add(AddPluginCandidates(preview, options, plugins, commonPhrases));
        }

        return result;
    }

    /// <summary>
    /// 플러그인 정의값과 사용자 설정을 결합해 정상화된 설정으로 반환한다.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildSettings(
        LoadedNameCorrectionPlugin plugin,
        RenameCorrectionPluginConfiguration configuration)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in plugin.SettingDefinitions)
        {
            values[definition.Key] = definition.DefaultValue;
        }

        foreach (var pair in configuration.Settings)
        {
            values[pair.Key] = pair.Value;
        }

        try
        {
            return plugin.Instance.NormalizeSettings(values);
        }
        catch (Exception ex)
        {
            FileToolsEnvironment.Log("RENAME-PLUGIN", $"Plugin settings normalization failed: {plugin.Descriptor.Id} | {ex.Message}");
            return values;
        }
    }

    /// <summary>
    /// 미리보기에 대해 플러그인별로 후보를 추가하고 중복을 제거해 반환한다.
    /// </summary>
    /// <remarks>
    /// 1) 입력 미리보기를 기준으로 플러그인 후보를 생성하고,
    /// 2) 기존 후보를 유지한 채 파일명만 정규화한 후보를 추가하며,
    /// 3) 중복이 아니면 충돌 시 충돌 후보를 최소화한다.
    /// </remarks>
    private static RenamePreview AddPluginCandidates(
        RenamePreview preview,
        RenameCorrectionPluginOptions options,
        IReadOnlyList<LoadedNameCorrectionPlugin> plugins,
        IReadOnlyList<string> commonPhrases)
    {
        var candidates = preview.Candidates.ToList();
        var seen = new HashSet<string>(candidates.Select(static candidate => candidate.Value), CandidateComparer);
        var added = false;

        foreach (var plugin in plugins)
        {
            var configuration = RenameCorrectionPluginDefaults.GetOrCreatePlugin(options, plugin.Descriptor.Id);
            var request = CreateRequest(preview, options.Language, commonPhrases);
            IReadOnlyList<PluginCorrectionCandidate> pluginCandidates;
            try
            {
                pluginCandidates = plugin.Instance.GenerateCandidates(
                    request,
                    BuildSettings(plugin, configuration),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                FileToolsEnvironment.Log("RENAME-PLUGIN", $"Plugin candidate generation failed: {plugin.Descriptor.Id} | {ex.Message}");
                continue;
            }

            foreach (var pluginCandidate in pluginCandidates)
            {
                var fileName = ToCandidateFileName(pluginCandidate, preview.Parts.Extension);
                var safeFileName = WindowsFileNameSafety.MakeSafeFileName(fileName.Trim());
                if (string.IsNullOrWhiteSpace(safeFileName) || !seen.Add(safeFileName))
                {
                    continue;
                }

                candidates.Add(new NameCorrectionCandidate
                {
                    Value = safeFileName,
                    Score = Math.Clamp(pluginCandidate.Score, 0, 1),
                    Reason = CreateCandidateReason(plugin, pluginCandidate),
                    RequiresReview = true
                });
                added = true;
            }
        }

        if (!added)
        {
            return preview;
        }

        return preview with
        {
            Status = preview.Status == RenamePreviewStatus.Conflict
                ? RenamePreviewStatus.Conflict
                : RenamePreviewStatus.NeedsReview,
            Reasons = preview.Reasons
                .Concat(["교정 플러그인 후보 검토 필요"])
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Candidates = candidates
        };
    }

    /// <summary>플러그인 enable 플래그와 선택 상태를 검사한다.</summary>
    private static bool IsPluginEnabled(
        LoadedNameCorrectionPlugin plugin,
        RenameCorrectionPluginOptions options)
    {
        return options.Plugins.Any(configuration =>
            configuration.Enabled &&
            string.Equals(configuration.PluginId, plugin.Descriptor.Id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>플러그인 descriptor 언어 지원 여부를 검사한다.</summary>
    private static bool SupportsLanguage(NameCorrectionPluginDescriptor descriptor, string language)
    {
        return descriptor.SupportedLanguages.Count == 0 ||
            descriptor.SupportedLanguages.Any(supported =>
                string.Equals(supported, "*", StringComparison.Ordinal) ||
                string.Equals(supported, language, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>플러그인 요청 객체를 구성한다.</summary>
    private static NameCorrectionRequest CreateRequest(
        RenamePreview preview,
        string language,
        IReadOnlyList<string> commonPhrases)
    {
        var isDirectory = Directory.Exists(preview.OriginalPath);
        return new NameCorrectionRequest
        {
            OriginalPath = preview.OriginalPath,
            OriginalFileName = preview.OriginalFileName,
            OriginalStem = GetStem(preview.OriginalFileName, isDirectory),
            SuggestedFileName = preview.SuggestedFileName,
            SuggestedStem = GetStem(preview.SuggestedFileName, isDirectory),
            Extension = preview.Parts.Extension,
            Title = preview.Parts.Title,
            EpisodeRange = preview.Parts.EpisodeRange,
            Author = preview.Parts.Author,
            Tags = preview.Parts.Tags,
            Language = language,
            IsDirectory = isDirectory,
            CommonPhrases = commonPhrases
        };
    }

    /// <summary>공통어 배열은 사용 안 함일 때 빈 배열을 반환한다.</summary>
    private static IReadOnlyList<string> LoadCommonPhrases(FileToolsSettings settings)
    {
        if (!settings.RenameUseDictionary)
        {
            return [];
        }

        try
        {
            return RenameDictionaryStore.Load().CommonPhrases
                .Where(static phrase => !string.IsNullOrWhiteSpace(phrase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            FileToolsEnvironment.Log("RENAME-PLUGIN", $"Common phrase load failed: {ex.Message}");
            return [];
        }
    }

    /// <summary>디렉토리면 전체 파일명, 아니면 확장자 없는 스템을 사용한다.</summary>
    private static string GetStem(string fileName, bool isDirectory)
    {
        return isDirectory ? fileName : Path.GetFileNameWithoutExtension(fileName);
    }

    /// <summary>파일명 후보값에 원본 확장자 보정이 필요한지 판단한다.</summary>
    private static string ToCandidateFileName(PluginCorrectionCandidate candidate, string extension)
    {
        var value = Path.GetFileName(candidate.Value.Trim());
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        if (candidate.IsFullFileName ||
            string.IsNullOrWhiteSpace(extension) ||
            string.Equals(Path.GetExtension(value), extension, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return value + extension;
    }

    private static string CreateCandidateReason(
        LoadedNameCorrectionPlugin plugin,
        PluginCorrectionCandidate candidate)
    {
        var source = string.IsNullOrWhiteSpace(candidate.Source)
            ? plugin.Descriptor.DisplayName
            : candidate.Source.Trim();
        var reason = candidate.Reason.Trim();
        return reason.Length == 0 ? source : $"{source}: {reason}";
    }
}
