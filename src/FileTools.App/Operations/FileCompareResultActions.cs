namespace FileTools;

/// <summary>
/// 파일 비교 결과에서 중복군 구성과 삭제 후보를 결정한다.
/// </summary>
internal enum FileCompareDuplicateKeepMode
{
    LargestSizeOldestCreated,
    ComparisonOrder,
    NewestModified,
    OldestModified,
    ShortestPath,
    LongestPath
}

/// <summary>
/// 동일 내용 연결성 그룹에 대한 UI/실행 공유 DTO.
/// </summary>
internal sealed record FileCompareDuplicateGroup(
    int Number,
    IReadOnlyList<string> Paths)
{
    public string KeepPath => Paths.Count == 0 ? "" : Paths[0];

    public IReadOnlyList<string> DeleteCandidates => Paths.Skip(1).ToArray();
}

/// <summary>
/// 중복 삭제 단계로 넘길 때 사용할 그룹 통합 핸드오프 구조.
/// </summary>
internal sealed record FileCompareDuplicateDeleteHandoff(
    IReadOnlyList<string> AllPaths,
    IReadOnlyList<string> DeletePaths,
    IReadOnlyList<FileCompareDuplicateDeleteGroupHandoff> Groups);

/// <summary>
/// 개별 중복군의 삭제 후보 집합을 표현한다.
/// </summary>
internal sealed record FileCompareDuplicateDeleteGroupHandoff(
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> DeletePaths);

/// <summary>
/// 중복 판정, 우선순위 규칙, 인덱스 기반 삭제 후보 추출의 집약점.
/// </summary>
internal static class FileCompareResultActions
{
    /// <summary>
    /// 비교 리포트에서 동일 그룹(동일 내용 연결성)이 2개 이상인 경로 묶음을 생성한다.
    /// </summary>
    /// <remarks>
    /// 비교 결과의 같은-content쌍을 union-find로 묶어 연결 컴포넌트 단위의 중복군을 구성한다.
    /// </remarks>
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

    /// <summary>
    /// 비교 쌍에서 실제 경로 목록만 안전하게 추출한다.
    /// </summary>
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

    /// <summary>
    /// 각 중복군의 삭제 대상 경로만 합쳐서 반환한다.
    /// </summary>
    public static IReadOnlyList<string> GetDeleteCandidates(IEnumerable<FileCompareDuplicateGroup> groups)
    {
        return groups
            .SelectMany(static group => group.DeleteCandidates)
            .Distinct(GetPathComparer())
            .ToArray();
    }

    /// <summary>
    /// UI 전달용 중복군/삭제군 핸드오프 구조체를 생성한다.
    /// </summary>
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

    /// <summary>
    /// 비교 판정이 ‘동일 콘텐츠’로 확정된 쌍인지 판단한다.
    /// </summary>
    private static bool IsSameContentPair(FileComparePairResult pair)
    {
        return pair.Status == FileCompareStatus.Same &&
               pair.Criteria.Any(static criterion =>
                   criterion.Status == FileCompareStatus.Same &&
                   IsContentCriterionName(criterion.Name));
    }

    /// <summary>
    /// 파일명 판정 기준이 아닌 “content” 기반 비교 기준인지 판별한다.
    /// </summary>
    private static bool IsContentCriterionName(string name)
    {
        return string.Equals(name, "Content", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(" content", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 유지 모드에 맞춰 대표 경로 우선순위를 계산한다.
    /// </summary>
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

    /// <summary>
    /// ‘최대 크기 + 오래된 생성일’ 기준으로 대표 경로를 재배치한다.
    /// </summary>
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

    /// <summary>
    /// 파일 크기를 안정적으로 읽고, 예외 시 sentinel 값으로 처리한다.
    /// </summary>
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

    /// <summary>
    /// 생성 시간을 안정적으로 조회한다.
    /// </summary>
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

    /// <summary>
    /// 수정 시간을 안정적으로 조회한다.
    /// </summary>
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

    /// <summary>
    /// union-find 기반으로 동일내용 연결성의 부모를 병합한다.
    /// </summary>
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

    /// <summary>
    /// 경로 루트를 탐색하고 경로 압축으로 후속 조회를 최적화한다.
    /// </summary>
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

    /// <summary>
    /// 플랫폼에 맞는 경로 비교 기준을 반환한다.
    /// </summary>
    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    /// <summary>
    /// 플랫폼에 맞는 경로 문자열 비교 옵션을 반환한다.
    /// </summary>
    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
