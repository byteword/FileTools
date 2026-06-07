using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileTools;

internal sealed record FileCompareExportDocument(
    string DocumentType,
    int SchemaVersion,
    DateTime CreatedAtUtc,
    FileCompareExportOptions Options,
    string DuplicateKeepMode,
    IReadOnlyList<FileCompareExportTarget> Targets,
    IReadOnlyList<FileCompareExportPair> Pairs,
    IReadOnlyList<FileCompareExportDuplicateGroup> DuplicateGroups);

internal sealed record FileCompareExportOptions(
    bool CompareFileName,
    string NameMatchMode,
    bool CompareCreatedTime,
    bool CompareModifiedTime,
    bool CompareFileSize,
    bool CompareContent,
    string ContentMode,
    string RangeMode,
    long RangeBytes,
    string ArchiveMode,
    string ArchiveEntryOrder,
    bool EnableEarlyExit,
    bool UseHashCache,
    double PartialMatchThreshold,
    double ByteToBytePrefilterRatio);

internal sealed record FileCompareExportTarget(
    string Path,
    string RelativePath,
    string? RootPath);

internal sealed record FileCompareExportPair(
    string LeftPath,
    string RightPath,
    string Status,
    double MatchRatio,
    string Reason,
    IReadOnlyList<FileCompareExportCriterion> Criteria);

internal sealed record FileCompareExportCriterion(
    string Name,
    string Status,
    double MatchRatio,
    string Detail);

internal sealed record FileCompareExportDuplicateGroup(
    int Number,
    string KeepPath,
    IReadOnlyList<string> DeleteCandidates,
    IReadOnlyList<string> Paths);

internal static class FileCompareResultExport
{
    public const string DocumentType = "FileTools.FileCompareResult";
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

    private static FileCompareExportOptions CreateOptions(FileCompareOptions options)
    {
        return new FileCompareExportOptions(
            options.CompareFileName,
            options.NameMatchMode.ToString(),
            options.CompareCreatedTime,
            options.CompareModifiedTime,
            options.CompareFileSize,
            options.CompareContent,
            options.ContentMode.ToString(),
            options.RangeMode.ToString(),
            options.RangeBytes,
            options.ArchiveMode.ToString(),
            options.ArchiveEntryOrder.ToString(),
            options.EnableEarlyExit,
            options.UseHashCache,
            options.PartialMatchThreshold,
            options.ByteToBytePrefilterRatio);
    }
}
