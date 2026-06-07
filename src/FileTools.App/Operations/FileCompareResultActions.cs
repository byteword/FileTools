namespace FileTools;

internal enum FileCompareDuplicateKeepMode
{
    LargestSizeOldestCreated,
    ComparisonOrder,
    NewestModified,
    OldestModified,
    ShortestPath,
    LongestPath
}

internal sealed record FileCompareDuplicateGroup(
    int Number,
    IReadOnlyList<string> Paths)
{
    public string KeepPath => Paths.Count == 0 ? "" : Paths[0];

    public IReadOnlyList<string> DeleteCandidates => Paths.Skip(1).ToArray();
}

internal sealed record FileCompareDuplicateDeleteHandoff(
    IReadOnlyList<string> AllPaths,
    IReadOnlyList<string> DeletePaths,
    IReadOnlyList<FileCompareDuplicateDeleteGroupHandoff> Groups);

internal sealed record FileCompareDuplicateDeleteGroupHandoff(
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> DeletePaths);

internal static class FileCompareResultActions
{
    public static IReadOnlyList<FileCompareDuplicateGroup> BuildDuplicateGroups(
        FileCompareReport report,
        FileCompareDuplicateKeepMode keepMode = FileCompareDuplicateKeepMode.ComparisonOrder)
    {
        var orderedPaths = report.Targets
            .Select(static target => target.Path)
            .Distinct(GetPathComparer())
            .ToArray();
        if (orderedPaths.Length < 2)
        {
            return [];
        }

        var parent = orderedPaths.ToDictionary(static path => path, static path => path, GetPathComparer());
        foreach (var pair in report.Pairs.Where(IsSameContentPair))
        {
            Union(parent, pair.Left.Path, pair.Right.Path);
        }

        return orderedPaths
            .GroupBy(path => Find(parent, path), GetPathComparer())
            .Where(static group => group.Count() > 1)
            .Select((group, index) => new FileCompareDuplicateGroup(index + 1, ApplyKeepMode(group.ToArray(), keepMode)))
            .ToArray();
    }

    public static IReadOnlyList<string> GetPairPaths(FileComparePairResult? pair)
    {
        if (pair is null)
        {
            return [];
        }

        return new[] { pair.Left.Path, pair.Right.Path }
            .Distinct(GetPathComparer())
            .ToArray();
    }

    public static IReadOnlyList<string> GetDeleteCandidates(IEnumerable<FileCompareDuplicateGroup> groups)
    {
        return groups
            .SelectMany(static group => group.DeleteCandidates)
            .Distinct(GetPathComparer())
            .ToArray();
    }

    public static FileCompareDuplicateDeleteHandoff CreateDuplicateDeleteHandoff(
        IEnumerable<FileCompareDuplicateGroup> groups)
    {
        var selectedGroups = groups.ToArray();
        var groupHandoffs = selectedGroups
            .Select(static group => new FileCompareDuplicateDeleteGroupHandoff(
                group.Paths,
                group.DeleteCandidates))
            .ToArray();
        return new FileCompareDuplicateDeleteHandoff(
            groupHandoffs
                .SelectMany(static group => group.Paths)
                .Distinct(GetPathComparer())
                .ToArray(),
            groupHandoffs
                .SelectMany(static group => group.DeletePaths)
                .Distinct(GetPathComparer())
                .ToArray(),
            groupHandoffs);
    }

    private static bool IsSameContentPair(FileComparePairResult pair)
    {
        return pair.Status == FileCompareStatus.Same &&
               pair.Criteria.Any(static criterion =>
                   criterion.Status == FileCompareStatus.Same &&
                   IsContentCriterionName(criterion.Name));
    }

    private static bool IsContentCriterionName(string name)
    {
        return string.Equals(name, "Content", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(" content", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ApplyKeepMode(IReadOnlyList<string> paths, FileCompareDuplicateKeepMode keepMode)
    {
        if (paths.Count <= 1)
        {
            return paths;
        }

        return keepMode switch
        {
            FileCompareDuplicateKeepMode.LargestSizeOldestCreated => ApplyLargestSizeOldestCreatedKeepMode(paths),
            FileCompareDuplicateKeepMode.NewestModified => paths
                .OrderByDescending(GetLastWriteTimeUtc)
                .ThenBy(static path => path, GetPathComparer())
                .ToArray(),
            FileCompareDuplicateKeepMode.OldestModified => paths
                .OrderBy(GetLastWriteTimeUtc)
                .ThenBy(static path => path, GetPathComparer())
                .ToArray(),
            FileCompareDuplicateKeepMode.ShortestPath => paths
                .OrderBy(static path => path.Length)
                .ThenBy(static path => path, GetPathComparer())
                .ToArray(),
            FileCompareDuplicateKeepMode.LongestPath => paths
                .OrderByDescending(static path => path.Length)
                .ThenBy(static path => path, GetPathComparer())
                .ToArray(),
            _ => paths
        };
    }

    private static IReadOnlyList<string> ApplyLargestSizeOldestCreatedKeepMode(IReadOnlyList<string> paths)
    {
        var keepPath = paths
            .OrderByDescending(GetFileSize)
            .ThenBy(GetCreationTimeUtc)
            .ThenBy(static path => path, GetPathComparer())
            .First();
        return new[] { keepPath }
            .Concat(paths
                .Where(path => !string.Equals(path, keepPath, GetPathComparison()))
                .OrderBy(GetFileSize)
                .ThenByDescending(GetCreationTimeUtc)
                .ThenBy(static path => path, GetPathComparer()))
            .ToArray();
    }

    private static long GetFileSize(string path)
    {
        try
        {
            return File.Exists(path)
                ? new FileInfo(path).Length
                : long.MinValue;
        }
        catch
        {
            return long.MinValue;
        }
    }

    private static DateTime GetCreationTimeUtc(string path)
    {
        try
        {
            return File.Exists(path)
                ? File.GetCreationTimeUtc(path)
                : DateTime.MaxValue;
        }
        catch
        {
            return DateTime.MaxValue;
        }
    }

    private static DateTime GetLastWriteTimeUtc(string path)
    {
        try
        {
            return File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static void Union(Dictionary<string, string> parent, string left, string right)
    {
        if (!parent.ContainsKey(left) || !parent.ContainsKey(right))
        {
            return;
        }

        var leftRoot = Find(parent, left);
        var rightRoot = Find(parent, right);
        if (string.Equals(leftRoot, rightRoot, GetPathComparison()))
        {
            return;
        }

        parent[rightRoot] = leftRoot;
    }

    private static string Find(Dictionary<string, string> parent, string path)
    {
        var current = path;
        while (!string.Equals(parent[current], current, GetPathComparison()))
        {
            current = parent[current];
        }

        var root = current;
        current = path;
        while (!string.Equals(parent[current], current, GetPathComparison()))
        {
            var next = parent[current];
            parent[current] = root;
            current = next;
        }

        return root;
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
