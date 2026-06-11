using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;

namespace FileTools;

internal enum ArchiveMergeLayout
{
    GroupByArchiveName,
    PreserveInternalPaths
}

internal enum ArchiveMergeCollisionPolicy
{
    AutoNumber,
    SameContentKeepFirst,
    Ask,
    Abort
}

internal enum ArchiveMergeDuplicatePolicy
{
    KeepBoth,
    SameContentKeepFirst,
    Ask
}

internal enum ArchiveMergeFailurePolicy
{
    AbortAll,
    SkipFailedArchive,
    SkipFailedEntry
}

internal enum ArchiveMergeOutputNamePolicy
{
    CommonStem,
    ParentFolderName,
    Timestamp,
    Manual
}

internal enum ArchiveMergeCompressionLevel
{
    StoreOnly,
    Fast,
    Default,
    Maximum
}

/// <summary>
/// 아카이브 병합 실행에 필요한 기본 옵션 집합이다.
/// </summary>
internal sealed class ArchiveMergeOptions
{
    /// <summary>
    /// 병합 계획 식별자.
    /// </summary>
    public string PlanId { get; set; } = Guid.NewGuid().ToString("N");

    public List<string> SourcePaths { get; set; } = [];

    public string OutputPath { get; set; } = "";

    public ArchiveMergeLayout Layout { get; set; } = ArchiveMergeLayout.GroupByArchiveName;

    public ArchiveMergeCollisionPolicy CollisionPolicy { get; set; } = ArchiveMergeCollisionPolicy.AutoNumber;

    public ArchiveMergeDuplicatePolicy DuplicatePolicy { get; set; } = ArchiveMergeDuplicatePolicy.KeepBoth;

    public ArchiveMergeFailurePolicy FailurePolicy { get; set; } = ArchiveMergeFailurePolicy.AbortAll;

    public ArchiveMergeCompressionLevel CompressionLevel { get; set; } = ArchiveMergeCompressionLevel.Default;

    public bool DeleteOriginals { get; set; }

    /// <summary>
    /// 현재 옵션을 값 복제로 안전하게 복제한다.
    /// </summary>
    public ArchiveMergeOptions Clone()
    {
        return new ArchiveMergeOptions
        {
            PlanId = PlanId,
            SourcePaths = SourcePaths.ToList(),
            OutputPath = OutputPath,
            Layout = Layout,
            CollisionPolicy = CollisionPolicy,
            DuplicatePolicy = DuplicatePolicy,
            FailurePolicy = FailurePolicy,
            CompressionLevel = CompressionLevel,
            DeleteOriginals = DeleteOriginals
        };
    }
}

/// <summary>
/// 아카이브 본문/엔트리 스트림 접근을 추상화한다.
/// </summary>
internal interface IArchiveReader : IDisposable
{
    IReadOnlyList<ArchiveEntryInfo> Entries { get; }

    Stream OpenEntryStream(ArchiveEntryInfo entry);
}

/// <summary>
/// 병합용 ZIP 출력기 인터페이스.
/// </summary>
internal interface IArchiveWriter : IDisposable
{
    void WriteDirectory(string entryPath, ArchiveEntryMetadata metadata);

    void WriteFile(string entryPath, Stream source, ArchiveEntryMetadata metadata, CancellationToken cancellationToken);

    void Complete();
}

/// <summary>
/// 충돌/중복/인코딩 선택을 UI에서 주입받기 위한 질문 싱크.
/// </summary>
internal interface IArchiveMergeQuestionSink
{
    Encoding? ChooseEncoding(ArchiveEncodingQuestion question);

    ArchiveMergeNameCollisionDecision ResolveNameCollision(ArchiveMergeNameCollisionQuestion question);

    ArchiveMergeDuplicateContentDecision ResolveDuplicateContent(ArchiveMergeDuplicateContentQuestion question);
}

/// <summary>
/// 병합 로직의 파일시스템 접근을 캡슐화한다.
/// </summary>
internal interface IArchiveMergeFileSystem
{
    void CreateDirectory(string path);

    bool FileExists(string path);

    bool DirectoryExists(string path);

    string CreateTempArchivePath(string outputDirectory);

    void MoveFile(string sourcePath, string destinationPath);

    void DeleteFileIfExists(string path);
}

internal enum ArchiveMergeNameCollisionDecision
{
    AutoNumberCurrent,
    SkipCurrent,
    Abort
}

internal enum ArchiveMergeDuplicateContentDecision
{
    KeepBoth,
    SkipCurrent,
    Abort
}

internal sealed record ArchiveEncodingQuestion(
    string ArchivePath,
    IReadOnlyList<ArchiveEncodingCandidateResult> Candidates);

internal sealed record ArchiveEncodingCandidateResult(
    string DisplayName,
    string Description,
    Encoding Encoding,
    int Score,
    IReadOnlyList<string> PreviewNames,
    bool IsSystemDefault = false);

internal sealed record ArchiveMergeQuestionEntry(
    string SourceArchivePath,
    string OriginalPath,
    string TargetPath,
    bool IsDirectory,
    long Size);

internal sealed record ArchiveMergeNameCollisionQuestion(
    string TargetPath,
    ArchiveMergeQuestionEntry ExistingEntry,
    ArchiveMergeQuestionEntry CurrentEntry);

internal sealed record ArchiveMergeDuplicateContentQuestion(
    string Hash,
    ArchiveMergeQuestionEntry FirstEntry,
    ArchiveMergeQuestionEntry CurrentEntry);

internal enum ArchiveMergePreviewSourceStatus
{
    Ready,
    Blocked
}

internal enum ArchiveMergePreviewEntryStatus
{
    Ready,
    CollisionRenamed,
    DuplicateSkipped,
    Skipped,
    Blocked
}

internal sealed record ArchiveMergePreviewSource(
    string SourcePath,
    ArchiveMergePreviewSourceStatus Status,
    string Reason,
    int EntryCount);

internal sealed record ArchiveMergePreviewEntry(
    string SourceArchivePath,
    string OriginalPath,
    string TargetPath,
    bool IsDirectory,
    long Size,
    ArchiveMergePreviewEntryStatus Status,
    string Reason);

internal sealed record ArchiveMergePreview(
    string OutputPath,
    IReadOnlyList<ArchiveMergePreviewSource> Sources,
    IReadOnlyList<ArchiveMergePreviewEntry> Entries)
{
    public int ReadyCount => Entries.Count(static entry => entry.Status == ArchiveMergePreviewEntryStatus.Ready);

    public int CollisionRenamedCount => Entries.Count(static entry => entry.Status == ArchiveMergePreviewEntryStatus.CollisionRenamed);

    public int SkippedCount => Entries.Count(static entry => entry.Status is ArchiveMergePreviewEntryStatus.Skipped or ArchiveMergePreviewEntryStatus.DuplicateSkipped);

    public int BlockedCount =>
        Sources.Count(static source => source.Status == ArchiveMergePreviewSourceStatus.Blocked) +
        Entries.Count(static entry => entry.Status == ArchiveMergePreviewEntryStatus.Blocked);
}

internal sealed record ArchiveEntryExtraFields(
    byte[] LocalHeader,
    byte[] CentralDirectory);

internal sealed record ZipRawEntryExtraFields(
    string Name,
    ArchiveEntryExtraFields ExtraFields);

internal sealed record ArchiveEntryMetadata(
    DateTime? LastModified,
    DateTime? Created,
    DateTime? LastAccessed,
    DateTime? Archived,
    int ExternalAttributes,
    ArchiveEntryExtraFields? ExtraFields,
    string? Comment);

internal sealed record ArchiveEntryInfo(
    string SourceArchivePath,
    int EntryIndex,
    string OriginalPath,
    bool IsDirectory,
    long Size,
    ArchiveEntryMetadata Metadata);

/// <summary>
/// 아카이브 병합 실행의 전 과정을 담당하는 메인 엔진.
/// </summary>
internal static class ArchiveMergeOperations
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly StringComparer InternalPathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// 병합 실행에 사용할 기본 옵션을 경로/설정 기반으로 구성한다.
    /// </summary>
    /// <param name="paths">병합 대상 경로 목록</param>
    /// <param name="settings">사용자 설정</param>
    /// <param name="layoutOverride">레아이웃 강제 지정(선택)</param>
    /// <returns>2개 미만 입력이면 null</returns>
    public static ArchiveMergeOptions? CreateDefaultOptions(
        IEnumerable<string> paths,
        FileToolsSettings settings,
        ArchiveMergeLayout? layoutOverride = null)
    {
        var sourcePaths = NormalizeArchivePaths(paths);
        if (sourcePaths.Length < 2)
        {
            return null;
        }

        var layout = layoutOverride ?? settings.ArchiveMergeLayout;
        var outputPath = ResolveDefaultOutputPath(sourcePaths, settings);
        return new ArchiveMergeOptions
        {
            SourcePaths = sourcePaths.ToList(),
            OutputPath = outputPath,
            Layout = layout,
            CollisionPolicy = settings.ArchiveMergeCollisionPolicy,
            DuplicatePolicy = settings.ArchiveMergeDuplicatePolicy,
            FailurePolicy = settings.ArchiveMergeFailurePolicy,
            CompressionLevel = settings.ArchiveMergeCompressionLevel,
            DeleteOriginals = settings.ArchiveMergeDeleteOriginals
        };
    }

    /// <summary>
    /// 파일시스템 기본 구현을 사용해 병합을 실행한다.
    /// </summary>
    /// <param name="options">실행 옵션</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <param name="progress">진행 로그 콜백</param>
    /// <param name="questionSink">충돌/인코딩 인터랙션 싱크</param>
    /// <returns>실행 결과 집계</returns>
    public static OperationResult Merge(
        ArchiveMergeOptions options,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null,
        IArchiveMergeQuestionSink? questionSink = null)
    {
        return Merge(
            options,
            cancellationToken,
            PhysicalArchiveMergeFileSystem.Instance,
            progress,
            questionSink);
    }

    /// <summary>
    /// 병합 본 실행.
    /// 소스 열기 → 유효성/해시/충돌 처리 → 임시 저장소에 쓰기 → 최종 병합본 이동까지 수행한다.
    /// </summary>
    /// <param name="options">병합 옵션</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <param name="fileSystem">파일시스템 추상화</param>
    /// <param name="progress">진행 로그</param>
    /// <param name="questionSink">대화형 결정 콜백</param>
    /// <returns>실행 결과</returns>
    internal static OperationResult Merge(
        ArchiveMergeOptions options,
        CancellationToken cancellationToken,
        IArchiveMergeFileSystem fileSystem,
        IProgress<string>? progress = null,
        IArchiveMergeQuestionSink? questionSink = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        var result = new OperationResult();
        var sourcePaths = NormalizeArchivePaths(options.SourcePaths);
        foreach (var sourcePath in sourcePaths)
        {
            result.AddCandidate();
        }

        if (sourcePaths.Length < 2)
        {
            result.AddSkipped(Localizer.Get("ArchiveMergeNeedsMultipleArchives"));
            return result;
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            result.AddError(Localizer.Get("ArchiveMergeOutputPathRequired"));
            return result;
        }

        var outputPath = Path.GetFullPath(options.OutputPath);
        if (sourcePaths.Any(path => PathComparer.Equals(path, outputPath)))
        {
            result.AddError(Localizer.Get("ArchiveMergeOutputCannotBeSource"));
            return result;
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            result.AddError(Localizer.Get("PlanPreviewNoParent"));
            return result;
        }

        string? tempPath = null;
        var states = new List<SourceArchiveState>();

        try
        {
            fileSystem.CreateDirectory(outputDirectory);
            outputPath = ResolveOutputCollision(outputPath, sourcePaths, fileSystem);
            tempPath = fileSystem.CreateTempArchivePath(outputDirectory);

            progress?.Report(Localizer.Get("ArchiveMergeProgressValidateSources"));
            foreach (var sourcePath in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = OpenSourceArchive(sourcePath, questionSink, result);
                if (state is null)
                {
                    if (options.FailurePolicy == ArchiveMergeFailurePolicy.AbortAll)
                    {
                        return result;
                    }

                    continue;
                }

                states.Add(state);
            }

            if (states.Count == 0)
            {
                result.AddError(Localizer.Get("ArchiveMergeNoReadableArchives"));
                return result;
            }

            progress?.Report(Localizer.Get("ArchiveMergeProgressScanEntries"));
            var plans = CreateEntryPlans(states, options, result);
            if (result.HasErrors && options.FailurePolicy == ArchiveMergeFailurePolicy.AbortAll)
            {
                return result;
            }

            progress?.Report(Localizer.Get("ArchiveMergeProgressValidateEntries"));
            if (!ValidateEntryStreams(states, plans, options, result, progress, cancellationToken))
            {
                return result;
            }

            progress?.Report(Localizer.Get("ArchiveMergeProgressResolveCollisions"));
            if (!ResolveDuplicatesAndCollisions(plans, options, result, questionSink, cancellationToken))
            {
                return result;
            }

            var writePlans = plans
                .Where(static plan => !plan.IsSkipped)
                .ToArray();
            if (writePlans.Length == 0)
            {
                result.AddError(Localizer.Get("ArchiveMergeNoEntriesToWrite"));
                return result;
            }

            progress?.Report(Localizer.Get("ArchiveMergeProgressWriteTemp"));
            WriteTempArchive(states, writePlans, tempPath, options, cancellationToken);

            progress?.Report(Localizer.Get("ArchiveMergeProgressVerifyOutput"));
            VerifyOutputArchive(tempPath, writePlans.Length);

            progress?.Report(Localizer.Get("ArchiveMergeProgressMoveFinal"));
            fileSystem.MoveFile(tempPath, outputPath);

            var writtenCount = writePlans.Count(static plan => !plan.Entry.IsDirectory);
            var directoryCount = writePlans.Count(static plan => plan.Entry.IsDirectory);
            result.AddApplied(Localizer.Format(
                "ArchiveMergeCreatedFormat",
                outputPath,
                writtenCount,
                directoryCount));
            FileToolsEnvironment.Log("ARCHIVE-MERGE", string.Join(" | ", sourcePaths) + " -> " + outputPath);

            if (options.DeleteOriginals)
            {
                progress?.Report(Localizer.Get("ArchiveMergeProgressDeleteOriginals"));
                DeleteEligibleOriginals(states, plans, result);
            }
        }
        catch (OperationCanceledException)
        {
            result.AddSkipped(Localizer.Get("ArchiveMergeCanceled"));
        }
        catch (Exception ex)
        {
            result.AddError(Localizer.Format("ArchiveMergeWriteFailedFormat", ex.Message));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempPath))
            {
                TryDeleteFile(tempPath, fileSystem);
            }
        }

        return result;
    }

    /// <summary>
    /// 입력이 실제 파일이면서 .zip 인지 확인한다.
    /// </summary>
    /// <param name="path">검사할 경로</param>
    /// <returns>zip 파일이면 true</returns>
    public static bool IsSupportedArchivePath(string path)
    {
        return File.Exists(path) &&
               string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 병합 계획 출력에서 사용될 옵션 문자열을 조합한다.
    /// </summary>
    public static string DescribeOptions(ArchiveMergeOptions options)
    {
        return Localizer.Format(
            "ArchiveMergePlanOptionsFormat",
            ArchiveMergeText.GetDisplayName(options.Layout),
            ArchiveMergeText.GetDisplayName(options.FailurePolicy),
            ArchiveMergeText.GetDisplayName(options.DuplicatePolicy));
    }

    /// <summary>
    /// 병합을 실행하지 않고 소스/타깃 매핑과 충돌/중복 상황을 미리 계산한다.
    /// </summary>
    /// <param name="options">병합 옵션</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>미리보기 결과</returns>
    public static ArchiveMergePreview CreatePreview(
        ArchiveMergeOptions options,
        CancellationToken cancellationToken = default)
    {
        var sources = new List<ArchiveMergePreviewSource>();
        var states = new List<SourceArchiveState>();
        var result = new OperationResult();
        var sourcePaths = options.SourcePaths
            .Select(static path => path.Trim().Trim('"'))
            .Where(static path => path.Length > 0)
            .Select(path =>
            {
                try
                {
                    return Path.GetFullPath(path);
                }
                catch
                {
                    return path;
                }
            })
            .Distinct(PathComparer)
            .ToArray();

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(sourcePath))
            {
                sources.Add(new ArchiveMergePreviewSource(
                    sourcePath,
                    ArchiveMergePreviewSourceStatus.Blocked,
                    Localizer.Get("ArchiveMergePreviewSourceMissing"),
                    EntryCount: 0));
                continue;
            }

            if (!IsSupportedArchivePath(sourcePath))
            {
                sources.Add(new ArchiveMergePreviewSource(
                    sourcePath,
                    ArchiveMergePreviewSourceStatus.Blocked,
                    Localizer.Format("ArchiveMergeUnsupportedArchiveFormat", Path.GetFileName(sourcePath)),
                    EntryCount: 0));
                continue;
            }

            var state = OpenSourceArchive(sourcePath, questionSink: null, result);
            if (state is null)
            {
                var reason = result.Errors.LastOrDefault() ?? Localizer.Get("ArchiveMergePreviewSourceUnreadable");
                sources.Add(new ArchiveMergePreviewSource(
                    sourcePath,
                    ArchiveMergePreviewSourceStatus.Blocked,
                    reason,
                    EntryCount: 0));
                continue;
            }

            states.Add(state);
            sources.Add(new ArchiveMergePreviewSource(
                sourcePath,
                ArchiveMergePreviewSourceStatus.Ready,
                "",
                state.Entries.Count));
        }

        if (states.Count == 0)
        {
            return new ArchiveMergePreview(options.OutputPath, sources, []);
        }

        result = new OperationResult();
        var plans = CreateEntryPlans(states, options, result);
        var originalTargets = plans.ToDictionary(static plan => plan, static plan => plan.TargetPath);
        if (NeedsEntryHashes(options))
        {
            ValidateEntryStreams(states, plans, options, result, progress: null, cancellationToken);
        }

        ResolvePreviewDuplicatesAndCollisions(plans, options, result, cancellationToken);

        var entries = plans
            .Select(plan => CreatePreviewEntry(plan, originalTargets[plan]))
            .ToArray();
        return new ArchiveMergePreview(options.OutputPath, sources, entries);
    }

    /// <summary>
    /// 개별 아카이브를 열고 항목 메타데이터를 수집한다.
    /// </summary>
    /// <param name="sourcePath">소스 아카이브 경로</param>
    /// <param name="questionSink">인코딩 선택 싱크</param>
    /// <param name="result">오류 누적 대상</param>
    /// <returns>열린 상태 객체, 실패 시 null</returns>
    private static SourceArchiveState? OpenSourceArchive(
        string sourcePath,
        IArchiveMergeQuestionSink? questionSink,
        OperationResult result)
    {
        try
        {
            var encodingResult = ArchiveEncodingDetector.Resolve(sourcePath, questionSink);
            if (!IsSupportedArchivePath(sourcePath))
            {
                result.AddError(Localizer.Format("ArchiveMergeUnsupportedArchiveFormat", Path.GetFileName(sourcePath)));
                return null;
            }

            using var reader = SharpCompressArchiveReader.Open(sourcePath, encodingResult.Encoding);
            return new SourceArchiveState(
                sourcePath,
                encodingResult.Encoding,
                encodingResult.DisplayName,
                reader.Entries.ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.AddError(Localizer.Format("ArchiveMergeOpenFailedFormat", Path.GetFileName(sourcePath), ex.Message));
            return null;
        }
    }

    /// <summary>
    /// 병합할 엔트리 목록을 레이아웃 규칙에 맞춰 생성한다.
    /// </summary>
    /// <param name="states">소스 아카이브 상태</param>
    /// <param name="options">병합 옵션</param>
    /// <param name="result">결과 집계</param>
    /// <returns>생성된 병합 계획</returns>
    private static List<EntryMergePlan> CreateEntryPlans(
        IReadOnlyList<SourceArchiveState> states,
        ArchiveMergeOptions options,
        OperationResult result)
    {
        var plans = new List<EntryMergePlan>();
        var rootNames = new HashSet<string>(InternalPathComparer);

        foreach (var state in states)
        {
            var rootName = "";
            if (options.Layout == ArchiveMergeLayout.GroupByArchiveName)
            {
                rootName = CreateUniqueInternalName(
                    WindowsFileNameSafety.MakeSafeFileName(Path.GetFileNameWithoutExtension(state.SourcePath)),
                    isDirectory: true,
                    rootNames);
                plans.Add(EntryMergePlan.CreateSyntheticDirectory(state, EnsureDirectoryPath(rootName)));
            }

            foreach (var entry in state.Entries)
            {
                var normalizedPath = ArchiveInternalPath.Normalize(entry.OriginalPath, entry.IsDirectory);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    result.AddSkipped(Localizer.Format("ArchiveMergeInvalidEntryPathFormat", entry.OriginalPath));
                    continue;
                }

                var targetPath = options.Layout == ArchiveMergeLayout.GroupByArchiveName
                    ? CombineEntryPath(rootName, normalizedPath)
                    : normalizedPath;
                plans.Add(new EntryMergePlan(state.SourcePath, entry, targetPath));
            }
        }

        return plans;
    }

    /// <summary>
    /// 각 엔트리 스트림을 읽어 해시/검증을 수행해 병합 불가 항목을 표시한다.
    /// </summary>
    /// <param name="states">소스 상태</param>
    /// <param name="plans">사전 생성된 계획</param>
    /// <param name="options">병합 옵션</param>
    /// <param name="result">오류/스킵 집계</param>
    /// <param name="progress">진행 로그</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>전체 유효성 통과 여부</returns>
    private static bool ValidateEntryStreams(
        IReadOnlyList<SourceArchiveState> states,
        IReadOnlyList<EntryMergePlan> plans,
        ArchiveMergeOptions options,
        OperationResult result,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var needsHash = NeedsEntryHashes(options);

        foreach (var state in states)
        {
            if (state.IsSkipped)
            {
                continue;
            }

            progress?.Report(Localizer.Format("ArchiveMergeProgressReadArchiveFormat", Path.GetFileName(state.SourcePath)));
            try
            {
                using var reader = SharpCompressArchiveReader.Open(state.SourcePath, state.Encoding);
                foreach (var plan in plans.Where(plan => PathComparer.Equals(plan.SourceArchivePath, state.SourcePath)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (plan.IsSkipped || plan.Entry.IsDirectory)
                    {
                        continue;
                    }

                    try
                    {
                        using var stream = reader.OpenEntryStream(plan.Entry);
                        plan.Hash = needsHash
                            ? ReadAndHash(stream, cancellationToken)
                            : ReadAndDiscard(stream, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        state.HadEntryFailure = true;
                        var message = Localizer.Format(
                            "ArchiveMergeEntryReadFailedFormat",
                            Path.GetFileName(state.SourcePath),
                            plan.Entry.OriginalPath,
                            ex.Message);

                        if (options.FailurePolicy == ArchiveMergeFailurePolicy.AbortAll)
                        {
                            result.AddError(message);
                            return false;
                        }

                        if (options.FailurePolicy == ArchiveMergeFailurePolicy.SkipFailedArchive)
                        {
                            state.IsSkipped = true;
                            foreach (var sourcePlan in plans.Where(item => PathComparer.Equals(item.SourceArchivePath, state.SourcePath)))
                            {
                                sourcePlan.Skip(Localizer.Format("ArchiveMergeSkippedArchiveFormat", Path.GetFileName(state.SourcePath)));
                            }

                            result.AddSkipped(message);
                            break;
                        }

                        plan.Skip(message);
                        result.AddSkipped(message);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var message = Localizer.Format("ArchiveMergeOpenFailedFormat", Path.GetFileName(state.SourcePath), ex.Message);
                if (options.FailurePolicy == ArchiveMergeFailurePolicy.AbortAll)
                {
                    result.AddError(message);
                    return false;
                }

                state.IsSkipped = true;
                foreach (var sourcePlan in plans.Where(item => PathComparer.Equals(item.SourceArchivePath, state.SourcePath)))
                {
                    sourcePlan.Skip(message);
                }

                result.AddSkipped(message);
            }
        }

        return true;
    }

    /// <summary>
    /// 중복/충돌 정책상 해시가 필요한지 판정한다.
    /// </summary>
    /// <param name="options">병합 옵션</param>
    /// <returns>해시 필요 여부</returns>
    private static bool NeedsEntryHashes(ArchiveMergeOptions options)
    {
        return options.DuplicatePolicy == ArchiveMergeDuplicatePolicy.Ask ||
               options.DuplicatePolicy == ArchiveMergeDuplicatePolicy.SameContentKeepFirst ||
               options.CollisionPolicy == ArchiveMergeCollisionPolicy.SameContentKeepFirst;
    }

    /// <summary>
    /// 실제 병합 대상 경로를 확정하기 위해 중복/충돌을 해석한다.
    /// </summary>
    /// <param name="plans">미리 구성된 엔트리 플랜</param>
    /// <param name="options">정책</param>
    /// <param name="result">집계 결과</param>
    /// <param name="questionSink">Ask 정책 시 사용자 결정 채널</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>실행 가능 여부</returns>
    private static bool ResolveDuplicatesAndCollisions(
        IReadOnlyList<EntryMergePlan> plans,
        ArchiveMergeOptions options,
        OperationResult result,
        IArchiveMergeQuestionSink? questionSink,
        CancellationToken cancellationToken)
    {
        if (options.DuplicatePolicy == ArchiveMergeDuplicatePolicy.Ask ||
            options.CollisionPolicy == ArchiveMergeCollisionPolicy.Ask)
        {
            if (questionSink is null)
            {
                result.AddError(Localizer.Get("ArchiveMergeAskPolicyRequiresUi"));
                return false;
            }
        }

        var hashOwners = new Dictionary<string, EntryMergePlan>(StringComparer.OrdinalIgnoreCase);
        var usedPaths = new Dictionary<string, EntryMergePlan>(InternalPathComparer);
        var usedDirectoryPaths = new HashSet<string>(InternalPathComparer);

        foreach (var plan in plans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (plan.IsSkipped)
            {
                continue;
            }

            if (!plan.Entry.IsDirectory && !string.IsNullOrWhiteSpace(plan.Hash))
            {
                if (!hashOwners.TryGetValue(plan.Hash, out var hashOwner))
                {
                    hashOwners[plan.Hash] = plan;
                }
                else if (options.DuplicatePolicy == ArchiveMergeDuplicatePolicy.SameContentKeepFirst)
                {
                    plan.SkipDuplicate(Localizer.Format("ArchiveMergeDuplicateContentSkippedFormat", plan.Entry.OriginalPath));
                    result.AddSkipped(plan.SkipReason!);
                    continue;
                }
                else if (options.DuplicatePolicy == ArchiveMergeDuplicatePolicy.Ask)
                {
                    var decision = questionSink!.ResolveDuplicateContent(new ArchiveMergeDuplicateContentQuestion(
                        plan.Hash,
                        CreateQuestionEntry(hashOwner),
                        CreateQuestionEntry(plan)));
                    if (decision == ArchiveMergeDuplicateContentDecision.Abort)
                    {
                        result.AddError(Localizer.Format("ArchiveMergeDuplicateDecisionAbortedFormat", plan.Entry.OriginalPath));
                        return false;
                    }

                    if (decision == ArchiveMergeDuplicateContentDecision.SkipCurrent)
                    {
                        plan.Skip(Localizer.Format("ArchiveMergeDuplicateContentSkippedFormat", plan.Entry.OriginalPath));
                        result.AddSkipped(plan.SkipReason!);
                        continue;
                    }
                }
            }

            if (plan.Entry.IsDirectory)
            {
                var directoryPath = EnsureDirectoryPath(plan.TargetPath);
                var canonical = TrimDirectoryPath(directoryPath);
                if (usedPaths.TryGetValue(canonical, out var existingPathPlan))
                {
                    if (!ResolveInternalPathCollision(plan, existingPathPlan, usedPaths, options, result, questionSink))
                    {
                        return false;
                    }

                    if (plan.IsSkipped)
                    {
                        continue;
                    }

                    directoryPath = EnsureDirectoryPath(plan.TargetPath);
                    canonical = TrimDirectoryPath(directoryPath);
                }

                if (!usedDirectoryPaths.Add(canonical))
                {
                    plan.Skip(Localizer.Format("ArchiveMergeDuplicateDirectorySkippedFormat", plan.TargetPath));
                    continue;
                }

                plan.TargetPath = directoryPath;
                usedPaths[canonical] = plan;
                continue;
            }

            var fileCanonical = TrimDirectoryPath(plan.TargetPath);
            if (!usedPaths.TryGetValue(fileCanonical, out var existing))
            {
                plan.TargetPath = TrimDirectoryPath(plan.TargetPath);
                usedPaths[fileCanonical] = plan;
                continue;
            }

            if (!ResolveInternalPathCollision(plan, existing, usedPaths, options, result, questionSink))
            {
                return false;
            }

            if (plan.IsSkipped)
            {
                continue;
            }

            usedPaths[TrimDirectoryPath(plan.TargetPath)] = plan;
        }

        return true;
    }

    /// <summary>
    /// 동일 경로 충돌을 정책에 따라 스킵/중단/번호부여로 해결한다.
    /// </summary>
    /// <param name="plan">현재 플랜</param>
    /// <param name="existing">이미 점유된 플랜</param>
    /// <param name="usedPaths">현재까지 사용된 경로</param>
    /// <param name="options">병합 정책</param>
    /// <param name="result">결과 집계</param>
    /// <param name="questionSink">Ask 정책 처리 채널</param>
    /// <returns>정상 처리/계속 여부</returns>
    private static bool ResolveInternalPathCollision(
        EntryMergePlan plan,
        EntryMergePlan existing,
        Dictionary<string, EntryMergePlan> usedPaths,
        ArchiveMergeOptions options,
        OperationResult result,
        IArchiveMergeQuestionSink? questionSink)
    {
        if (options.CollisionPolicy == ArchiveMergeCollisionPolicy.Abort)
        {
            result.AddError(Localizer.Format("ArchiveMergeInternalCollisionFormat", plan.TargetPath));
            return false;
        }

        if (options.CollisionPolicy == ArchiveMergeCollisionPolicy.SameContentKeepFirst &&
            !existing.Entry.IsDirectory &&
            !plan.Entry.IsDirectory &&
            !string.IsNullOrWhiteSpace(existing.Hash) &&
            string.Equals(existing.Hash, plan.Hash, StringComparison.OrdinalIgnoreCase))
        {
            plan.SkipDuplicate(Localizer.Format("ArchiveMergeDuplicateContentSkippedFormat", plan.Entry.OriginalPath));
            result.AddSkipped(plan.SkipReason!);
            return true;
        }

        if (options.CollisionPolicy == ArchiveMergeCollisionPolicy.Ask)
        {
            var decision = questionSink!.ResolveNameCollision(new ArchiveMergeNameCollisionQuestion(
                plan.TargetPath,
                CreateQuestionEntry(existing),
                CreateQuestionEntry(plan)));
            if (decision == ArchiveMergeNameCollisionDecision.Abort)
            {
                result.AddError(Localizer.Format("ArchiveMergeCollisionDecisionAbortedFormat", plan.TargetPath));
                return false;
            }

            if (decision == ArchiveMergeNameCollisionDecision.SkipCurrent)
            {
                plan.Skip(Localizer.Format("ArchiveMergeCollisionSkippedFormat", plan.Entry.OriginalPath));
                result.AddSkipped(plan.SkipReason!);
                return true;
            }
        }

        var resolved = CreateNumberedInternalPath(plan.TargetPath, usedPaths.Keys);
        plan.TargetPath = plan.Entry.IsDirectory ? EnsureDirectoryPath(resolved) : TrimDirectoryPath(resolved);
        return true;
    }

    /// <summary>
    /// 미리보기용 충돌/중복 분석을 수행해 경고/변경 상태를 설정한다.
    /// </summary>
    /// <param name="plans">병합 후보 플랜</param>
    /// <param name="options">옵션</param>
    /// <param name="result">결과 집계</param>
    /// <param name="cancellationToken">취소 토큰</param>
    private static void ResolvePreviewDuplicatesAndCollisions(
        IReadOnlyList<EntryMergePlan> plans,
        ArchiveMergeOptions options,
        OperationResult result,
        CancellationToken cancellationToken)
    {
        var hashOwners = new Dictionary<string, EntryMergePlan>(StringComparer.OrdinalIgnoreCase);
        var usedPaths = new Dictionary<string, EntryMergePlan>(InternalPathComparer);
        var usedDirectoryPaths = new HashSet<string>(InternalPathComparer);

        foreach (var plan in plans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (plan.IsSkipped)
            {
                continue;
            }

            if (!plan.Entry.IsDirectory && !string.IsNullOrWhiteSpace(plan.Hash))
            {
                if (!hashOwners.TryGetValue(plan.Hash, out var hashOwner))
                {
                    hashOwners[plan.Hash] = plan;
                }
                else if (options.DuplicatePolicy == ArchiveMergeDuplicatePolicy.SameContentKeepFirst)
                {
                    plan.SkipDuplicate(Localizer.Format("ArchiveMergeDuplicateContentSkippedFormat", plan.Entry.OriginalPath));
                    result.AddSkipped(plan.SkipReason!);
                    continue;
                }
                else if (options.DuplicatePolicy == ArchiveMergeDuplicatePolicy.Ask)
                {
                    plan.Block(Localizer.Format("ArchiveMergePreviewDuplicateDecisionRequiredFormat", hashOwner.Entry.OriginalPath));
                    continue;
                }
            }

            if (plan.Entry.IsDirectory)
            {
                var directoryPath = EnsureDirectoryPath(plan.TargetPath);
                var canonical = TrimDirectoryPath(directoryPath);
                if (usedPaths.TryGetValue(canonical, out var existingPathPlan))
                {
                    ResolvePreviewInternalPathCollision(plan, existingPathPlan, usedPaths, options, result);
                    if (plan.IsSkipped || plan.IsBlocked)
                    {
                        continue;
                    }

                    directoryPath = EnsureDirectoryPath(plan.TargetPath);
                    canonical = TrimDirectoryPath(directoryPath);
                }

                if (!usedDirectoryPaths.Add(canonical))
                {
                    plan.Skip(Localizer.Format("ArchiveMergeDuplicateDirectorySkippedFormat", plan.TargetPath));
                    continue;
                }

                plan.TargetPath = directoryPath;
                usedPaths[canonical] = plan;
                continue;
            }

            var fileCanonical = TrimDirectoryPath(plan.TargetPath);
            if (!usedPaths.TryGetValue(fileCanonical, out var existing))
            {
                plan.TargetPath = fileCanonical;
                usedPaths[fileCanonical] = plan;
                continue;
            }

            ResolvePreviewInternalPathCollision(plan, existing, usedPaths, options, result);
            if (plan.IsSkipped || plan.IsBlocked)
            {
                continue;
            }

            usedPaths[TrimDirectoryPath(plan.TargetPath)] = plan;
        }
    }

    /// <summary>
    /// 미리보기 단계에서 충돌 경로를 상태로만 표시한다.
    /// </summary>
    /// <param name="plan">현재 플랜</param>
    /// <param name="existing">기존 충돌 대상</param>
    /// <param name="usedPaths">이미 사용된 경로</param>
    /// <param name="options">병합 정책</param>
    /// <param name="result">결과 집계</param>
    private static void ResolvePreviewInternalPathCollision(
        EntryMergePlan plan,
        EntryMergePlan existing,
        Dictionary<string, EntryMergePlan> usedPaths,
        ArchiveMergeOptions options,
        OperationResult result)
    {
        if (options.CollisionPolicy == ArchiveMergeCollisionPolicy.Abort)
        {
            plan.Block(Localizer.Format("ArchiveMergeInternalCollisionFormat", plan.TargetPath));
            return;
        }

        if (options.CollisionPolicy == ArchiveMergeCollisionPolicy.SameContentKeepFirst &&
            !existing.Entry.IsDirectory &&
            !plan.Entry.IsDirectory &&
            !string.IsNullOrWhiteSpace(existing.Hash) &&
            string.Equals(existing.Hash, plan.Hash, StringComparison.OrdinalIgnoreCase))
        {
            plan.SkipDuplicate(Localizer.Format("ArchiveMergeDuplicateContentSkippedFormat", plan.Entry.OriginalPath));
            result.AddSkipped(plan.SkipReason!);
            return;
        }

        if (options.CollisionPolicy == ArchiveMergeCollisionPolicy.Ask)
        {
            plan.Block(Localizer.Format("ArchiveMergePreviewCollisionDecisionRequiredFormat", plan.TargetPath));
            return;
        }

        var resolved = CreateNumberedInternalPath(plan.TargetPath, usedPaths.Keys);
        plan.TargetPath = plan.Entry.IsDirectory ? EnsureDirectoryPath(resolved) : TrimDirectoryPath(resolved);
    }

    /// <summary>
    /// 플랜의 상태를 미리보기 엔트리 상태값으로 변환한다.
    /// </summary>
    /// <param name="plan">변환 대상 플랜</param>
    /// <param name="originalTargetPath">변경 전 경로</param>
    /// <returns>미리보기 항목</returns>
    private static ArchiveMergePreviewEntry CreatePreviewEntry(EntryMergePlan plan, string originalTargetPath)
    {
        if (plan.IsBlocked)
        {
            return new ArchiveMergePreviewEntry(
                plan.SourceArchivePath,
                plan.Entry.OriginalPath,
                plan.TargetPath,
                plan.Entry.IsDirectory,
                plan.Entry.Size,
                ArchiveMergePreviewEntryStatus.Blocked,
                plan.BlockReason!);
        }

        if (plan.IsSkipped)
        {
            var status = plan.IsDuplicateSkipped
                ? ArchiveMergePreviewEntryStatus.DuplicateSkipped
                : ArchiveMergePreviewEntryStatus.Skipped;
            return new ArchiveMergePreviewEntry(
                plan.SourceArchivePath,
                plan.Entry.OriginalPath,
                plan.TargetPath,
                plan.Entry.IsDirectory,
                plan.Entry.Size,
                status,
                plan.SkipReason ?? "");
        }

        if (!string.Equals(originalTargetPath, plan.TargetPath, StringComparison.Ordinal))
        {
            return new ArchiveMergePreviewEntry(
                plan.SourceArchivePath,
                plan.Entry.OriginalPath,
                plan.TargetPath,
                plan.Entry.IsDirectory,
                plan.Entry.Size,
                ArchiveMergePreviewEntryStatus.CollisionRenamed,
                Localizer.Get("ArchiveMergePreviewCollisionRenamed"));
        }

        return new ArchiveMergePreviewEntry(
            plan.SourceArchivePath,
            plan.Entry.OriginalPath,
            plan.TargetPath,
            plan.Entry.IsDirectory,
            plan.Entry.Size,
            ArchiveMergePreviewEntryStatus.Ready,
            "");
    }

    /// <summary>
    /// UI 질의용 질문 엔트리 모델을 생성한다.
    /// </summary>
    /// <param name="plan">질의 대상 플랜</param>
    /// <returns>질의 항목</returns>
    private static ArchiveMergeQuestionEntry CreateQuestionEntry(EntryMergePlan plan)
    {
        return new ArchiveMergeQuestionEntry(
            plan.SourceArchivePath,
            plan.Entry.OriginalPath,
            plan.TargetPath,
            plan.Entry.IsDirectory,
            plan.Entry.Size);
    }

    /// <summary>
    /// 병합 결과를 임시 아카이브에 실제 쓰기한다.
    /// </summary>
    /// <param name="states">원본 상태</param>
    /// <param name="plans">최종 적용 가능한 계획</param>
    /// <param name="tempPath">임시 저장 경로</param>
    /// <param name="options">병합 옵션</param>
    /// <param name="cancellationToken">취소 토큰</param>
    private static void WriteTempArchive(
        IReadOnlyList<SourceArchiveState> states,
        IReadOnlyList<EntryMergePlan> plans,
        string tempPath,
        ArchiveMergeOptions options,
        CancellationToken cancellationToken)
    {
        using var writer = SharpZipLibArchiveWriter.Create(tempPath, options.CompressionLevel);

        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.IsSkipped)
            {
                continue;
            }

            using var reader = SharpCompressArchiveReader.Open(state.SourcePath, state.Encoding);
            foreach (var plan in plans.Where(plan => PathComparer.Equals(plan.SourceArchivePath, state.SourcePath)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (plan.IsSkipped)
                {
                    continue;
                }

                if (plan.Entry.IsDirectory)
                {
                    writer.WriteDirectory(plan.TargetPath, plan.Entry.Metadata);
                    continue;
                }

                using var stream = reader.OpenEntryStream(plan.Entry);
                writer.WriteFile(plan.TargetPath, stream, plan.Entry.Metadata, cancellationToken);
            }
        }

        writer.Complete();
    }

    /// <summary>
    /// 임시 아카이브에 적재된 엔트리 수를 검증한다.
    /// </summary>
    private static void VerifyOutputArchive(string tempPath, int expectedEntries)
    {
        using var reader = SharpCompressArchiveReader.Open(tempPath, Encoding.UTF8);
        if (reader.Entries.Count != expectedEntries)
        {
            throw new InvalidOperationException(Localizer.Format(
                "ArchiveMergeVerificationCountMismatchFormat",
                expectedEntries,
                reader.Entries.Count));
        }
    }

    /// <summary>
    /// 삭제 옵션이 켜진 경우 병합 성공한 원본 아카이브를 삭제한다.
    /// </summary>
    /// <param name="states">소스 상태</param>
    /// <param name="plans">병합 계획</param>
    /// <param name="result">집계 결과</param>
    private static void DeleteEligibleOriginals(
        IReadOnlyList<SourceArchiveState> states,
        IReadOnlyList<EntryMergePlan> plans,
        OperationResult result)
    {
        foreach (var state in states)
        {
            var sourcePlans = plans.Where(plan => PathComparer.Equals(plan.SourceArchivePath, state.SourcePath)).ToArray();
            var hasSkippedFile = sourcePlans.Any(static plan => !plan.Entry.IsDirectory && plan.IsSkipped);
            if (state.IsSkipped || state.HadEntryFailure || hasSkippedFile)
            {
                result.AddSkipped(Localizer.Format("ArchiveMergeOriginalDeleteSkippedFormat", Path.GetFileName(state.SourcePath)));
                continue;
            }

            try
            {
                File.Delete(state.SourcePath);
                result.AddApplied(Localizer.Format("ArchiveMergeOriginalDeletedFormat", state.SourcePath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.AddError(Localizer.Format("ArchiveMergeOriginalDeleteFailedFormat", state.SourcePath, ex.Message));
            }
        }
    }

    /// <summary>
    /// 설정/입력 기반 기본 출력 경로를 계산한다.
    /// </summary>
    /// <param name="sourcePaths">입력 소스</param>
    /// <param name="settings">병합 설정</param>
    /// <returns>기본 출력 경로</returns>
    private static string ResolveDefaultOutputPath(IReadOnlyList<string> sourcePaths, FileToolsSettings settings)
    {
        var parent = Path.GetDirectoryName(sourcePaths[0]) ?? Environment.CurrentDirectory;
        var name = settings.ArchiveMergeOutputNamePolicy switch
        {
            ArchiveMergeOutputNamePolicy.ParentFolderName => Path.GetFileName(parent),
            ArchiveMergeOutputNamePolicy.Timestamp => "Merged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"),
            _ => CreateCommonArchiveStem(sourcePaths.Select(Path.GetFileNameWithoutExtension).ToArray())
        };

        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileName(parent);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Merged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        }

        var context = new NameTemplateContext
        {
            CommonStem = name,
            FirstFileStem = Path.GetFileNameWithoutExtension(sourcePaths[0]),
            SelectedCount = sourcePaths.Count,
            TargetExtension = ".zip"
        };
        var evaluation = NameTemplateResolver.CreateDefault(settings)
            .Evaluate(NameTemplateDefaults.ArchiveMergeFileNameTemplate, context);
        var fileName = WindowsFileNameSafety.MakeSafeFileName(evaluation.IsReady ? evaluation.Value : name + ".zip");
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".zip";
        }

        return ResolveOutputCollision(Path.Combine(parent, fileName), sourcePaths);
    }

    /// <summary>
    /// 기본 파일시스템에서 출력 경로 충돌을 회피한다.
    /// </summary>
    private static string ResolveOutputCollision(string outputPath, IReadOnlyList<string> sourcePaths)
    {
        return ResolveOutputCollision(outputPath, sourcePaths, PhysicalArchiveMergeFileSystem.Instance);
    }

    /// <summary>
    /// 경로 충돌이 존재할 때 "(2)" 형태로 가능한 출력 경로를 탐색한다.
    /// </summary>
    private static string ResolveOutputCollision(
        string outputPath,
        IReadOnlyList<string> sourcePaths,
        IArchiveMergeFileSystem fileSystem)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? "";
        var desiredName = Path.GetFileName(outputPath);
        var used = sourcePaths.Select(Path.GetFullPath).ToHashSet(PathComparer);
        if (!fileSystem.FileExists(outputPath) &&
            !fileSystem.DirectoryExists(outputPath) &&
            !used.Contains(Path.GetFullPath(outputPath)))
        {
            return outputPath;
        }

        var stem = Path.GetFileNameWithoutExtension(desiredName);
        var extension = Path.GetExtension(desiredName);
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({index}){extension}");
            if (!fileSystem.FileExists(candidate) &&
                !fileSystem.DirectoryExists(candidate) &&
                !used.Contains(Path.GetFullPath(candidate)))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(Localizer.Get("ArchiveMergeOutputCollisionUnresolved"));
    }

    /// <summary>
    /// 병합 소스 경로를 trim/존재확인/전체경로/중복제거한다.
    /// </summary>
    private static string[] NormalizeArchivePaths(IEnumerable<string> paths)
    {
        return paths
            .Select(static path => path.Trim().Trim('"'))
            .Where(static path => path.Length > 0 && File.Exists(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray();
    }

    /// <summary>
    /// 여러 소스 파일명에서 병합 출력 이름의 공통 stem을 계산한다.
    /// </summary>
    /// <remarks>
    /// 먼저 연속된 시퀀스 표기를 제거해 논리적 공통값을 먼저 시도하고,
    /// 실패하면 문자열 prefix 기반으로 폴백한다.
    /// </remarks>
    internal static string CreateCommonArchiveStem(IReadOnlyList<string?> stems)
    {
        var normalized = stems
            .Where(static stem => !string.IsNullOrWhiteSpace(stem))
            .Select(static stem => stem!)
            .ToArray();
        if (normalized.Length == 0)
        {
            return "";
        }

        var logicalStem = CreateCommonLogicalStem(normalized);
        if (!string.IsNullOrWhiteSpace(logicalStem))
        {
            return logicalStem;
        }

        return CreateCommonPrefixStem(normalized);
    }

    /// <summary>
    /// 동일한 베이스명(끝 시퀀스만 다름)을 가진 항목인지 판별해 공통 stem을 만든다.
    /// </summary>
    private static string CreateCommonLogicalStem(IReadOnlyList<string> stems)
    {
        if (stems.Count < 2)
        {
            return "";
        }

        var stripped = stems
            .Select(StripTerminalSequenceMarker)
            .ToArray();
        if (stripped.Any(static stem => string.IsNullOrWhiteSpace(stem)))
        {
            return "";
        }

        var first = stripped[0];
        return stripped.All(stem => string.Equals(first, stem, StringComparison.OrdinalIgnoreCase))
            ? first
            : "";
    }

    /// <summary>
    /// 파일명 뒤쪽에 붙는 시퀀스 표기(예: " - 001")를 제거한다.
    /// </summary>
    private static string StripTerminalSequenceMarker(string stem)
    {
        var value = stem.Trim();
        if (value.Length == 0)
        {
            return "";
        }

        var end = value.Length - 1;
        while (end >= 0 && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        var digitEnd = end;
        while (end >= 0 && char.IsDigit(value[end]))
        {
            end--;
        }

        if (digitEnd == end)
        {
            return "";
        }

        var prefix = value[..(end + 1)].TrimEnd(' ', '.', '-', '_', '[', '(', '{', '#');
        return string.IsNullOrWhiteSpace(prefix)
            ? ""
            : WindowsFileNameSafety.MakeSafeFileName(prefix);
    }

    /// <summary>
    /// 정규화된 이름 목록에서 대소문자 무시 prefix 공통부를 추출한다.
    /// </summary>
    private static string CreateCommonPrefixStem(IReadOnlyList<string> normalized)
    {
        var prefix = normalized[0];
        foreach (var stem in normalized.Skip(1))
        {
            var length = 0;
            var max = Math.Min(prefix.Length, stem.Length);
            while (length < max && char.ToUpperInvariant(prefix[length]) == char.ToUpperInvariant(stem[length]))
            {
                length++;
            }

            prefix = prefix[..length];
            if (prefix.Length == 0)
            {
                break;
            }
        }

        return prefix.Trim().TrimEnd(' ', '.', '-', '_', '[', '(', '{');
    }

    /// <summary>
    /// 스트림을 SHA-256으로 읽어 해시 문자열을 생성한다.
    /// </summary>
    /// <remarks>검증만 필요한 시점에서 본문을 모두 소비한 뒤 해시를 반환한다.</remarks>
    private static string ReadAndHash(Stream stream, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        using var crypto = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);
        CopyStream(stream, crypto, cancellationToken);
        crypto.FlushFinalBlock();
        return Convert.ToHexString(sha.Hash ?? []);
    }

    /// <summary>
    /// 스트림의 내용을 버퍼로 소진하고 문자열은 반환하지 않는다.
    /// </summary>
    /// <returns>항상 빈 문자열.</returns>
    private static string ReadAndDiscard(Stream stream, CancellationToken cancellationToken)
    {
        CopyStream(stream, Stream.Null, cancellationToken);
        return "";
    }

    /// <summary>
    /// 버퍼 단위로 소스 스트림을 목적지 스트림으로 전송한다.
    /// </summary>
    private static void CopyStream(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[128 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                return;
            }

            destination.Write(buffer, 0, read);
        }
    }

    /// <summary>
    /// 내부 엔트리명 충돌을 피하도록 사용되지 않은 파일/폴더명을 생성한다.
    /// </summary>
    private static string CreateUniqueInternalName(string desiredName, bool isDirectory, HashSet<string> usedNames)
    {
        var safeName = WindowsFileNameSafety.MakeSafeFileName(desiredName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = Localizer.Get("DefaultMergeFolderName");
        }

        var candidate = safeName;
        for (var index = 2; !usedNames.Add(candidate); index++)
        {
            candidate = isDirectory
                ? $"{safeName} ({index})"
                : Path.GetFileNameWithoutExtension(safeName) + $" ({index})" + Path.GetExtension(safeName);
        }

        return candidate;
    }

    /// <summary>
    /// 동일 경로 충돌이 발생했을 때 파일명에 번호를 붙여 대체 경로를 생성한다.
    /// </summary>
    private static string CreateNumberedInternalPath(string desiredPath, IEnumerable<string> usedPaths)
    {
        var used = usedPaths.ToHashSet(InternalPathComparer);
        var directory = GetEntryDirectory(desiredPath);
        var fileName = GetEntryFileName(desiredPath);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var index = 2; index < 10_000; index++)
        {
            var candidateName = $"{stem} ({index}){extension}";
            var candidate = string.IsNullOrWhiteSpace(directory)
                ? candidateName
                : directory + "/" + candidateName;
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(Localizer.Format("ArchiveMergeInternalCollisionFormat", desiredPath));
    }

    /// <summary>
    /// 상위 폴더명과 엔트리 경로를 합쳐 ZIP 내부 경로 문자열을 만든다.
    /// </summary>
    private static string CombineEntryPath(string rootName, string entryPath)
    {
        return string.IsNullOrWhiteSpace(rootName)
            ? entryPath
            : EnsureDirectoryPath(rootName) + entryPath.TrimStart('/');
    }

    /// <summary>
    /// 경로가 디렉터리 경로처럼 끝이 '/'로 유지되도록 보정한다.
    /// </summary>
    private static string EnsureDirectoryPath(string path)
    {
        return path.TrimEnd('/') + "/";
    }

    /// <summary>
    /// 경로 끝의 '/'를 제거해 파일/디렉터리 공통 처리에 사용할 정규형을 만든다.
    /// </summary>
    private static string TrimDirectoryPath(string path)
    {
        return path.TrimEnd('/');
    }

    /// <summary>
    /// 내부 엔트리 경로에서 폴더 부분만 추출한다.
    /// </summary>
    private static string GetEntryDirectory(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/');
        var index = normalized.LastIndexOf('/');
        return index < 0 ? "" : normalized[..index];
    }

    /// <summary>
    /// 내부 엔트리 경로에서 파일명만 추출한다.
    /// </summary>
    private static string GetEntryFileName(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/').TrimEnd('/');
        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    /// <summary>
    /// 파일 삭제를 시도하되, 실패 시 무시하는 보수적 정리 유틸이다.
    /// </summary>
    private static void TryDeleteFile(string path, IArchiveMergeFileSystem fileSystem)
    {
        try
        {
            fileSystem.DeleteFileIfExists(path);
        }
        catch
        {
        }
    }

    /// <summary>
    /// 병합 대상 아카이브 1개에 대한 상태와 진입 항목 목록을 보관한다.
    /// </summary>
    private sealed class SourceArchiveState
    {
        public SourceArchiveState(string sourcePath, Encoding encoding, string encodingDisplayName, List<ArchiveEntryInfo> entries)
        {
            SourcePath = sourcePath;
            Encoding = encoding;
            EncodingDisplayName = encodingDisplayName;
            Entries = entries;
        }

        public string SourcePath { get; }

        public Encoding Encoding { get; }

        public string EncodingDisplayName { get; }

        public List<ArchiveEntryInfo> Entries { get; }

        public bool IsSkipped { get; set; }

        public bool HadEntryFailure { get; set; }
    }

    /// <summary>
    /// 각 엔트리에 대한 병합 결과 경로 및 상태를 표현하는 계획 객체.
    /// </summary>
    private sealed class EntryMergePlan
    {
        public EntryMergePlan(string sourceArchivePath, ArchiveEntryInfo entry, string targetPath)
        {
            SourceArchivePath = sourceArchivePath;
            Entry = entry;
            TargetPath = entry.IsDirectory ? EnsureDirectoryPath(targetPath) : TrimDirectoryPath(targetPath);
        }

        public string SourceArchivePath { get; }

        public ArchiveEntryInfo Entry { get; }

        public string TargetPath { get; set; }

        public string? Hash { get; set; }

        public string? SkipReason { get; private set; }

        public string? BlockReason { get; private set; }

        public bool IsSkipped => !string.IsNullOrWhiteSpace(SkipReason);

        public bool IsBlocked => !string.IsNullOrWhiteSpace(BlockReason);

        public bool IsDuplicateSkipped { get; private set; }

        public void Skip(string reason)
        {
            SkipReason = reason;
        }

        public void SkipDuplicate(string reason)
        {
            IsDuplicateSkipped = true;
            Skip(reason);
        }

        public void Block(string reason)
        {
            BlockReason = reason;
        }

        public static EntryMergePlan CreateSyntheticDirectory(SourceArchiveState state, string targetPath)
        {
            var entry = new ArchiveEntryInfo(
                state.SourcePath,
                EntryIndex: -1,
                targetPath,
                IsDirectory: true,
                Size: 0,
                new ArchiveEntryMetadata(DateTime.Now, DateTime.Now, DateTime.Now, Archived: null, 0, ExtraFields: null, Comment: null));
            return new EntryMergePlan(state.SourcePath, entry, targetPath);
        }
    }
}

internal sealed class PhysicalArchiveMergeFileSystem : IArchiveMergeFileSystem
{
    public static PhysicalArchiveMergeFileSystem Instance { get; } = new();

    private PhysicalArchiveMergeFileSystem()
    {
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public string CreateTempArchivePath(string outputDirectory)
    {
        return Path.Combine(outputDirectory, ".FileTools.Merge.tmp-" + Guid.NewGuid().ToString("N") + ".zip");
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        File.Move(sourcePath, destinationPath);
    }

    public void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

internal static class ZipRawExtraFieldReader
{
    private const uint LocalFileHeaderSignature = 0x04034b50;
    private const uint CentralDirectorySignature = 0x02014b50;
    private const uint EndOfCentralDirectorySignature = 0x06054b50;
    private const ushort Utf8NameFlag = 0x0800;

    public static IReadOnlyList<ZipRawEntryExtraFields> Read(string path, Encoding defaultEncoding)
    {
        try
        {
            return Read(File.ReadAllBytes(path), defaultEncoding);
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<ZipRawEntryExtraFields> Read(byte[] bytes, Encoding defaultEncoding)
    {
        var eocdOffset = FindEndOfCentralDirectory(bytes);
        if (eocdOffset < 0)
        {
            return [];
        }

        var entryCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(eocdOffset + 10, 2));
        var centralDirectorySize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(eocdOffset + 12, 4));
        var centralDirectoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(eocdOffset + 16, 4));
        if (entryCount == ushort.MaxValue ||
            centralDirectorySize == uint.MaxValue ||
            centralDirectoryOffset == uint.MaxValue)
        {
            return [];
        }

        var centralOffset = checked((int)centralDirectoryOffset);
        var centralSize = checked((int)centralDirectorySize);
        if (centralOffset < 0 ||
            centralSize < 0 ||
            centralSize > bytes.Length ||
            centralOffset > bytes.Length - centralSize)
        {
            return [];
        }

        var entries = new List<ZipRawEntryExtraFields>(entryCount);
        var offset = centralOffset;
        for (var index = 0; index < entryCount; index++)
        {
            if (offset + 46 > bytes.Length ||
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) != CentralDirectorySignature)
            {
                return [];
            }

            var flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 8, 2));
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28, 2));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 30, 2));
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 32, 2));
            var localHeaderOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 42, 4));
            var nameStart = offset + 46;
            var extraStart = nameStart + nameLength;
            var commentStart = extraStart + extraLength;
            var nextOffset = commentStart + commentLength;
            if (nextOffset > bytes.Length)
            {
                return [];
            }

            var name = DecodeEntryName(bytes, nameStart, nameLength, flags, defaultEncoding);
            var centralExtraData = CopyRange(bytes, extraStart, extraLength);
            var localExtraData = ReadLocalHeaderExtraData(bytes, localHeaderOffset);
            if (localExtraData is null)
            {
                return [];
            }

            entries.Add(new ZipRawEntryExtraFields(
                name,
                new ArchiveEntryExtraFields(localExtraData, centralExtraData)));
            offset = nextOffset;
        }

        return entries;
    }

    private static int FindEndOfCentralDirectory(byte[] bytes)
    {
        var minimumOffset = Math.Max(0, bytes.Length - 22 - ushort.MaxValue);
        for (var offset = bytes.Length - 22; offset >= minimumOffset; offset--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) != EndOfCentralDirectorySignature)
            {
                continue;
            }

            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 20, 2));
            if (offset + 22 + commentLength == bytes.Length)
            {
                return offset;
            }
        }

        return -1;
    }

    private static byte[]? ReadLocalHeaderExtraData(byte[] bytes, uint localHeaderOffset)
    {
        if (localHeaderOffset > int.MaxValue)
        {
            return null;
        }

        var offset = (int)localHeaderOffset;
        if (offset < 0 ||
            offset + 30 > bytes.Length ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) != LocalFileHeaderSignature)
        {
            return null;
        }

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 26, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28, 2));
        var extraStart = offset + 30 + nameLength;
        if (extraStart + extraLength > bytes.Length)
        {
            return null;
        }

        return CopyRange(bytes, extraStart, extraLength);
    }

    private static byte[] CopyRange(byte[] bytes, int start, int length)
    {
        if (length == 0)
        {
            return [];
        }

        var result = new byte[length];
        Buffer.BlockCopy(bytes, start, result, 0, length);
        return result;
    }

    private static string DecodeEntryName(byte[] bytes, int start, int length, ushort flags, Encoding defaultEncoding)
    {
        var encoding = (flags & Utf8NameFlag) != 0 ? Encoding.UTF8 : defaultEncoding;
        return encoding.GetString(bytes, start, length);
    }
}

internal sealed class SharpCompressArchiveReader : IArchiveReader
{
    private readonly IArchive _archive;
    private readonly IArchiveEntry[] _archiveEntries;
    private readonly ArchiveEntryInfo[] _entries;

    private SharpCompressArchiveReader(string path, Encoding encoding)
    {
        var options = new ReaderOptions
        {
            LeaveStreamOpen = false,
            ArchiveEncoding = new SharpCompress.Common.ArchiveEncoding
            {
                Default = encoding,
                Password = encoding
            },
            ExtensionHint = Path.GetExtension(path).TrimStart('.')
        };
        _archive = ArchiveFactory.OpenArchive(path, options);
        _archiveEntries = _archive.Entries.ToArray();
        var rawExtraFields = ZipRawExtraFieldReader.Read(path, encoding);
        var rawExtraFieldsByName = CreateRawExtraFieldQueues(rawExtraFields);
        _entries = _archiveEntries
            .Select((entry, index) => CreateEntryInfo(path, entry, index, rawExtraFields, rawExtraFieldsByName))
            .ToArray();
    }

    public IReadOnlyList<ArchiveEntryInfo> Entries => _entries;

    public static SharpCompressArchiveReader Open(string path, Encoding encoding)
    {
        return new SharpCompressArchiveReader(path, encoding);
    }

    public Stream OpenEntryStream(ArchiveEntryInfo entry)
    {
        if (entry.EntryIndex < 0 || entry.EntryIndex >= _archiveEntries.Length)
        {
            throw new InvalidOperationException("Archive entry is synthetic or out of range.");
        }

        return _archiveEntries[entry.EntryIndex].OpenEntryStream();
    }

    public void Dispose()
    {
        _archive.Dispose();
    }

    private static ArchiveEntryInfo CreateEntryInfo(
        string path,
        IArchiveEntry entry,
        int index,
        IReadOnlyList<ZipRawEntryExtraFields> rawExtraFields,
        Dictionary<string, Queue<ArchiveEntryExtraFields>> rawExtraFieldsByName)
    {
        var rawExtraField = TakeRawExtraFields(rawExtraFieldsByName, entry.Key ?? "") ??
                            (index >= 0 && index < rawExtraFields.Count ? rawExtraFields[index].ExtraFields : null);

        return new ArchiveEntryInfo(
            path,
            index,
            entry.Key ?? "",
            entry.IsDirectory,
            entry.Size,
            new ArchiveEntryMetadata(
                entry.LastModifiedTime,
                entry.CreatedTime,
                entry.LastAccessedTime,
                entry.ArchivedTime,
                TryReadExternalAttributes(entry),
                ExtraFields: rawExtraField,
                TryReadComment(entry)));
    }

    private static Dictionary<string, Queue<ArchiveEntryExtraFields>> CreateRawExtraFieldQueues(
        IReadOnlyList<ZipRawEntryExtraFields> entries)
    {
        var queues = new Dictionary<string, Queue<ArchiveEntryExtraFields>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!queues.TryGetValue(entry.Name, out var queue))
            {
                queue = new Queue<ArchiveEntryExtraFields>();
                queues.Add(entry.Name, queue);
            }

            queue.Enqueue(entry.ExtraFields);
        }

        return queues;
    }

    private static ArchiveEntryExtraFields? TakeRawExtraFields(
        Dictionary<string, Queue<ArchiveEntryExtraFields>> rawExtraFieldsByName,
        string entryName)
    {
        if (rawExtraFieldsByName.TryGetValue(entryName, out var queue) &&
            queue.Count > 0)
        {
            return queue.Dequeue();
        }

        var normalized = entryName.Replace('\\', '/');
        if (!string.Equals(normalized, entryName, StringComparison.Ordinal) &&
            rawExtraFieldsByName.TryGetValue(normalized, out queue) &&
            queue.Count > 0)
        {
            return queue.Dequeue();
        }

        return null;
    }

    private static int TryReadExternalAttributes(IArchiveEntry entry)
    {
        try
        {
            return entry.Attrib ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string? TryReadComment(IArchiveEntry entry)
    {
        try
        {
            if (entry is SharpCompress.Common.Zip.ZipEntry zipEntry &&
                !string.IsNullOrWhiteSpace(zipEntry.Comment))
            {
                return zipEntry.Comment;
            }
        }
        catch
        {
        }

        return null;
    }
}

/// <summary>
/// SharpZipLib 의존 없이 ZIP을 직접 조립해 쓰기 위한 경량 작성기 구현.
/// </summary>
internal sealed class SharpZipLibArchiveWriter : IArchiveWriter
{
    private const ushort Utf8NameFlag = 0x0800;
    private const ushort StoredMethod = 0;
    private const ushort DeflatedMethod = 8;
    private const ushort VersionNeeded = 20;
    private const uint LocalFileHeaderSignature = 0x04034b50;
    private const uint CentralDirectorySignature = 0x02014b50;
    private const uint EndOfCentralDirectorySignature = 0x06054b50;

    private readonly FileStream _stream;
    private readonly ArchiveMergeCompressionLevel _compressionLevel;
    private readonly List<WrittenZipEntry> _entries = [];

    /// <summary>
    /// ZIP 저장 스트림과 압축 레벨을 초기화한다.
    /// </summary>
    private SharpZipLibArchiveWriter(string path, ArchiveMergeCompressionLevel compressionLevel)
    {
        _stream = File.Create(path);
        _compressionLevel = compressionLevel;
    }

    /// <summary>
    /// 작성기 인스턴스를 생성한다.
    /// </summary>
    public static SharpZipLibArchiveWriter Create(string path, ArchiveMergeCompressionLevel compressionLevel)
    {
        return new SharpZipLibArchiveWriter(path, compressionLevel);
    }

    /// <summary>
    /// 디렉터리 엔트리를 ZIP에 기록한다.
    /// </summary>
    public void WriteDirectory(string entryPath, ArchiveEntryMetadata metadata)
    {
        var entry = CreateEntry(EnsureDirectoryPath(entryPath), metadata, _compressionLevel, isDirectory: true);
        entry.LocalHeaderOffset = _stream.Position;
        WriteLocalHeader(entry);
        _entries.Add(entry);
    }

    /// <summary>
    /// 파일 엔트리를 압축/저장 규칙에 맞춰 쓰고 CRC/크기를 업데이트한다.
    /// </summary>
    public void WriteFile(string entryPath, Stream source, ArchiveEntryMetadata metadata, CancellationToken cancellationToken)
    {
        var entry = CreateEntry(entryPath.Replace('\\', '/').TrimStart('/'), metadata, _compressionLevel, isDirectory: false);
        entry.LocalHeaderOffset = _stream.Position;
        WriteLocalHeader(entry);
        var payloadStart = _stream.Position;
        var crc = new Crc32();
        var buffer = new byte[128 * 1024];

        if (entry.CompressionMethod == StoredMethod)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = source.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                crc.Update(new ArraySegment<byte>(buffer, 0, read));
                entry.UncompressedSize = EnsureUInt32((long)entry.UncompressedSize + read, "uncompressed ZIP entry size");
                _stream.Write(buffer, 0, read);
            }
        }
        else
        {
            var deflater = new Deflater(ToSharpZipLevel(_compressionLevel), noZlibHeaderOrFooter: true);
            using var deflaterStream = new DeflaterOutputStream(_stream, deflater, buffer.Length)
            {
                IsStreamOwner = false
            };
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = source.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                crc.Update(new ArraySegment<byte>(buffer, 0, read));
                entry.UncompressedSize = EnsureUInt32((long)entry.UncompressedSize + read, "uncompressed ZIP entry size");
                deflaterStream.Write(buffer, 0, read);
            }

            deflaterStream.Finish();
        }

        entry.Crc = checked((uint)crc.Value);
        entry.CompressedSize = EnsureUInt32(_stream.Position - payloadStart, "compressed ZIP entry size");
        PatchLocalHeader(entry);
        _entries.Add(entry);
    }

    /// <summary>
    /// 엔트리 모두 기록 후 중앙 디렉터리와 EOCD를 기록해 ZIP 파일을 완성한다.
    /// </summary>
    public void Complete()
    {
        var centralDirectoryOffset = _stream.Position;
        foreach (var entry in _entries)
        {
            WriteCentralDirectoryHeader(entry);
        }

        var centralDirectorySize = _stream.Position - centralDirectoryOffset;
        WriteEndOfCentralDirectory(
            EnsureUInt16(_entries.Count, "ZIP entry count"),
            EnsureUInt32(centralDirectorySize, "ZIP central directory size"),
            EnsureUInt32(centralDirectoryOffset, "ZIP central directory offset"));
    }

    /// <summary>
    /// 파일 스트림을 정리한다.
    /// </summary>
    public void Dispose()
    {
        _stream.Dispose();
    }

    /// <summary>
    /// 메타데이터와 압축 설정을 반영한 ZIP 엔트리 구조를 만든다.
    /// </summary>
    private static WrittenZipEntry CreateEntry(
        string entryPath,
        ArchiveEntryMetadata metadata,
        ArchiveMergeCompressionLevel compressionLevel,
        bool isDirectory)
    {
        var fallbackExtraData = metadata.ExtraFields is null ? CreateFallbackExtraData(metadata) : null;
        var localExtraData = metadata.ExtraFields?.LocalHeader ?? fallbackExtraData ?? [];
        var centralExtraData = metadata.ExtraFields?.CentralDirectory ?? fallbackExtraData ?? [];
        return new WrittenZipEntry
        {
            NameBytes = Encoding.UTF8.GetBytes(entryPath),
            LastModified = ClampZipDate(metadata.LastModified ?? DateTime.Now),
            CompressionMethod = isDirectory || compressionLevel == ArchiveMergeCompressionLevel.StoreOnly
                ? StoredMethod
                : DeflatedMethod,
            ExternalAttributes = unchecked((uint)(metadata.ExternalAttributes != 0 || !isDirectory ? metadata.ExternalAttributes : 16)),
            LocalExtraData = localExtraData,
            CentralDirectoryExtraData = centralExtraData,
            CommentBytes = string.IsNullOrWhiteSpace(metadata.Comment) ? [] : Encoding.UTF8.GetBytes(metadata.Comment)
        };
    }

    /// <summary>
    /// 메타데이터에 extra field가 없을 때 시간 정보를 담은 대체 extra field를 생성한다.
    /// </summary>
    private static byte[]? CreateFallbackExtraData(ArchiveEntryMetadata metadata)
    {
        var times = new[]
        {
            metadata.LastModified,
            metadata.LastAccessed,
            metadata.Created
        };
        if (times.All(static time => time is null))
        {
            return null;
        }

        using var stream = new MemoryStream();
        var fallback = metadata.LastModified ?? metadata.Created ?? metadata.LastAccessed ?? DateTime.Now;
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x000A);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write((ushort)0x0001);
        writer.Write((ushort)24);
        writer.Write(ToFileTime(metadata.LastModified ?? fallback));
        writer.Write(ToFileTime(metadata.LastAccessed ?? fallback));
        writer.Write(ToFileTime(metadata.Created ?? fallback));
        return stream.ToArray();
    }

    /// <summary>
    /// ZIP Local File Header를 현재 위치에 기록한다.
    /// </summary>
    private void WriteLocalHeader(WrittenZipEntry entry)
    {
        EnsureUInt16(entry.NameBytes.Length, "ZIP entry name length");
        EnsureUInt16(entry.LocalExtraData.Length, "ZIP local extra field length");
        WriteUInt32(LocalFileHeaderSignature);
        WriteUInt16(VersionNeeded);
        WriteUInt16(Utf8NameFlag);
        WriteUInt16(entry.CompressionMethod);
        WriteDosDateTime(entry.LastModified);
        WriteUInt32(entry.Crc);
        WriteUInt32(entry.CompressedSize);
        WriteUInt32(entry.UncompressedSize);
        WriteUInt16((ushort)entry.NameBytes.Length);
        WriteUInt16((ushort)entry.LocalExtraData.Length);
        _stream.Write(entry.NameBytes, 0, entry.NameBytes.Length);
        _stream.Write(entry.LocalExtraData, 0, entry.LocalExtraData.Length);
    }

    /// <summary>
    /// 파일 바디 계산이 끝난 뒤 Local Header의 CRC/크기를 패치한다.
    /// </summary>
    private void PatchLocalHeader(WrittenZipEntry entry)
    {
        var currentPosition = _stream.Position;
        _stream.Position = entry.LocalHeaderOffset + 14;
        WriteUInt32(entry.Crc);
        WriteUInt32(entry.CompressedSize);
        WriteUInt32(entry.UncompressedSize);
        _stream.Position = currentPosition;
    }

    /// <summary>
    /// 중앙 디렉터리 헤더를 순회중인 엔트리에 대해 작성한다.
    /// </summary>
    private void WriteCentralDirectoryHeader(WrittenZipEntry entry)
    {
        EnsureUInt16(entry.NameBytes.Length, "ZIP entry name length");
        EnsureUInt16(entry.CentralDirectoryExtraData.Length, "ZIP central directory extra field length");
        EnsureUInt16(entry.CommentBytes.Length, "ZIP entry comment length");
        WriteUInt32(CentralDirectorySignature);
        WriteUInt16(VersionNeeded);
        WriteUInt16(VersionNeeded);
        WriteUInt16(Utf8NameFlag);
        WriteUInt16(entry.CompressionMethod);
        WriteDosDateTime(entry.LastModified);
        WriteUInt32(entry.Crc);
        WriteUInt32(entry.CompressedSize);
        WriteUInt32(entry.UncompressedSize);
        WriteUInt16((ushort)entry.NameBytes.Length);
        WriteUInt16((ushort)entry.CentralDirectoryExtraData.Length);
        WriteUInt16((ushort)entry.CommentBytes.Length);
        WriteUInt16(0);
        WriteUInt16(0);
        WriteUInt32(entry.ExternalAttributes);
        WriteUInt32(EnsureUInt32(entry.LocalHeaderOffset, "ZIP local header offset"));
        _stream.Write(entry.NameBytes, 0, entry.NameBytes.Length);
        _stream.Write(entry.CentralDirectoryExtraData, 0, entry.CentralDirectoryExtraData.Length);
        _stream.Write(entry.CommentBytes, 0, entry.CommentBytes.Length);
    }

    /// <summary>
    /// EOCD 레코드를 작성해 ZIP 마무리 메타데이터를 닫는다.
    /// </summary>
    private void WriteEndOfCentralDirectory(ushort entryCount, uint centralDirectorySize, uint centralDirectoryOffset)
    {
        WriteUInt32(EndOfCentralDirectorySignature);
        WriteUInt16(0);
        WriteUInt16(0);
        WriteUInt16(entryCount);
        WriteUInt16(entryCount);
        WriteUInt32(centralDirectorySize);
        WriteUInt32(centralDirectoryOffset);
        WriteUInt16(0);
    }

    /// <summary>
    /// DateTime을 ZIP DOS time/date 형식으로 변환해 기록한다.
    /// </summary>
    private void WriteDosDateTime(DateTime value)
    {
        var dosTime = (ushort)((value.Hour << 11) | (value.Minute << 5) | (value.Second / 2));
        var dosDate = (ushort)(((value.Year - 1980) << 9) | (value.Month << 5) | value.Day);
        WriteUInt16(dosTime);
        WriteUInt16(dosDate);
    }

    /// <summary>
    /// UTF-8 little-endian uint16 값을 스트림에 기록한다.
    /// </summary>
    private void WriteUInt16(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    /// <summary>
    /// UTF-8 little-endian uint32 값을 스트림에 기록한다.
    /// </summary>
    private void WriteUInt32(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    /// <summary>
    /// ZIP 포맷 범위를 벗어난 값은 예외, 유효하면 ushort로 반환한다.
    /// </summary>
    private static ushort EnsureUInt16(int value, string description)
    {
        if (value < 0 || value > ushort.MaxValue)
        {
            throw new InvalidOperationException(description + " exceeds the ZIP format limit.");
        }

        return (ushort)value;
    }

    /// <summary>
    /// ZIP32 포맷 범위를 벗어난 값은 예외, 유효하면 uint로 반환한다.
    /// </summary>
    private static uint EnsureUInt32(long value, string description)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new InvalidOperationException(description + " exceeds the ZIP32 format limit.");
        }

        return (uint)value;
    }

    /// <summary>
    /// FILETIME 기반 시간 정수로 변환한다.
    /// </summary>
    private static long ToFileTime(DateTime value)
    {
        var normalized = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Local)
            : value;
        return normalized.ToUniversalTime().ToFileTimeUtc();
    }

    /// <summary>
    /// ZIP에서 허용되지 않는 과거/미래 날짜를 ZIP 경계(1980~2107)로 보정한다.
    /// </summary>
    private static DateTime ClampZipDate(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        if (local < new DateTime(1980, 1, 1))
        {
            return new DateTime(1980, 1, 1);
        }

        if (local > new DateTime(2107, 12, 31, 23, 59, 58))
        {
            return new DateTime(2107, 12, 31, 23, 59, 58);
        }

        return local;
    }

    /// <summary>
    /// 프로젝트 압축 레벨을 SharpZipLib Deflater 레벨로 매핑한다.
    /// </summary>
    private static int ToSharpZipLevel(ArchiveMergeCompressionLevel level)
    {
        return level switch
        {
            ArchiveMergeCompressionLevel.StoreOnly => 0,
            ArchiveMergeCompressionLevel.Fast => 1,
            ArchiveMergeCompressionLevel.Maximum => 9,
            _ => 6
        };
    }

    /// <summary>
    /// 내부 경로를 ZIP 디렉터리 표기(끝 '/')로 정규화한다.
    /// </summary>
    private static string EnsureDirectoryPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/').TrimEnd('/') + "/";
    }

    private sealed class WrittenZipEntry
    {
        public byte[] NameBytes { get; init; } = [];

        public DateTime LastModified { get; init; }

        public ushort CompressionMethod { get; init; }

        public uint ExternalAttributes { get; init; }

        public byte[] LocalExtraData { get; init; } = [];

        public byte[] CentralDirectoryExtraData { get; init; } = [];

        public byte[] CommentBytes { get; init; } = [];

        public long LocalHeaderOffset { get; set; }

        public uint Crc { get; set; }

        public uint CompressedSize { get; set; }

        public uint UncompressedSize { get; set; }
    }
}

internal static class ArchiveInternalPath
{
    public static string Normalize(string path, bool isDirectory)
    {
        var normalized = path.Replace('\\', '/').Trim();
        while (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            normalized = normalized[2..].TrimStart('/');
        }

        var parts = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static part => part != ".")
            .ToArray();
        if (parts.Length == 0)
        {
            return "";
        }

        var safeParts = new List<string>();
        foreach (var part in parts)
        {
            if (part == "..")
            {
                return "";
            }

            var safe = WindowsFileNameSafety.MakeSafeFileName(part);
            if (string.IsNullOrWhiteSpace(safe))
            {
                return "";
            }

            safeParts.Add(safe);
        }

        var result = string.Join("/", safeParts);
        return isDirectory ? result.TrimEnd('/') + "/" : result.TrimEnd('/');
    }
}

internal static class ArchiveEncodingDetector
{
    private static readonly ArchiveEncodingCandidate[] Candidates =
    [
        new("ArchiveEncodingCandidateKoreanName", "ArchiveEncodingCandidateKoreanDescription", 949),
        new("ArchiveEncodingCandidateJapaneseName", "ArchiveEncodingCandidateJapaneseDescription", 932),
        new("ArchiveEncodingCandidateSimplifiedChineseName", "ArchiveEncodingCandidateSimplifiedChineseDescription", 936),
        new("ArchiveEncodingCandidateTraditionalChineseName", "ArchiveEncodingCandidateTraditionalChineseDescription", 950),
        new("ArchiveEncodingCandidateZipDefaultName", "ArchiveEncodingCandidateZipDefaultDescription", 437),
        new("ArchiveEncodingCandidateUtf8Name", "ArchiveEncodingCandidateUtf8Description", 65001),
        new("ArchiveEncodingCandidateSystemDefaultName", "ArchiveEncodingCandidateSystemDefaultDescription", 0)
    ];

    private static bool _providerRegistered;

    public static ArchiveEncodingResolution Resolve(string archivePath, IArchiveMergeQuestionSink? questionSink)
    {
        EnsureProviderRegistered();
        var results = new List<ArchiveEncodingCandidateResult>();
        foreach (var candidate in Candidates)
        {
            var encoding = candidate.CodePage == 0
                ? Encoding.Default
                : Encoding.GetEncoding(candidate.CodePage);
            try
            {
                using var reader = SharpCompressArchiveReader.Open(archivePath, encoding);
                var names = reader.Entries
                    .Select(static entry => entry.OriginalPath)
                    .Take(20)
                    .ToArray();
                results.Add(new ArchiveEncodingCandidateResult(
                    Localizer.Get(candidate.DisplayNameKey),
                    Localizer.Get(candidate.DescriptionKey),
                    encoding,
                    ScoreNames(names),
                    names,
                    candidate.CodePage == 0));
            }
            catch
            {
            }
        }

        if (results.Count == 0)
        {
            throw new InvalidDataException(Localizer.Get("ArchiveMergeNoEncodingCandidate"));
        }

        var ordered = results
            .OrderByDescending(static item => item.Score)
            .ToArray();
        if (AllCandidateScoresTie(ordered) &&
            ordered.FirstOrDefault(static item => item.IsSystemDefault) is { } systemDefault)
        {
            return new ArchiveEncodingResolution(
                systemDefault.Encoding,
                systemDefault.DisplayName,
                IsAmbiguous: false);
        }

        var ambiguous = ordered.Length > 1 && ordered[0].Score - ordered[1].Score <= 2;
        if (ambiguous && questionSink is not null)
        {
            var selected = questionSink.ChooseEncoding(new ArchiveEncodingQuestion(archivePath, ordered));
            if (selected is not null)
            {
                var selectedResult = ordered.FirstOrDefault(item => item.Encoding.CodePage == selected.CodePage);
                return new ArchiveEncodingResolution(
                    selected,
                    selectedResult?.DisplayName ?? selected.WebName,
                    IsAmbiguous: false);
            }
        }

        return new ArchiveEncodingResolution(ordered[0].Encoding, ordered[0].DisplayName, ambiguous);
    }

    private static bool AllCandidateScoresTie(IReadOnlyList<ArchiveEncodingCandidateResult> results)
    {
        return results.Count > 1 &&
               results.All(item => item.Score == results[0].Score);
    }

    private static int ScoreNames(IReadOnlyList<string> names)
    {
        var score = 0;
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                score -= 20;
                continue;
            }

            if (name.Contains('\uFFFD'))
            {
                score -= 50;
            }

            score -= name.Count(static ch => char.IsControl(ch) && ch != '\t') * 10;
            score += name.Count(static ch => ch is >= '가' and <= '힣');
            score += name.Count(static ch => ch is >= '\u3040' and <= '\u30ff');
            score += name.Count(static ch => ch is >= '\u4e00' and <= '\u9fff');
            if (Path.HasExtension(name))
            {
                score += 2;
            }

            if (name.Split('/', '\\').Any(static part => part is "." or ".."))
            {
                score -= 10;
            }
        }

        return score;
    }

    private static void EnsureProviderRegistered()
    {
        if (_providerRegistered)
        {
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _providerRegistered = true;
    }

    private sealed record ArchiveEncodingCandidate(string DisplayNameKey, string DescriptionKey, int CodePage);
}

internal sealed record ArchiveEncodingResolution(Encoding Encoding, string DisplayName, bool IsAmbiguous);

internal static class ArchiveMergeText
{
    public static string GetDisplayName(ArchiveMergeLayout layout)
    {
        return layout switch
        {
            ArchiveMergeLayout.GroupByArchiveName => Localizer.Get("ArchiveMergeLayoutGroupByArchiveName"),
            ArchiveMergeLayout.PreserveInternalPaths => Localizer.Get("ArchiveMergeLayoutPreserveInternalPaths"),
            _ => layout.ToString()
        };
    }

    public static string GetDisplayName(ArchiveMergeCollisionPolicy policy)
    {
        return policy switch
        {
            ArchiveMergeCollisionPolicy.AutoNumber => Localizer.Get("ArchiveMergeCollisionAutoNumber"),
            ArchiveMergeCollisionPolicy.SameContentKeepFirst => Localizer.Get("ArchiveMergeCollisionSameContentKeepFirst"),
            ArchiveMergeCollisionPolicy.Ask => Localizer.Get("ArchiveMergeCollisionAsk"),
            ArchiveMergeCollisionPolicy.Abort => Localizer.Get("ArchiveMergeCollisionAbort"),
            _ => policy.ToString()
        };
    }

    public static string GetDisplayName(ArchiveMergeDuplicatePolicy policy)
    {
        return policy switch
        {
            ArchiveMergeDuplicatePolicy.KeepBoth => Localizer.Get("ArchiveMergeDuplicateKeepBoth"),
            ArchiveMergeDuplicatePolicy.SameContentKeepFirst => Localizer.Get("ArchiveMergeDuplicateSameContentKeepFirst"),
            ArchiveMergeDuplicatePolicy.Ask => Localizer.Get("ArchiveMergeDuplicateAsk"),
            _ => policy.ToString()
        };
    }

    public static string GetDisplayName(ArchiveMergeFailurePolicy policy)
    {
        return policy switch
        {
            ArchiveMergeFailurePolicy.AbortAll => Localizer.Get("ArchiveMergeFailureAbortAll"),
            ArchiveMergeFailurePolicy.SkipFailedArchive => Localizer.Get("ArchiveMergeFailureSkipArchive"),
            ArchiveMergeFailurePolicy.SkipFailedEntry => Localizer.Get("ArchiveMergeFailureSkipEntry"),
            _ => policy.ToString()
        };
    }

    public static string GetDisplayName(ArchiveMergeOutputNamePolicy policy)
    {
        return policy switch
        {
            ArchiveMergeOutputNamePolicy.CommonStem => Localizer.Get("ArchiveMergeOutputCommonStem"),
            ArchiveMergeOutputNamePolicy.ParentFolderName => Localizer.Get("ArchiveMergeOutputParentFolder"),
            ArchiveMergeOutputNamePolicy.Timestamp => Localizer.Get("ArchiveMergeOutputTimestamp"),
            ArchiveMergeOutputNamePolicy.Manual => Localizer.Get("ArchiveMergeOutputManual"),
            _ => policy.ToString()
        };
    }

    public static string GetDisplayName(ArchiveMergeCompressionLevel level)
    {
        return level switch
        {
            ArchiveMergeCompressionLevel.StoreOnly => Localizer.Get("ArchiveMergeCompressionStoreOnly"),
            ArchiveMergeCompressionLevel.Fast => Localizer.Get("ArchiveMergeCompressionFast"),
            ArchiveMergeCompressionLevel.Maximum => Localizer.Get("ArchiveMergeCompressionMaximum"),
            _ => Localizer.Get("ArchiveMergeCompressionDefault")
        };
    }
}
