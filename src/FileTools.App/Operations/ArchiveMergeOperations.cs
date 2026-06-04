using System.Security.Cryptography;
using System.Text;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;
using ICSharpCode.SharpZipLib.Zip;

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

internal sealed class ArchiveMergeOptions
{
    public string PlanId { get; set; } = Guid.NewGuid().ToString("N");

    public List<string> SourcePaths { get; set; } = [];

    public string OutputPath { get; set; } = "";

    public ArchiveMergeLayout Layout { get; set; } = ArchiveMergeLayout.GroupByArchiveName;

    public ArchiveMergeCollisionPolicy CollisionPolicy { get; set; } = ArchiveMergeCollisionPolicy.AutoNumber;

    public ArchiveMergeDuplicatePolicy DuplicatePolicy { get; set; } = ArchiveMergeDuplicatePolicy.KeepBoth;

    public ArchiveMergeFailurePolicy FailurePolicy { get; set; } = ArchiveMergeFailurePolicy.AbortAll;

    public ArchiveMergeCompressionLevel CompressionLevel { get; set; } = ArchiveMergeCompressionLevel.Default;

    public bool DeleteOriginals { get; set; }

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

internal interface IArchiveReader : IDisposable
{
    IReadOnlyList<ArchiveEntryInfo> Entries { get; }

    Stream OpenEntryStream(ArchiveEntryInfo entry);
}

internal interface IArchiveWriter : IDisposable
{
    void WriteDirectory(string entryPath, ArchiveEntryMetadata metadata);

    void WriteFile(string entryPath, Stream source, ArchiveEntryMetadata metadata, CancellationToken cancellationToken);

    void Complete();
}

internal interface IArchiveMergeQuestionSink
{
    Encoding? ChooseEncoding(ArchiveEncodingQuestion question);

    ArchiveMergeNameCollisionDecision ResolveNameCollision(ArchiveMergeNameCollisionQuestion question);

    ArchiveMergeDuplicateContentDecision ResolveDuplicateContent(ArchiveMergeDuplicateContentQuestion question);
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
    IReadOnlyList<string> PreviewNames);

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

internal sealed record ArchiveEntryMetadata(
    DateTime? LastModified,
    DateTime? Created,
    DateTime? LastAccessed,
    DateTime? Archived,
    int ExternalAttributes,
    byte[]? ExtraData,
    string? Comment);

internal sealed record ArchiveEntryInfo(
    string SourceArchivePath,
    int EntryIndex,
    string OriginalPath,
    bool IsDirectory,
    long Size,
    ArchiveEntryMetadata Metadata);

internal static class ArchiveMergeOperations
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly StringComparer InternalPathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

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

    public static OperationResult Merge(
        ArchiveMergeOptions options,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null,
        IArchiveMergeQuestionSink? questionSink = null)
    {
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

        Directory.CreateDirectory(outputDirectory);
        outputPath = ResolveOutputCollision(outputPath, sourcePaths);
        var tempPath = Path.Combine(outputDirectory, ".FileTools.Merge.tmp-" + Guid.NewGuid().ToString("N") + ".zip");
        var states = new List<SourceArchiveState>();

        try
        {
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
            File.Move(tempPath, outputPath);

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
            TryDeleteFile(tempPath);
        }

        return result;
    }

    public static bool IsSupportedArchivePath(string path)
    {
        return File.Exists(path) &&
               string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
    }

    public static string DescribeOptions(ArchiveMergeOptions options)
    {
        return Localizer.Format(
            "ArchiveMergePlanOptionsFormat",
            ArchiveMergeText.GetDisplayName(options.Layout),
            ArchiveMergeText.GetDisplayName(options.FailurePolicy),
            ArchiveMergeText.GetDisplayName(options.DuplicatePolicy));
    }

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

    private static bool ValidateEntryStreams(
        IReadOnlyList<SourceArchiveState> states,
        IReadOnlyList<EntryMergePlan> plans,
        ArchiveMergeOptions options,
        OperationResult result,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var needsHash =
            options.DuplicatePolicy == ArchiveMergeDuplicatePolicy.Ask ||
            options.DuplicatePolicy == ArchiveMergeDuplicatePolicy.SameContentKeepFirst ||
            options.CollisionPolicy == ArchiveMergeCollisionPolicy.SameContentKeepFirst;

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
                    plan.Skip(Localizer.Format("ArchiveMergeDuplicateContentSkippedFormat", plan.Entry.OriginalPath));
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
            plan.Skip(Localizer.Format("ArchiveMergeDuplicateContentSkippedFormat", plan.Entry.OriginalPath));
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

    private static ArchiveMergeQuestionEntry CreateQuestionEntry(EntryMergePlan plan)
    {
        return new ArchiveMergeQuestionEntry(
            plan.SourceArchivePath,
            plan.Entry.OriginalPath,
            plan.TargetPath,
            plan.Entry.IsDirectory,
            plan.Entry.Size);
    }

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

    private static string ResolveDefaultOutputPath(IReadOnlyList<string> sourcePaths, FileToolsSettings settings)
    {
        var parent = Path.GetDirectoryName(sourcePaths[0]) ?? Environment.CurrentDirectory;
        var name = settings.ArchiveMergeOutputNamePolicy switch
        {
            ArchiveMergeOutputNamePolicy.ParentFolderName => Path.GetFileName(parent),
            ArchiveMergeOutputNamePolicy.Timestamp => "Merged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"),
            _ => CreateCommonStem(sourcePaths.Select(Path.GetFileNameWithoutExtension).ToArray())
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

    private static string ResolveOutputCollision(string outputPath, IReadOnlyList<string> sourcePaths)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? "";
        var desiredName = Path.GetFileName(outputPath);
        var used = sourcePaths.Select(Path.GetFullPath).ToHashSet(PathComparer);
        if (!File.Exists(outputPath) && !Directory.Exists(outputPath) && !used.Contains(Path.GetFullPath(outputPath)))
        {
            return outputPath;
        }

        var stem = Path.GetFileNameWithoutExtension(desiredName);
        var extension = Path.GetExtension(desiredName);
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate) && !used.Contains(Path.GetFullPath(candidate)))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(Localizer.Get("ArchiveMergeOutputCollisionUnresolved"));
    }

    private static string[] NormalizeArchivePaths(IEnumerable<string> paths)
    {
        return paths
            .Select(static path => path.Trim().Trim('"'))
            .Where(static path => path.Length > 0 && File.Exists(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray();
    }

    private static string CreateCommonStem(IReadOnlyList<string?> stems)
    {
        var normalized = stems
            .Where(static stem => !string.IsNullOrWhiteSpace(stem))
            .Select(static stem => stem!)
            .ToArray();
        if (normalized.Length == 0)
        {
            return "";
        }

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

    private static string ReadAndHash(Stream stream, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        using var crypto = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);
        CopyStream(stream, crypto, cancellationToken);
        crypto.FlushFinalBlock();
        return Convert.ToHexString(sha.Hash ?? []);
    }

    private static string ReadAndDiscard(Stream stream, CancellationToken cancellationToken)
    {
        CopyStream(stream, Stream.Null, cancellationToken);
        return "";
    }

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

    private static string CombineEntryPath(string rootName, string entryPath)
    {
        return string.IsNullOrWhiteSpace(rootName)
            ? entryPath
            : EnsureDirectoryPath(rootName) + entryPath.TrimStart('/');
    }

    private static string EnsureDirectoryPath(string path)
    {
        return path.TrimEnd('/') + "/";
    }

    private static string TrimDirectoryPath(string path)
    {
        return path.TrimEnd('/');
    }

    private static string GetEntryDirectory(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/');
        var index = normalized.LastIndexOf('/');
        return index < 0 ? "" : normalized[..index];
    }

    private static string GetEntryFileName(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/').TrimEnd('/');
        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

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

        public bool IsSkipped => !string.IsNullOrWhiteSpace(SkipReason);

        public void Skip(string reason)
        {
            SkipReason = reason;
        }

        public static EntryMergePlan CreateSyntheticDirectory(SourceArchiveState state, string targetPath)
        {
            var entry = new ArchiveEntryInfo(
                state.SourcePath,
                EntryIndex: -1,
                targetPath,
                IsDirectory: true,
                Size: 0,
                new ArchiveEntryMetadata(DateTime.Now, DateTime.Now, DateTime.Now, Archived: null, 0, ExtraData: null, Comment: null));
            return new EntryMergePlan(state.SourcePath, entry, targetPath);
        }
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
        _entries = _archiveEntries
            .Select((entry, index) => CreateEntryInfo(path, entry, index))
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

    private static ArchiveEntryInfo CreateEntryInfo(string path, IArchiveEntry entry, int index)
    {
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
                ExtraData: null,
                TryReadComment(entry)));
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

internal sealed class SharpZipLibArchiveWriter : IArchiveWriter
{
    private readonly ZipOutputStream _stream;

    private SharpZipLibArchiveWriter(string path, ArchiveMergeCompressionLevel compressionLevel)
    {
        _stream = new ZipOutputStream(File.Create(path), StringCodec.FromEncoding(Encoding.UTF8))
        {
            IsStreamOwner = true
        };
        _stream.SetLevel(ToSharpZipLevel(compressionLevel));
    }

    public static SharpZipLibArchiveWriter Create(string path, ArchiveMergeCompressionLevel compressionLevel)
    {
        return new SharpZipLibArchiveWriter(path, compressionLevel);
    }

    public void WriteDirectory(string entryPath, ArchiveEntryMetadata metadata)
    {
        var entry = CreateEntry(EnsureDirectoryPath(entryPath), metadata);
        _stream.PutNextEntry(entry);
        _stream.CloseEntry();
    }

    public void WriteFile(string entryPath, Stream source, ArchiveEntryMetadata metadata, CancellationToken cancellationToken)
    {
        var entry = CreateEntry(entryPath.Replace('\\', '/').TrimStart('/'), metadata);
        _stream.PutNextEntry(entry);
        var buffer = new byte[128 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            _stream.Write(buffer, 0, read);
        }

        _stream.CloseEntry();
    }

    public void Complete()
    {
        _stream.Finish();
    }

    public void Dispose()
    {
        _stream.Dispose();
    }

    private static ZipEntry CreateEntry(string entryPath, ArchiveEntryMetadata metadata)
    {
        var isDirectory = entryPath.EndsWith("/", StringComparison.Ordinal);
        var entry = new ZipEntry(entryPath)
        {
            IsUnicodeText = true,
            DateTime = ClampZipDate(metadata.LastModified ?? DateTime.Now),
            ExternalFileAttributes = metadata.ExternalAttributes != 0 || !isDirectory
                ? metadata.ExternalAttributes
                : 16
        };

        var extraData = CreateExtraData(metadata);
        if (extraData is { Length: > 0 })
        {
            entry.ExtraData = extraData;
        }

        if (!string.IsNullOrWhiteSpace(metadata.Comment))
        {
            entry.Comment = metadata.Comment;
        }

        return entry;
    }

    private static byte[]? CreateExtraData(ArchiveEntryMetadata metadata)
    {
        var times = new[]
        {
            metadata.LastModified,
            metadata.LastAccessed,
            metadata.Created
        };
        if (metadata.ExtraData is not { Length: > 0 } &&
            times.All(static time => time is null))
        {
            return null;
        }

        using var stream = new MemoryStream();
        if (metadata.ExtraData is { Length: > 0 })
        {
            stream.Write(metadata.ExtraData);
        }

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

    private static long ToFileTime(DateTime value)
    {
        var normalized = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Local)
            : value;
        return normalized.ToUniversalTime().ToFileTimeUtc();
    }

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

    private static string EnsureDirectoryPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/').TrimEnd('/') + "/";
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
                    names));
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
