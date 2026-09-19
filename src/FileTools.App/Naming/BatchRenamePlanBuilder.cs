using System.Globalization;

namespace FileTools;

internal enum BatchRenameOrder { NaturalName, Selection, ModifiedTime, CreatedTime }
internal enum BatchRenameStatus { Ready, Unchanged, Excluded, Blocked }
internal sealed record BatchRenameOptions
{
    public string Find { get; init; } = "";
    public string ReplaceWith { get; init; } = "";
    public string RemovePrefix { get; init; } = "";
    public string RemoveSuffix { get; init; } = "";
    public string Prefix { get; init; } = "";
    public string Suffix { get; init; } = "";
    public string Pattern { get; init; } = "{Stem}";
    public bool PreserveExtension { get; init; } = true;
    public bool IgnoreCase { get; init; } = true;
    public int Start { get; init; } = 1;
    public int Increment { get; init; } = 1;
    public int Digits { get; init; } = 3;
    public BatchRenameOrder Order { get; init; }
}

internal sealed record BatchRenamePreview(string OriginalPath, string TargetPath, int? Number,
    BatchRenameStatus Status, string Detail, RenameFileSnapshot? Snapshot);

/// <summary>설정·선택 순서와 확정 결과를 함께 보존한다. 입력이 바뀌면 새 계획을 만든다.</summary>
internal sealed record BatchRenamePlan(string Id, BatchRenameOptions Options, IReadOnlyList<string> SourcePaths,
    IReadOnlyList<string> ExcludedPaths, IReadOnlyList<BatchRenamePreview> Rows);

internal static class BatchRenamePlanBuilder
{
    public static BatchRenamePlan Build(IEnumerable<string> paths, BatchRenameOptions options,
        IEnumerable<string>? excludedPaths = null, CancellationToken cancellationToken = default)
    {
        var sources = paths.Select(FileOperationPathGuard.Normalize).Distinct(FileOperationPathGuard.Comparer).ToArray();
        var excluded = (excludedPaths ?? []).ToHashSet(FileOperationPathGuard.Comparer);
        var snapshots = new Dictionary<string, RenameFileSnapshot>(FileOperationPathGuard.Comparer);
        var errors = new Dictionary<string, string>(FileOperationPathGuard.Comparer);
        foreach (var path in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (excluded.Contains(path)) continue;
            try { snapshots[path] = RenameFileSnapshot.Capture(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { errors[path] = ex.Message; }
        }
        IEnumerable<string> ordered = options.Order switch
        {
            BatchRenameOrder.NaturalName => sources.OrderBy(Path.GetFileName, NaturalFileNameComparer.Instance)
                .ThenBy(static path => path, FileOperationPathGuard.Comparer),
            BatchRenameOrder.ModifiedTime => sources.OrderBy(path => snapshots.TryGetValue(path, out var snapshot) ? snapshot.LastWriteTime : ulong.MaxValue)
                .ThenBy(static path => path, FileOperationPathGuard.Comparer),
            BatchRenameOrder.CreatedTime => sources.OrderBy(path => snapshots.TryGetValue(path, out var snapshot) ? snapshot.CreationTime : ulong.MaxValue)
                .ThenBy(static path => path, FileOperationPathGuard.Comparer),
            _ => sources
        };
        var rows = new List<BatchRenamePreview>();
        var position = 0;
        var resolver = new NameTemplateResolver([new BatchTokenProvider(options.Digits)]);
        foreach (var path in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (excluded.Contains(path))
            {
                rows.Add(new(path, path, null, BatchRenameStatus.Excluded, "", null));
                continue;
            }
            int? number = null;
            var targetPath = path;
            snapshots.TryGetValue(path, out var snapshot);
            try
            {
                if (options.Start < 0 || options.Increment < 1 || options.Digits is < 1 or > 10)
                    throw new IOException(Localizer.Get("BatchRenameInvalidNumbers"));
                number = checked(options.Start + checked(position * options.Increment));
                position++;
                if (errors.TryGetValue(path, out var error)) throw new IOException(error);
                if (string.IsNullOrWhiteSpace(options.Pattern)) throw new IOException(Localizer.Get("BatchRenameInvalidPattern"));
                var name = Path.GetFileName(path);
                var stem = options.PreserveExtension ? Path.GetFileNameWithoutExtension(name) : name;
                var extension = options.PreserveExtension ? Path.GetExtension(name) : "";
                var comparison = options.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (options.RemovePrefix.Length > 0 && stem.StartsWith(options.RemovePrefix, comparison)) stem = stem[options.RemovePrefix.Length..];
                if (options.RemoveSuffix.Length > 0 && stem.EndsWith(options.RemoveSuffix, comparison)) stem = stem[..^options.RemoveSuffix.Length];
                if (options.Find.Length > 0) stem = stem.Replace(options.Find, options.ReplaceWith, comparison);
                stem = options.Prefix + stem + options.Suffix;
                var evaluated = resolver.Evaluate(options.Pattern,
                    NameTemplateContext.FromNameParts(name, stem, extension) with { Index = number });
                if (!evaluated.IsReady) throw new IOException(Localizer.Get("BatchRenameInvalidPattern"));
                var newName = evaluated.Value + extension;
                if (!IsValidName(newName)) throw new IOException(Localizer.Get("RenameInvalidNameMessage"));
                targetPath = Path.Combine(Path.GetDirectoryName(path)!, newName);
                var unchanged = string.Equals(path, targetPath, StringComparison.Ordinal);
                if (!FileOperationPathGuard.Comparer.Equals(path, targetPath) && PathOccupied(targetPath))
                    throw new IOException(Localizer.Format("PlanPreviewTargetExistsFormat", targetPath));
                rows.Add(new(path, targetPath, number, unchanged ? BatchRenameStatus.Unchanged : BatchRenameStatus.Ready, "", snapshot));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OverflowException or FormatException)
            {
                rows.Add(new(path, targetPath, number, BatchRenameStatus.Blocked, ex is OverflowException ? Localizer.Get("BatchRenameInvalidNumbers") : ex.Message, snapshot));
            }
        }
        var collisions = rows.Where(static row => row.Status is BatchRenameStatus.Ready or BatchRenameStatus.Unchanged)
            .GroupBy(static row => row.TargetPath, FileOperationPathGuard.Comparer).Where(static group => group.Count() > 1)
            .Select(static group => group.Key).ToHashSet(FileOperationPathGuard.Comparer);
        for (var i = 0; i < rows.Count; i++)
            if (rows[i].Status == BatchRenameStatus.Ready && collisions.Contains(rows[i].TargetPath))
                rows[i] = rows[i] with { Status = BatchRenameStatus.Blocked, Detail = Localizer.Get("RenameDuplicateNameMessage") };
        return new(Guid.NewGuid().ToString("N"), options, sources, excluded.ToArray(), rows);
    }

    internal static bool IsValidName(string name)
    {
        if (name.Length is < 1 or > 255 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name is "." or ".." || !string.Equals(name, WindowsFileNameSafety.MakeSafeFileName(name), StringComparison.Ordinal)) return false;
        var device = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        return device is not ("CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$") &&
            !(device.Length == 4 && (device.StartsWith("COM", StringComparison.Ordinal) || device.StartsWith("LPT", StringComparison.Ordinal)) &&
              "123456789¹²³".Contains(device[3]));
    }

    internal static bool PathOccupied(string path)
    {
        try { File.GetAttributes(path); return true; }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return false; }
    }

    /// <summary>공용 이름 템플릿 파서를 사용하되 배치용 Stem/Index만 제공한다.</summary>
    private sealed class BatchTokenProvider(int digits) : INameTemplateTokenProvider
    {
        public bool TryResolve(NameTemplateToken token, NameTemplateContext context, out string value)
        {
            value = "";
            if (token.Name.Equals("Stem", StringComparison.OrdinalIgnoreCase) && token.Format is null)
            { value = context.Stem ?? ""; return true; }
            if (!token.Name.Equals("Index", StringComparison.OrdinalIgnoreCase) || context.Index is null) return false;
            var format = token.Format;
            if (format is not null && (format.Length is < 1 or > 10 || format.Any(static ch => ch != '0'))) return false;
            value = context.Index.Value.ToString(format ?? "D" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return true;
        }
    }
}

/// <summary>숫자 문자열을 정수로 변환하지 않아 큰 번호도 넘침 없이 자연 정렬한다.</summary>
internal sealed class NaturalFileNameComparer : IComparer<string?>
{
    public static NaturalFileNameComparer Instance { get; } = new();
    public int Compare(string? left, string? right)
    {
        left ??= ""; right ??= "";
        var i = 0; var j = 0;
        while (i < left.Length && j < right.Length)
        {
            if (char.IsAsciiDigit(left[i]) && char.IsAsciiDigit(right[j]))
            {
                var a = i; var b = j;
                while (i < left.Length && char.IsAsciiDigit(left[i])) i++;
                while (j < right.Length && char.IsAsciiDigit(right[j])) j++;
                var x = a; var y = b;
                while (x < i - 1 && left[x] == '0') x++;
                while (y < j - 1 && right[y] == '0') y++;
                var comparison = (i - x).CompareTo(j - y);
                if (comparison == 0) comparison = string.Compare(left, x, right, y, i - x, StringComparison.Ordinal);
                if (comparison == 0) comparison = (i - a).CompareTo(j - b);
                if (comparison != 0) return comparison;
            }
            else
            {
                var comparison = char.ToUpperInvariant(left[i++]).CompareTo(char.ToUpperInvariant(right[j++]));
                if (comparison != 0) return comparison;
            }
        }
        return (left.Length - i).CompareTo(right.Length - j);
    }
}
