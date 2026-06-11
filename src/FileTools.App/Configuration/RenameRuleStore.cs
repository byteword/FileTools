using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

/// <summary>이름 교정 파이프라인의 단계/유형/실행 모드 모델과 저장소.</summary>
internal enum RenameCorrectionRuleStage
{
    Preprocess,
    UserRewrite,
    Candidate,
    Extract,
    Compose,
    Finalize
}

internal enum RenameCorrectionRuleKind
{
    BuiltInUnicodeJamo,
    BuiltInMojibakeRecovery,
    BuiltInRenameDictionary,
    BuiltInSeparatorNormalization,
    BuiltInObfuscatedHangulCandidate,
    BuiltInBracketMetadataExtraction,
    BuiltInAuthorExtraction,
    BuiltInEpisodeExtraction,
    BuiltInTitleCleanup,
    BuiltInWindowsSafeFileName,
    LiteralReplace,
    PrefixTrim,
    SuffixTrim,
    WhitespaceNormalize,
    SeparatorNormalize,
    RegexReplace
}

internal enum RenameCorrectionRuleMode
{
    Automatic,
    Review,
    CandidateOnly
}

internal sealed class RenameRuleDocument
{
    public List<RenameCorrectionRule> Rules { get; set; } = [];
}

/// <summary>한 규칙의 정의를 표현한다.</summary>
internal sealed class RenameCorrectionRule
{
    public string Id { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string Description { get; set; } = "";

    public RenameCorrectionRuleKind Kind { get; set; }

    public RenameCorrectionRuleStage Stage { get; set; }

    public RenameCorrectionRuleMode Mode { get; set; } = RenameCorrectionRuleMode.Automatic;

    public int Order { get; set; }

    public bool Enabled { get; set; } = true;

    public bool IsBuiltIn { get; set; }

    public bool IsRequired { get; set; }

    public bool IgnoreCase { get; set; } = true;

    public string Source { get; set; } = "";

    public string Replacement { get; set; } = "";

    /// <summary>
    /// 리스트 항목 노출/로그에 쓰기 쉬운 문자열을 반환한다.
    /// </summary>
    public override string ToString()
    {
        var enabled = Enabled || IsRequired ? "On" : "Off";
        var type = IsBuiltIn ? "Built-in" : RenameCorrectionRuleText.GetKindDisplayName(Kind);
        return $"{Order:000} [{RenameCorrectionRuleText.GetStageDisplayName(Stage)}] {enabled} - {DisplayName} ({type}, {RenameCorrectionRuleText.GetModeDisplayName(Mode)})";
    }

    /// <summary>
    /// 클론은 목록 정렬/병합 시 기존 인스턴스 오염을 막는다.
    /// </summary>
    public RenameCorrectionRule Clone()
    {
        return new RenameCorrectionRule
        {
            Id = Id,
            DisplayName = DisplayName,
            Description = Description,
            Kind = Kind,
            Stage = Stage,
            Mode = Mode,
            Order = Order,
            Enabled = Enabled,
            IsBuiltIn = IsBuiltIn,
            IsRequired = IsRequired,
            IgnoreCase = IgnoreCase,
            Source = Source,
            Replacement = Replacement
        };
    }
}

/// <summary>기본 내장 규칙 ID 상수.</summary>
internal static class RenameRuleIds
{
    public const string UnicodeJamo = "built-in.unicode-jamo";
    public const string MojibakeRecovery = "built-in.mojibake-recovery";
    public const string RenameDictionary = "built-in.rename-dictionary";
    public const string SeparatorNormalization = "built-in.separator-normalization";
    public const string ObfuscatedHangulCandidate = "built-in.obfuscated-hangul-candidate";
    public const string BracketMetadataExtraction = "built-in.bracket-metadata-extraction";
    public const string AuthorExtraction = "built-in.author-extraction";
    public const string EpisodeExtraction = "built-in.episode-extraction";
    public const string TitleCleanup = "built-in.title-cleanup";
    public const string WindowsSafeFileName = "built-in.windows-safe-file-name";
}

/// <summary>라벨 변환 유틸.</summary>
internal static class RenameCorrectionRuleText
{
    public static string GetStageDisplayName(RenameCorrectionRuleStage stage) => stage switch
    {
        RenameCorrectionRuleStage.Preprocess => "전처리",
        RenameCorrectionRuleStage.UserRewrite => "사용자 보정",
        RenameCorrectionRuleStage.Candidate => "후보 생성",
        RenameCorrectionRuleStage.Extract => "구성요소 추출",
        RenameCorrectionRuleStage.Compose => "조합",
        RenameCorrectionRuleStage.Finalize => "최종 안전화",
        _ => stage.ToString()
    };

    public static string GetKindDisplayName(RenameCorrectionRuleKind kind) => kind switch
    {
        RenameCorrectionRuleKind.BuiltInUnicodeJamo => "한글 자모 결합",
        RenameCorrectionRuleKind.BuiltInMojibakeRecovery => "깨진 인코딩 복구",
        RenameCorrectionRuleKind.BuiltInRenameDictionary => "기존 이름변경 사전",
        RenameCorrectionRuleKind.BuiltInSeparatorNormalization => "구분자 정규화",
        RenameCorrectionRuleKind.BuiltInObfuscatedHangulCandidate => "왜곡 한글 후보",
        RenameCorrectionRuleKind.BuiltInBracketMetadataExtraction => "괄호 메타데이터 추출",
        RenameCorrectionRuleKind.BuiltInAuthorExtraction => "작가 추출",
        RenameCorrectionRuleKind.BuiltInEpisodeExtraction => "회차 추출",
        RenameCorrectionRuleKind.BuiltInTitleCleanup => "제목 정리",
        RenameCorrectionRuleKind.BuiltInWindowsSafeFileName => "Windows 파일명 안전화",
        RenameCorrectionRuleKind.LiteralReplace => "문자열 치환",
        RenameCorrectionRuleKind.PrefixTrim => "접두사 제거",
        RenameCorrectionRuleKind.SuffixTrim => "접미사 제거",
        RenameCorrectionRuleKind.WhitespaceNormalize => "공백 정규화",
        RenameCorrectionRuleKind.SeparatorNormalize => "기호 정규화",
        RenameCorrectionRuleKind.RegexReplace => "정규식 치환",
        _ => kind.ToString()
    };

    public static string GetModeDisplayName(RenameCorrectionRuleMode mode) => mode switch
    {
        RenameCorrectionRuleMode.Automatic => "자동 적용",
        RenameCorrectionRuleMode.Review => "검토 필요",
        RenameCorrectionRuleMode.CandidateOnly => "후보만 생성",
        _ => mode.ToString()
    };
}

/// <summary>규칙 파일의 로드/저장/정규화를 담당한다.</summary>
internal static class RenameRuleStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string RulePath => Path.Combine(FileToolsEnvironment.AppDataDir, "rename-rules.json");

    /// <summary>
    /// 규칙 목록을 읽고 없으면 기본 규칙을 만든다.
    /// </summary>
    public static RenameRuleDocument Load()
    {
        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        if (!File.Exists(RulePath))
        {
            var document = CreateDefaultDocument();
            Save(document);
            return document;
        }

        try
        {
            return new RenameRuleDocument
            {
                Rules = NormalizeRules(JsonSerializer.Deserialize<RenameRuleDocument>(
                    File.ReadAllText(RulePath),
                    JsonOptions)?.Rules ?? []).ToList()
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            FileToolsEnvironment.Log("RENAME-RULES", ex.Message);
            return CreateDefaultDocument();
        }
    }

    /// <summary>
    /// 중복/정규화된 규칙만 직렬화해 저장한다.
    /// </summary>
    public static void Save(RenameRuleDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(FileToolsEnvironment.AppDataDir);
        File.WriteAllText(RulePath, JsonSerializer.Serialize(
            new RenameRuleDocument { Rules = NormalizeRules(document.Rules).ToList() },
            JsonOptions));
    }

    /// <summary>
    /// 기본 규칙을 반환한다.
    /// </summary>
    public static RenameRuleDocument CreateDefaultDocument()
    {
        return new RenameRuleDocument { Rules = CreateDefaultRules().ToList() };
    }

    /// <summary>
    /// 기본 규칙 + 사용자 규칙을 합쳐 id/순서/필수성 기준으로 정규화한다.
    /// </summary>
    public static IReadOnlyList<RenameCorrectionRule> NormalizeRules(IEnumerable<RenameCorrectionRule> rules)
    {
        var incoming = rules
            .Where(static rule => rule is not null)
            .Select(static rule => rule.Clone())
            .ToList();
        var defaultRules = CreateDefaultRules()
            .ToDictionary(static rule => rule.Id, StringComparer.OrdinalIgnoreCase);
        var incomingById = incoming
            .Where(static rule => !string.IsNullOrWhiteSpace(rule.Id))
            .GroupBy(static rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var normalized = new List<RenameCorrectionRule>();
        foreach (var defaultRule in defaultRules.Values.OrderBy(static rule => rule.Order))
        {
            var merged = defaultRule.Clone();
            if (incomingById.TryGetValue(defaultRule.Id, out var stored))
            {
                merged.Enabled = stored.Enabled || defaultRule.IsRequired;
                merged.Mode = defaultRule.IsRequired ? defaultRule.Mode : stored.Mode;
                merged.Order = stored.Order;
            }

            normalized.Add(merged);
        }

        foreach (var rule in incoming.Where(static rule => !rule.IsBuiltIn))
        {
            var userRule = rule.Clone();
            userRule.Id = string.IsNullOrWhiteSpace(userRule.Id)
                ? "user." + Guid.NewGuid().ToString("N")
                : userRule.Id.Trim();
            userRule.DisplayName = string.IsNullOrWhiteSpace(userRule.DisplayName)
                ? RenameCorrectionRuleText.GetKindDisplayName(userRule.Kind)
                : userRule.DisplayName.Trim();
            userRule.Description = userRule.Description.Trim();
            userRule.Source = userRule.Source.Trim();
            userRule.Replacement = userRule.Replacement.Trim();
            userRule.IsBuiltIn = false;
            userRule.IsRequired = false;
            normalized.Add(userRule);
        }

        return normalized
            .GroupBy(static rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static rule => rule.Stage)
            .ThenBy(static rule => rule.Order)
            .ThenBy(static rule => rule.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>기본 규칙을 생성한다.</summary>
    private static IEnumerable<RenameCorrectionRule> CreateDefaultRules()
    {
        yield return BuiltIn(
            RenameRuleIds.UnicodeJamo,
            "Unicode NFC/한글 자모 결합",
            "한글 자모 분리와 Unicode 정규화를 보정합니다.",
            RenameCorrectionRuleKind.BuiltInUnicodeJamo,
            RenameCorrectionRuleStage.Preprocess,
            10,
            RenameCorrectionRuleMode.Automatic);
        yield return BuiltIn(
            RenameRuleIds.MojibakeRecovery,
            "UTF-8/Latin-1 깨짐 복구",
            "깨진 인코딩처럼 보이는 파일명을 한글 후보로 복구합니다.",
            RenameCorrectionRuleKind.BuiltInMojibakeRecovery,
            RenameCorrectionRuleStage.Preprocess,
            20,
            RenameCorrectionRuleMode.Review);
        yield return BuiltIn(
            RenameRuleIds.RenameDictionary,
            "기존 이름변경 사전 적용",
            "설정의 이름변경 사전(source -> replacement)을 적용합니다.",
            RenameCorrectionRuleKind.BuiltInRenameDictionary,
            RenameCorrectionRuleStage.UserRewrite,
            10,
            RenameCorrectionRuleMode.Automatic);
        yield return BuiltIn(
            RenameRuleIds.SeparatorNormalization,
            "구분자/공백 정규화",
            "전각 괄호, 물결표, 밑줄, 반복 공백을 정리합니다.",
            RenameCorrectionRuleKind.BuiltInSeparatorNormalization,
            RenameCorrectionRuleStage.UserRewrite,
            20,
            RenameCorrectionRuleMode.Automatic);
        yield return BuiltIn(
            RenameRuleIds.ObfuscatedHangulCandidate,
            "왜곡 한글 복원 후보",
            "ㅇr 같은 왜곡 한글 표기를 한글 후보로 제안합니다.",
            RenameCorrectionRuleKind.BuiltInObfuscatedHangulCandidate,
            RenameCorrectionRuleStage.Candidate,
            10,
            RenameCorrectionRuleMode.CandidateOnly);
        yield return BuiltIn(
            RenameRuleIds.BracketMetadataExtraction,
            "괄호 태그/작가 추출",
            "괄호 안 문자열을 태그나 작가 후보로 분리합니다.",
            RenameCorrectionRuleKind.BuiltInBracketMetadataExtraction,
            RenameCorrectionRuleStage.Extract,
            10,
            RenameCorrectionRuleMode.Automatic);
        yield return BuiltIn(
            RenameRuleIds.AuthorExtraction,
            "작가 표기 추출",
            "작가, 저자, by 패턴을 작가 필드로 분리합니다.",
            RenameCorrectionRuleKind.BuiltInAuthorExtraction,
            RenameCorrectionRuleStage.Extract,
            20,
            RenameCorrectionRuleMode.Automatic);
        yield return BuiltIn(
            RenameRuleIds.EpisodeExtraction,
            "회차 추출/정규화",
            "화, 권, ep, 범위 표기를 회차 필드로 분리합니다.",
            RenameCorrectionRuleKind.BuiltInEpisodeExtraction,
            RenameCorrectionRuleStage.Extract,
            30,
            RenameCorrectionRuleMode.Automatic);
        yield return BuiltIn(
            RenameRuleIds.TitleCleanup,
            "제목 노이즈 정리",
            "제목에 남은 태그성 단어와 가장자리 기호를 정리합니다.",
            RenameCorrectionRuleKind.BuiltInTitleCleanup,
            RenameCorrectionRuleStage.Compose,
            10,
            RenameCorrectionRuleMode.Automatic);
        yield return BuiltIn(
            RenameRuleIds.WindowsSafeFileName,
            "Windows 파일명 안전화",
            "금지 문자와 예약어를 최종 파일명에서 보정합니다.",
            RenameCorrectionRuleKind.BuiltInWindowsSafeFileName,
            RenameCorrectionRuleStage.Finalize,
            10,
            RenameCorrectionRuleMode.Automatic,
            isRequired: true);
    }

    /// <summary>BuiltIn 규칙 객체를 한 번만 만들어준다.</summary>
    private static RenameCorrectionRule BuiltIn(
        string id,
        string displayName,
        string description,
        RenameCorrectionRuleKind kind,
        RenameCorrectionRuleStage stage,
        int order,
        RenameCorrectionRuleMode mode,
        bool isRequired = false)
    {
        return new RenameCorrectionRule
        {
            Id = id,
            DisplayName = displayName,
            Description = description,
            Kind = kind,
            Stage = stage,
            Order = order,
            Mode = mode,
            Enabled = true,
            IsBuiltIn = true,
            IsRequired = isRequired
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
