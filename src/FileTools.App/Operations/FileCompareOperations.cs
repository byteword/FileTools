using System.IO.Compression;
using System.Security.Cryptography;

namespace FileTools;

internal enum FileCompareNameMatchMode
{
    ExactFileName,
    Stem,
    RelativePath,
    None
}

internal enum FileCompareContentMode
{
    Hash,
    ByteToByte
}

internal enum FileCompareRangeMode
{
    Full,
    FrontBytes,
    BackBytes,
    MiddleBytes,
    FrontAndBackBytes
}

internal enum FileCompareArchiveMode
{
    AsFile,
    ExtractEntries
}

internal enum FileCompareArchiveEntryOrder
{
    Original,
    FileName
}

internal enum FileCompareStatus
{
    Same,
    Different,
    PartialMatch,
    Failed
}

internal sealed class FileCompareOptions
{
    public bool CompareFileName { get; set; } = true;

    public FileCompareNameMatchMode NameMatchMode { get; set; } = FileCompareNameMatchMode.ExactFileName;

    public bool CompareCreatedTime { get; set; }

    public bool CompareModifiedTime { get; set; } = true;

    public bool CompareFileSize { get; set; } = true;

    public bool CompareContent { get; set; } = true;

    public FileCompareContentMode ContentMode { get; set; } = FileCompareContentMode.Hash;

    public FileCompareRangeMode RangeMode { get; set; } = FileCompareRangeMode.Full;

    public long RangeBytes { get; set; } = 1024 * 1024;

    public double PartialMatchThreshold { get; set; } = 0.10;

    public bool EnableEarlyExit { get; set; } = true;

    public bool UseHashCache { get; set; } = true;

    public double ByteToBytePrefilterRatio { get; set; } = 0.10;

    public FileCompareArchiveMode ArchiveMode { get; set; } = FileCompareArchiveMode.AsFile;

    public FileCompareArchiveEntryOrder ArchiveEntryOrder { get; set; } = FileCompareArchiveEntryOrder.FileName;

    public FileCompareOptions Clone()
    {
        return new FileCompareOptions
        {
            CompareFileName = CompareFileName,
            NameMatchMode = NameMatchMode,
            CompareCreatedTime = CompareCreatedTime,
            CompareModifiedTime = CompareModifiedTime,
            CompareFileSize = CompareFileSize,
            CompareContent = CompareContent,
            ContentMode = ContentMode,
            RangeMode = RangeMode,
            RangeBytes = RangeBytes,
            PartialMatchThreshold = PartialMatchThreshold,
            EnableEarlyExit = EnableEarlyExit,
            UseHashCache = UseHashCache,
            ByteToBytePrefilterRatio = ByteToBytePrefilterRatio,
            ArchiveMode = ArchiveMode,
            ArchiveEntryOrder = ArchiveEntryOrder
        };
    }
}

internal sealed record FileCompareTarget(
    string Path,
    string RelativePath,
    string? RootPath);

internal sealed record FileCompareReport(
    IReadOnlyList<FileCompareTarget> Targets,
    IReadOnlyList<FileComparePairResult> Pairs,
    int HashCacheHits,
    int HashCacheMisses);

internal sealed record FileComparePairResult(
    FileCompareTarget Left,
    FileCompareTarget Right,
    FileCompareStatus Status,
    double MatchRatio,
    string Reason,
    IReadOnlyList<FileCompareCriterionResult> Criteria);

internal sealed record FileCompareCriterionResult(
    string Name,
    FileCompareStatus Status,
    double MatchRatio,
    string Detail);

internal sealed record FileCompareProgress(
    int CompletedPairs,
    int TotalPairs,
    string CurrentLeftPath,
    string CurrentRightPath);

internal static class FileCompareOperations
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly StringComparer EntryNameComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static FileCompareReport Compare(
        IEnumerable<string> paths,
        FileCompareOptions? options = null,
        IProgress<FileCompareProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedOptions = NormalizeOptions(options);
        var targets = CollectTargets(paths);
        var cache = new FileCompareHashCache(normalizedOptions.UseHashCache);
        var results = new List<FileComparePairResult>();
        var totalPairs = targets.Count * Math.Max(0, targets.Count - 1) / 2;

        for (var leftIndex = 0; leftIndex < targets.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < targets.Count; rightIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var left = targets[leftIndex];
                var right = targets[rightIndex];
                results.Add(ComparePair(left, right, normalizedOptions, cache, cancellationToken));
                progress?.Report(new FileCompareProgress(results.Count, totalPairs, left.Path, right.Path));
            }
        }

        return new FileCompareReport(targets, results, cache.Hits, cache.Misses);
    }

    public static IReadOnlyList<FileCompareTarget> CollectTargets(IEnumerable<string> paths)
    {
        var targets = new List<FileCompareTarget>();
        var seen = new HashSet<string>(PathComparer);
        foreach (var rawPath in paths)
        {
            var path = rawPath.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (File.Exists(path))
            {
                AddFile(Path.GetFullPath(path), rootPath: null);
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            var root = Path.GetFullPath(path);
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(PathComparer))
            {
                AddFile(Path.GetFullPath(file), root);
            }
        }

        return targets;

        void AddFile(string filePath, string? rootPath)
        {
            if (!seen.Add(filePath))
            {
                return;
            }

            var relativePath = rootPath is null
                ? Path.GetFileName(filePath)
                : Path.GetRelativePath(rootPath, filePath);
            targets.Add(new FileCompareTarget(filePath, relativePath, rootPath));
        }
    }

    private static FileComparePairResult ComparePair(
        FileCompareTarget left,
        FileCompareTarget right,
        FileCompareOptions options,
        FileCompareHashCache cache,
        CancellationToken cancellationToken)
    {
        var criteria = new List<FileCompareCriterionResult>();
        try
        {
            if (options.ArchiveMode == FileCompareArchiveMode.ExtractEntries &&
                IsSupportedArchiveContentPath(left.Path) &&
                IsSupportedArchiveContentPath(right.Path))
            {
                return CompareArchives(left, right, options, criteria, cancellationToken);
            }

            var leftInfo = new FileInfo(left.Path);
            var rightInfo = new FileInfo(right.Path);
            var earlyResult = CompareFileIdentityAndMetadata(left, right, leftInfo, rightInfo, options, criteria);
            if (earlyResult is not null)
            {
                return earlyResult;
            }

            if (options.CompareContent)
            {
                criteria.Add(CompareContent(
                    leftInfo.Length,
                    () => File.OpenRead(left.Path),
                    CreateFileCacheKey(leftInfo),
                    rightInfo.Length,
                    () => File.OpenRead(right.Path),
                    CreateFileCacheKey(rightInfo),
                    options,
                    cache,
                    cancellationToken));
            }

            return CreatePairResult(left, right, criteria, options.PartialMatchThreshold);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            criteria.Add(new FileCompareCriterionResult("Failure", FileCompareStatus.Failed, 0, ex.Message));
            return new FileComparePairResult(left, right, FileCompareStatus.Failed, 0, ex.Message, criteria);
        }
    }

    private static FileComparePairResult? CompareFileIdentityAndMetadata(
        FileCompareTarget left,
        FileCompareTarget right,
        FileInfo leftInfo,
        FileInfo rightInfo,
        FileCompareOptions options,
        List<FileCompareCriterionResult> criteria)
    {
        if (options.CompareFileName && options.NameMatchMode != FileCompareNameMatchMode.None)
        {
            var leftName = GetNameForComparison(left, options.NameMatchMode);
            var rightName = GetNameForComparison(right, options.NameMatchMode);
            var sameName = EntryNameComparer.Equals(leftName, rightName);
            criteria.Add(new FileCompareCriterionResult(
                "File name",
                sameName ? FileCompareStatus.Same : FileCompareStatus.Different,
                sameName ? 1 : 0,
                $"{leftName} <-> {rightName}"));
            if (!sameName && options.EnableEarlyExit)
            {
                return CreatePairResult(left, right, criteria, options.PartialMatchThreshold);
            }
        }

        if (options.CompareFileSize)
        {
            var sameSize = leftInfo.Length == rightInfo.Length;
            criteria.Add(new FileCompareCriterionResult(
                "File size",
                sameSize ? FileCompareStatus.Same : FileCompareStatus.Different,
                sameSize ? 1 : 0,
                $"{leftInfo.Length} <-> {rightInfo.Length}"));
            if (!sameSize && options.EnableEarlyExit)
            {
                return CreatePairResult(left, right, criteria, options.PartialMatchThreshold);
            }
        }

        if (options.CompareCreatedTime)
        {
            var sameCreated = leftInfo.CreationTimeUtc == rightInfo.CreationTimeUtc;
            criteria.Add(new FileCompareCriterionResult(
                "Created time",
                sameCreated ? FileCompareStatus.Same : FileCompareStatus.Different,
                sameCreated ? 1 : 0,
                $"{leftInfo.CreationTimeUtc:o} <-> {rightInfo.CreationTimeUtc:o}"));
            if (!sameCreated && options.EnableEarlyExit)
            {
                return CreatePairResult(left, right, criteria, options.PartialMatchThreshold);
            }
        }

        if (options.CompareModifiedTime)
        {
            var sameModified = leftInfo.LastWriteTimeUtc == rightInfo.LastWriteTimeUtc;
            criteria.Add(new FileCompareCriterionResult(
                "Modified time",
                sameModified ? FileCompareStatus.Same : FileCompareStatus.Different,
                sameModified ? 1 : 0,
                $"{leftInfo.LastWriteTimeUtc:o} <-> {rightInfo.LastWriteTimeUtc:o}"));
            if (!sameModified && options.EnableEarlyExit)
            {
                return CreatePairResult(left, right, criteria, options.PartialMatchThreshold);
            }
        }

        return null;
    }

    private static FileComparePairResult CompareArchives(
        FileCompareTarget left,
        FileCompareTarget right,
        FileCompareOptions options,
        List<FileCompareCriterionResult> criteria,
        CancellationToken cancellationToken)
    {
        using var leftArchive = ZipFile.OpenRead(left.Path);
        using var rightArchive = ZipFile.OpenRead(right.Path);
        var leftEntries = GetArchiveEntries(leftArchive, options.ArchiveEntryOrder);
        var rightEntries = GetArchiveEntries(rightArchive, options.ArchiveEntryOrder);
        var pairedCount = Math.Min(leftEntries.Count, rightEntries.Count);
        var entryCriteria = new List<FileCompareCriterionResult>();

        if (leftEntries.Count != rightEntries.Count)
        {
            entryCriteria.Add(new FileCompareCriterionResult(
                "Archive entry count",
                FileCompareStatus.Different,
                0,
                $"{leftEntries.Count} <-> {rightEntries.Count}"));
        }

        if (options.CompareCreatedTime)
        {
            entryCriteria.Add(new FileCompareCriterionResult(
                "Archive created time",
                FileCompareStatus.Failed,
                0,
                "ZIP entries do not expose creation time through this comparison path."));
        }

        var cache = new FileCompareHashCache(enabled: false);
        for (var index = 0; index < pairedCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var leftEntry = leftEntries[index];
            var rightEntry = rightEntries[index];
            CompareArchiveEntry(index, leftEntry, rightEntry, options, entryCriteria, cache, cancellationToken);
        }

        criteria.Add(new FileCompareCriterionResult(
            "Archive entries",
            GetAggregateStatus(entryCriteria, options.PartialMatchThreshold),
            CalculateAverageRatio(entryCriteria),
            $"{leftEntries.Count} entries <-> {rightEntries.Count} entries ({options.ArchiveEntryOrder})"));
        criteria.AddRange(entryCriteria);
        return CreatePairResult(left, right, criteria, options.PartialMatchThreshold);
    }

    private static void CompareArchiveEntry(
        int index,
        ZipArchiveEntry leftEntry,
        ZipArchiveEntry rightEntry,
        FileCompareOptions options,
        List<FileCompareCriterionResult> criteria,
        FileCompareHashCache cache,
        CancellationToken cancellationToken)
    {
        var prefix = $"Entry {index + 1}";
        if (options.CompareFileName && options.NameMatchMode != FileCompareNameMatchMode.None)
        {
            var leftName = GetEntryNameForComparison(leftEntry.FullName, options.NameMatchMode);
            var rightName = GetEntryNameForComparison(rightEntry.FullName, options.NameMatchMode);
            var sameName = EntryNameComparer.Equals(leftName, rightName);
            criteria.Add(new FileCompareCriterionResult(
                prefix + " name",
                sameName ? FileCompareStatus.Same : FileCompareStatus.Different,
                sameName ? 1 : 0,
                $"{leftName} <-> {rightName}"));
            if (!sameName && options.EnableEarlyExit)
            {
                return;
            }
        }

        if (options.CompareFileSize)
        {
            var sameSize = leftEntry.Length == rightEntry.Length;
            criteria.Add(new FileCompareCriterionResult(
                prefix + " size",
                sameSize ? FileCompareStatus.Same : FileCompareStatus.Different,
                sameSize ? 1 : 0,
                $"{leftEntry.Length} <-> {rightEntry.Length}"));
            if (!sameSize && options.EnableEarlyExit)
            {
                return;
            }
        }

        if (options.CompareModifiedTime)
        {
            var sameModified = leftEntry.LastWriteTime.UtcDateTime == rightEntry.LastWriteTime.UtcDateTime;
            criteria.Add(new FileCompareCriterionResult(
                prefix + " modified time",
                sameModified ? FileCompareStatus.Same : FileCompareStatus.Different,
                sameModified ? 1 : 0,
                $"{leftEntry.LastWriteTime.UtcDateTime:o} <-> {rightEntry.LastWriteTime.UtcDateTime:o}"));
            if (!sameModified && options.EnableEarlyExit)
            {
                return;
            }
        }

        if (options.CompareContent)
        {
            criteria.Add(CompareContent(
                leftEntry.Length,
                leftEntry.Open,
                cacheKeyBaseLeft: null,
                rightEntry.Length,
                rightEntry.Open,
                cacheKeyBaseRight: null,
                options,
                cache,
                cancellationToken) with { Name = prefix + " content" });
        }
    }

    private static FileCompareCriterionResult CompareContent(
        long leftLength,
        Func<Stream> openLeft,
        string? cacheKeyBaseLeft,
        long rightLength,
        Func<Stream> openRight,
        string? cacheKeyBaseRight,
        FileCompareOptions options,
        FileCompareHashCache cache,
        CancellationToken cancellationToken)
    {
        var leftRanges = CreateRanges(leftLength, options);
        var rightRanges = CreateRanges(rightLength, options);

        return options.ContentMode == FileCompareContentMode.ByteToByte
            ? CompareContentByteToByte(leftLength, openLeft, leftRanges, rightLength, openRight, rightRanges, options, cache, cancellationToken)
            : CompareContentByHash(openLeft, cacheKeyBaseLeft, leftRanges, openRight, cacheKeyBaseRight, rightRanges, options, cache, cancellationToken);
    }

    private static FileCompareCriterionResult CompareContentByHash(
        Func<Stream> openLeft,
        string? cacheKeyBaseLeft,
        IReadOnlyList<ContentRange> leftRanges,
        Func<Stream> openRight,
        string? cacheKeyBaseRight,
        IReadOnlyList<ContentRange> rightRanges,
        FileCompareOptions options,
        FileCompareHashCache cache,
        CancellationToken cancellationToken)
    {
        var total = 0L;
        var matched = 0L;
        var count = Math.Max(leftRanges.Count, rightRanges.Count);
        for (var index = 0; index < count; index++)
        {
            var leftRange = index < leftRanges.Count ? leftRanges[index] : new ContentRange(0, 0);
            var rightRange = index < rightRanges.Count ? rightRanges[index] : new ContentRange(0, 0);
            var compared = Math.Max(leftRange.Length, rightRange.Length);
            total += compared;

            if (leftRange.Length != rightRange.Length)
            {
                continue;
            }

            var leftHash = GetHash(openLeft, cacheKeyBaseLeft, [leftRange], cache, cancellationToken);
            var rightHash = GetHash(openRight, cacheKeyBaseRight, [rightRange], cache, cancellationToken);
            if (leftHash.AsSpan().SequenceEqual(rightHash))
            {
                matched += compared;
            }
        }

        return CreateContentResult(matched, total, options.PartialMatchThreshold, "Content hash");
    }

    private static FileCompareCriterionResult CompareContentByteToByte(
        long leftLength,
        Func<Stream> openLeft,
        IReadOnlyList<ContentRange> leftRanges,
        long rightLength,
        Func<Stream> openRight,
        IReadOnlyList<ContentRange> rightRanges,
        FileCompareOptions options,
        FileCompareHashCache cache,
        CancellationToken cancellationToken)
    {
        var comparedLength = Math.Min(SumLength(leftRanges), SumLength(rightRanges));
        if (comparedLength > 0 && options.ByteToBytePrefilterRatio > 0)
        {
            var prefilterLength = Math.Max(1, (long)Math.Ceiling(comparedLength * options.ByteToBytePrefilterRatio));
            var leftPrefilterRanges = TakeLeadingBytes(leftRanges, prefilterLength);
            var rightPrefilterRanges = TakeLeadingBytes(rightRanges, prefilterLength);
            var leftPrefilterHash = GetHash(openLeft, cacheKeyBase: null, leftPrefilterRanges, cache, cancellationToken);
            var rightPrefilterHash = GetHash(openRight, cacheKeyBase: null, rightPrefilterRanges, cache, cancellationToken);
            if (!leftPrefilterHash.AsSpan().SequenceEqual(rightPrefilterHash))
            {
                return new FileCompareCriterionResult(
                    "Content",
                    FileCompareStatus.Different,
                    0,
                    $"Byte prefilter mismatch in first {prefilterLength} selected bytes.");
            }
        }

        var (matched, total) = CountMatchingBytes(
            leftLength,
            openLeft,
            leftRanges,
            rightLength,
            openRight,
            rightRanges,
            cancellationToken);
        return CreateContentResult(matched, total, options.PartialMatchThreshold, "Byte-to-byte content");
    }

    private static (long Matched, long Total) CountMatchingBytes(
        long leftLength,
        Func<Stream> openLeft,
        IReadOnlyList<ContentRange> leftRanges,
        long rightLength,
        Func<Stream> openRight,
        IReadOnlyList<ContentRange> rightRanges,
        CancellationToken cancellationToken)
    {
        _ = leftLength;
        _ = rightLength;
        using var left = openLeft();
        using var right = openRight();
        var matched = 0L;
        var total = 0L;
        var count = Math.Max(leftRanges.Count, rightRanges.Count);
        var leftPosition = 0L;
        var rightPosition = 0L;
        var leftBuffer = new byte[128 * 1024];
        var rightBuffer = new byte[128 * 1024];

        for (var index = 0; index < count; index++)
        {
            var leftRange = index < leftRanges.Count ? leftRanges[index] : new ContentRange(0, 0);
            var rightRange = index < rightRanges.Count ? rightRanges[index] : new ContentRange(0, 0);
            total += Math.Max(leftRange.Length, rightRange.Length);

            leftPosition = MoveToRangeStart(left, leftPosition, leftRange.Offset, leftBuffer, cancellationToken);
            rightPosition = MoveToRangeStart(right, rightPosition, rightRange.Offset, rightBuffer, cancellationToken);
            var remaining = Math.Min(leftRange.Length, rightRange.Length);
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var readLength = (int)Math.Min(remaining, leftBuffer.Length);
                var leftRead = ReadExactlyUpTo(left, leftBuffer, readLength);
                var rightRead = ReadExactlyUpTo(right, rightBuffer, readLength);
                var actual = Math.Min(leftRead, rightRead);
                for (var i = 0; i < actual; i++)
                {
                    if (leftBuffer[i] == rightBuffer[i])
                    {
                        matched++;
                    }
                }

                leftPosition += leftRead;
                rightPosition += rightRead;
                remaining -= actual;
                if (actual == 0)
                {
                    break;
                }
            }
        }

        return (matched, total);
    }

    private static FileCompareCriterionResult CreateContentResult(
        long matched,
        long total,
        double partialMatchThreshold,
        string detail)
    {
        var ratio = total == 0 ? 1 : Math.Clamp((double)matched / total, 0, 1);
        var status = ratio >= 1
            ? FileCompareStatus.Same
            : ratio >= partialMatchThreshold
                ? FileCompareStatus.PartialMatch
                : FileCompareStatus.Different;
        return new FileCompareCriterionResult("Content", status, ratio, detail);
    }

    private static byte[] GetHash(
        Func<Stream> openStream,
        string? cacheKeyBase,
        IReadOnlyList<ContentRange> ranges,
        FileCompareHashCache cache,
        CancellationToken cancellationToken)
    {
        var rangeKey = string.Join("|", ranges.Select(static range => range.Offset + ":" + range.Length));
        var cacheKey = cacheKeyBase is null ? null : cacheKeyBase + "|sha256|" + rangeKey;
        if (cacheKey is not null && cache.TryGet(cacheKey, out var cached))
        {
            return cached;
        }

        using var stream = openStream();
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        var position = 0L;
        foreach (var range in ranges.OrderBy(static range => range.Offset))
        {
            cancellationToken.ThrowIfCancellationRequested();
            position = MoveToRangeStart(stream, position, range.Offset, buffer, cancellationToken);
            var remaining = range.Length;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var readLength = (int)Math.Min(remaining, buffer.Length);
                var read = stream.Read(buffer, 0, readLength);
                if (read == 0)
                {
                    break;
                }

                sha.AppendData(buffer.AsSpan(0, read));
                position += read;
                remaining -= read;
            }
        }

        var hash = sha.GetHashAndReset();
        if (cacheKey is not null)
        {
            cache.Set(cacheKey, hash);
        }

        return hash;
    }

    private static long MoveToRangeStart(
        Stream stream,
        long currentPosition,
        long targetPosition,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        if (targetPosition < currentPosition && !stream.CanSeek)
        {
            throw new InvalidOperationException("The stream cannot seek backward for the selected comparison range.");
        }

        if (stream.CanSeek)
        {
            stream.Position = targetPosition;
            return targetPosition;
        }

        var remaining = targetPosition - currentPosition;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readLength = (int)Math.Min(remaining, buffer.Length);
            var read = stream.Read(buffer, 0, readLength);
            if (read == 0)
            {
                break;
            }

            remaining -= read;
            currentPosition += read;
        }

        return currentPosition;
    }

    private static int ReadExactlyUpTo(Stream stream, byte[] buffer, int length)
    {
        var offset = 0;
        while (offset < length)
        {
            var read = stream.Read(buffer, offset, length - offset);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return offset;
    }

    private static IReadOnlyList<ContentRange> CreateRanges(long length, FileCompareOptions options)
    {
        if (length <= 0)
        {
            return [new ContentRange(0, 0)];
        }

        if (options.RangeMode == FileCompareRangeMode.Full)
        {
            return [new ContentRange(0, length)];
        }

        var rangeLength = Math.Min(length, Math.Max(1, options.RangeBytes));
        var ranges = options.RangeMode switch
        {
            FileCompareRangeMode.FrontBytes => new List<ContentRange> { new(0, rangeLength) },
            FileCompareRangeMode.BackBytes => new List<ContentRange> { new(length - rangeLength, rangeLength) },
            FileCompareRangeMode.MiddleBytes => new List<ContentRange> { new(Math.Max(0, (length - rangeLength) / 2), rangeLength) },
            FileCompareRangeMode.FrontAndBackBytes => new List<ContentRange>
            {
                new(0, rangeLength),
                new(Math.Max(0, length - rangeLength), rangeLength)
            },
            _ => new List<ContentRange> { new(0, length) }
        };
        return MergeRanges(ranges, length);
    }

    private static IReadOnlyList<ContentRange> MergeRanges(IEnumerable<ContentRange> ranges, long length)
    {
        var normalized = ranges
            .Select(range =>
            {
                var offset = Math.Clamp(range.Offset, 0, Math.Max(0, length));
                var rangeLength = Math.Clamp(range.Length, 0, Math.Max(0, length - offset));
                return new ContentRange(offset, rangeLength);
            })
            .Where(static range => range.Length >= 0)
            .OrderBy(static range => range.Offset)
            .ToList();
        if (normalized.Count == 0)
        {
            return [new ContentRange(0, 0)];
        }

        var merged = new List<ContentRange>();
        foreach (var range in normalized)
        {
            if (merged.Count == 0)
            {
                merged.Add(range);
                continue;
            }

            var last = merged[^1];
            var lastEnd = last.Offset + last.Length;
            if (range.Offset <= lastEnd)
            {
                merged[^1] = last with { Length = Math.Max(lastEnd, range.Offset + range.Length) - last.Offset };
                continue;
            }

            merged.Add(range);
        }

        return merged;
    }

    private static IReadOnlyList<ContentRange> TakeLeadingBytes(IReadOnlyList<ContentRange> ranges, long byteCount)
    {
        var result = new List<ContentRange>();
        var remaining = byteCount;
        foreach (var range in ranges)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(range.Length, remaining);
            result.Add(range with { Length = take });
            remaining -= take;
        }

        return result.Count == 0 ? [new ContentRange(0, 0)] : result;
    }

    private static long SumLength(IEnumerable<ContentRange> ranges)
    {
        return ranges.Sum(static range => range.Length);
    }

    private static FileComparePairResult CreatePairResult(
        FileCompareTarget left,
        FileCompareTarget right,
        IReadOnlyList<FileCompareCriterionResult> criteria,
        double partialMatchThreshold)
    {
        var status = GetAggregateStatus(criteria, partialMatchThreshold);
        var ratio = CalculateAverageRatio(criteria);
        var reason = criteria.FirstOrDefault(item => item.Status != FileCompareStatus.Same)?.Detail ??
                     "All selected comparison criteria matched.";
        return new FileComparePairResult(left, right, status, ratio, reason, criteria);
    }

    private static FileCompareStatus GetAggregateStatus(
        IReadOnlyList<FileCompareCriterionResult> criteria,
        double partialMatchThreshold)
    {
        if (criteria.Count == 0)
        {
            return FileCompareStatus.Failed;
        }

        if (criteria.Any(static item => item.Status == FileCompareStatus.Failed))
        {
            return FileCompareStatus.Failed;
        }

        if (criteria.All(static item => item.Status == FileCompareStatus.Same))
        {
            return FileCompareStatus.Same;
        }

        return CalculateAverageRatio(criteria) >= partialMatchThreshold
            ? FileCompareStatus.PartialMatch
            : FileCompareStatus.Different;
    }

    private static double CalculateAverageRatio(IReadOnlyList<FileCompareCriterionResult> criteria)
    {
        if (criteria.Count == 0)
        {
            return 0;
        }

        return Math.Clamp(criteria.Average(static item => item.MatchRatio), 0, 1);
    }

    private static string GetNameForComparison(FileCompareTarget target, FileCompareNameMatchMode mode)
    {
        return mode switch
        {
            FileCompareNameMatchMode.Stem => Path.GetFileNameWithoutExtension(target.RelativePath),
            FileCompareNameMatchMode.RelativePath => target.RelativePath.Replace('\\', '/'),
            FileCompareNameMatchMode.None => "",
            _ => Path.GetFileName(target.RelativePath)
        };
    }

    private static string GetEntryNameForComparison(string entryPath, FileCompareNameMatchMode mode)
    {
        var normalized = entryPath.Replace('\\', '/').TrimEnd('/');
        var slashIndex = normalized.LastIndexOf('/');
        var fileName = slashIndex < 0 ? normalized : normalized[(slashIndex + 1)..];
        return mode switch
        {
            FileCompareNameMatchMode.Stem => System.IO.Path.GetFileNameWithoutExtension(fileName),
            FileCompareNameMatchMode.RelativePath => normalized,
            FileCompareNameMatchMode.None => "",
            _ => fileName
        };
    }

    private static IReadOnlyList<ZipArchiveEntry> GetArchiveEntries(
        ZipArchive archive,
        FileCompareArchiveEntryOrder order)
    {
        var entries = archive.Entries
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Name))
            .ToList();
        return order == FileCompareArchiveEntryOrder.FileName
            ? entries.OrderBy(static entry => entry.FullName, EntryNameComparer).ToList()
            : entries;
    }

    private static bool IsSupportedArchiveContentPath(string path)
    {
        return string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateFileCacheKey(FileInfo file)
    {
        return string.Join(
            "|",
            Path.GetFullPath(file.FullName),
            file.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            file.LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            file.CreationTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static FileCompareOptions NormalizeOptions(FileCompareOptions? options)
    {
        var normalized = options?.Clone() ?? new FileCompareOptions();
        normalized.RangeBytes = Math.Max(1, normalized.RangeBytes);
        normalized.PartialMatchThreshold = Math.Clamp(normalized.PartialMatchThreshold, 0.10, 1);
        normalized.ByteToBytePrefilterRatio = Math.Clamp(normalized.ByteToBytePrefilterRatio, 0, 1);
        if (!normalized.CompareFileName &&
            !normalized.CompareCreatedTime &&
            !normalized.CompareModifiedTime &&
            !normalized.CompareFileSize &&
            !normalized.CompareContent)
        {
            normalized.CompareContent = true;
        }

        return normalized;
    }

    private readonly record struct ContentRange(long Offset, long Length);

    private sealed class FileCompareHashCache
    {
        private readonly bool _enabled;
        private readonly Dictionary<string, byte[]> _hashes = new(StringComparer.Ordinal);

        public FileCompareHashCache(bool enabled)
        {
            _enabled = enabled;
        }

        public int Hits { get; private set; }

        public int Misses { get; private set; }

        public bool TryGet(string key, out byte[] hash)
        {
            if (_enabled && _hashes.TryGetValue(key, out hash!))
            {
                Hits++;
                return true;
            }

            hash = [];
            return false;
        }

        public void Set(string key, byte[] hash)
        {
            if (!_enabled)
            {
                return;
            }

            Misses++;
            _hashes[key] = hash;
        }
    }
}

internal static class FileCompareText
{
    public static string GetDisplayName(FileCompareNameMatchMode mode)
    {
        return mode switch
        {
            FileCompareNameMatchMode.ExactFileName => Localizer.Get("FileCompareNameModeExact"),
            FileCompareNameMatchMode.Stem => Localizer.Get("FileCompareNameModeStem"),
            FileCompareNameMatchMode.RelativePath => Localizer.Get("FileCompareNameModeRelativePath"),
            FileCompareNameMatchMode.None => Localizer.Get("FileCompareNameModeNone"),
            _ => mode.ToString()
        };
    }

    public static string GetDisplayName(FileCompareContentMode mode)
    {
        return mode switch
        {
            FileCompareContentMode.ByteToByte => Localizer.Get("FileCompareContentModeByteToByte"),
            _ => Localizer.Get("FileCompareContentModeHash")
        };
    }

    public static string GetDisplayName(FileCompareRangeMode mode)
    {
        return mode switch
        {
            FileCompareRangeMode.FrontBytes => Localizer.Get("FileCompareRangeFront"),
            FileCompareRangeMode.BackBytes => Localizer.Get("FileCompareRangeBack"),
            FileCompareRangeMode.MiddleBytes => Localizer.Get("FileCompareRangeMiddle"),
            FileCompareRangeMode.FrontAndBackBytes => Localizer.Get("FileCompareRangeFrontAndBack"),
            _ => Localizer.Get("FileCompareRangeFull")
        };
    }

    public static string GetDisplayName(FileCompareArchiveMode mode)
    {
        return mode switch
        {
            FileCompareArchiveMode.ExtractEntries => Localizer.Get("FileCompareArchiveModeExtractEntries"),
            _ => Localizer.Get("FileCompareArchiveModeAsFile")
        };
    }

    public static string GetDisplayName(FileCompareArchiveEntryOrder order)
    {
        return order switch
        {
            FileCompareArchiveEntryOrder.Original => Localizer.Get("FileCompareArchiveOrderOriginal"),
            _ => Localizer.Get("FileCompareArchiveOrderFileName")
        };
    }

    public static string GetDisplayName(FileCompareStatus status)
    {
        return status switch
        {
            FileCompareStatus.Same => Localizer.Get("FileCompareStatusSame"),
            FileCompareStatus.Different => Localizer.Get("FileCompareStatusDifferent"),
            FileCompareStatus.PartialMatch => Localizer.Get("FileCompareStatusPartialMatch"),
            FileCompareStatus.Failed => Localizer.Get("FileCompareStatusFailed"),
            _ => status.ToString()
        };
    }
}
