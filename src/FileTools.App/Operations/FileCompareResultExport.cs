using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

/// <summary>
/// 비교 결과 JSON 내보내기 DTO와 직렬화 진입점을 묶는다.
/// </summary>
internal sealed record FileCompareExportDocument(
    string DocumentType,
    int SchemaVersion,
    DateTime CreatedAtUtc,
    FileCompareExportOptions Options,
    string DuplicateKeepMode,
    IReadOnlyList<FileCompareExportTarget> Targets,
    IReadOnlyList<FileCompareExportPair> Pairs,
    IReadOnlyList<FileCompareExportDuplicateGroup> DuplicateGroups);

/// <summary>
/// 비교 옵션을 저장 포맷에 맞는 단순 타입 DTO로 변환한다.
/// </summary>
internal sealed record FileCompareExportOptions(
    bool CompareFileName,
    string NameMatchMode,
    string CommonNameThresholdMode,
    int CommonNameMinimumCharacters,
    double CommonNameMinimumPercent,
    bool CompareCreatedTime,
    bool CompareModifiedTime,
    bool CompareFileSize,
    bool CompareContent,
    string ContentMode,
    string RangeMode,
    long RangeBytes,
    long RangeOffsetBytes,
    string RangeUnit,
    string ArchiveMode,
    string ArchiveEntryOrder,
    string ArchiveEntryLimitMode,
    int ArchiveEntryLimitCount,
    bool ArchiveCompareSameRelativePathOnly,
    bool EnableEarlyExit,
    bool UseHashCache,
    double PartialMatchThreshold,
    double ByteToBytePrefilterRatio);

/// <summary>
/// 비교 대상(입력 파일/폴더) 메타정보 DTO.
/// </summary>
internal sealed record FileCompareExportTarget(
    string Path,
    string RelativePath,
    string? RootPath);

/// <summary>
/// 비교 결과 한 쌍을 직렬화 가능한 형태로 보존한다.
/// </summary>
internal sealed record FileCompareExportPair(
    string LeftPath,
    string RightPath,
    string Status,
    double MatchRatio,
    string Reason,
    IReadOnlyList<FileCompareExportCriterion> Criteria);

/// <summary>
/// 비교 판정 기준별 상태/비율/상세 사유 DTO.
/// </summary>
internal sealed record FileCompareExportCriterion(
    string Name,
    string Status,
    double MatchRatio,
    string Detail);

/// <summary>
/// 중복 후보 그룹과 삭제 후보 집합을 직렬화한다.
/// </summary>
internal sealed record FileCompareExportDuplicateGroup(
    int Number,
    string KeepPath,
    IReadOnlyList<string> DeleteCandidates,
    IReadOnlyList<string> Paths);

/// <summary>
/// 비교 리포트를 JSON으로 저장/생성 형식으로 변환한다.
/// </summary>
internal static class FileCompareResultExport
{
    public const string DocumentType = "FileTools.FileCompareResult";
    public const int SchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 비교 리포트를 지정 경로에 JSON으로 저장한다.
    /// </summary>
    public static void Save(
        string path,
        FileCompareReport report,
        FileCompareOptions options,
        FileCompareDuplicateKeepMode keepMode,
        IReadOnlyList<FileCompareDuplicateGroup> duplicateGroups)
    {
        var document = CreateDocument(report, options, keepMode, duplicateGroups);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// 문서 객체로 먼저 구성해 저장/미리보기에서 재사용한다.
    /// </summary>
    public static FileCompareExportDocument CreateDocument(
        FileCompareReport report,
        FileCompareOptions options,
        FileCompareDuplicateKeepMode keepMode,
        IReadOnlyList<FileCompareDuplicateGroup> duplicateGroups)
    {
        return new FileCompareExportDocument(
            DocumentType,
            SchemaVersion,
            DateTime.UtcNow,
            CreateOptions(options),
            keepMode.ToString(),
            report.Targets.Select(static target => new FileCompareExportTarget(
                target.Path,
                target.RelativePath,
                target.RootPath)).ToArray(),
            report.Pairs.Select(static pair => new FileCompareExportPair(
                pair.Left.Path,
                pair.Right.Path,
                pair.Status.ToString(),
                pair.MatchRatio,
                pair.Reason,
                pair.Criteria.Select(static criterion => new FileCompareExportCriterion(
                    criterion.Name,
                    criterion.Status.ToString(),
                    criterion.MatchRatio,
                    criterion.Detail)).ToArray())).ToArray(),
            duplicateGroups.Select(static group => new FileCompareExportDuplicateGroup(
                group.Number,
                group.KeepPath,
                group.DeleteCandidates,
                group.Paths)).ToArray());
    }

    /// <summary>
    /// 옵션을 직렬화에 적합한 단순 타입으로 변환한다.
    /// </summary>
    private static FileCompareExportOptions CreateOptions(FileCompareOptions options)
    {
        return new FileCompareExportOptions(
            options.CompareFileName,
            options.NameMatchMode.ToString(),
            options.CommonNameThresholdMode.ToString(),
            options.CommonNameMinimumCharacters,
            options.CommonNameMinimumPercent,
            options.CompareCreatedTime,
            options.CompareModifiedTime,
            options.CompareFileSize,
            options.CompareContent,
            options.ContentMode.ToString(),
            options.RangeMode.ToString(),
            options.RangeBytes,
            options.RangeOffsetBytes,
            options.RangeUnit.ToString(),
            options.ArchiveMode.ToString(),
            options.ArchiveEntryOrder.ToString(),
            options.ArchiveEntryLimitMode.ToString(),
            options.ArchiveEntryLimitCount,
            options.ArchiveCompareSameRelativePathOnly,
            options.EnableEarlyExit,
            options.UseHashCache,
            options.PartialMatchThreshold,
            options.ByteToBytePrefilterRatio);
    }
}
