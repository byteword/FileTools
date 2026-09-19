using System.Text.RegularExpressions;

namespace FileTools;

internal enum FileCollectionNameMode { Contains, Equals, StartsWith, EndsWith, Wildcard }
internal sealed record FileCollectionQuery
{
    public IReadOnlyList<string> Kinds { get; init; } = [];
    public IReadOnlyList<string> Extensions { get; init; } = [];
    public string Name { get; init; } = "";
    public FileCollectionNameMode NameMode { get; init; }
    public bool IgnoreCase { get; init; } = true;
    public DateOnly? CreatedFrom { get; init; }
    public DateOnly? CreatedThrough { get; init; }
    public DateOnly? ModifiedFrom { get; init; }
    public DateOnly? ModifiedThrough { get; init; }
    public long? MinimumSize { get; init; }
    public long? MaximumSize { get; init; }

    public Func<FileCatalogEntry, bool> CreateMatcher(TimeZoneInfo? timeZone = null)
    {
        if (MinimumSize < 0 || MaximumSize < 0 || MinimumSize > MaximumSize)
            throw new ArgumentException(Localizer.Get("CollectionInvalidSize"));
        if (CreatedFrom > CreatedThrough || ModifiedFrom > ModifiedThrough)
            throw new ArgumentException(Localizer.Get("CollectionInvalidDates"));
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in Extensions)
        {
            var extension = AutoRelocationFileTypeClassifier.NormalizeExtension(text);
            if (extension.Length == 0) throw new ArgumentException(Localizer.Format("CollectionInvalidExtension", text));
            extensions.Add(extension);
        }
        var kinds = Kinds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var zone = timeZone ?? TimeZoneInfo.Local;
        var createdFrom = Start(CreatedFrom); var createdUntil = End(CreatedThrough);
        var modifiedFrom = Start(ModifiedFrom); var modifiedUntil = End(ModifiedThrough);
        var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var wildcard = NameMode == FileCollectionNameMode.Wildcard && Name.Length > 0
            ? new Regex("\\A" + Regex.Escape(Name).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + "\\z",
                RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | (IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None), TimeSpan.FromMilliseconds(250))
            : null;
        return entry =>
            (kinds.Count == 0 || kinds.Contains(entry.Kind)) &&
            (extensions.Count == 0 || extensions.Contains(entry.Extension)) &&
            (!MinimumSize.HasValue || entry.Size >= MinimumSize.Value) &&
            (!MaximumSize.HasValue || entry.Size <= MaximumSize.Value) &&
            InRange(entry.CreatedUtc, createdFrom, createdUntil) &&
            InRange(entry.ModifiedUtc, modifiedFrom, modifiedUntil) &&
            (Name.Length == 0 || NameMode switch
            {
                FileCollectionNameMode.Equals => entry.Name.Equals(Name, comparison),
                FileCollectionNameMode.StartsWith => entry.Name.StartsWith(Name, comparison),
                FileCollectionNameMode.EndsWith => entry.Name.EndsWith(Name, comparison),
                FileCollectionNameMode.Wildcard => wildcard!.IsMatch(entry.Name),
                _ => entry.Name.Contains(Name, comparison)
            });

        DateTimeOffset? Start(DateOnly? date) => date.HasValue ? MidnightUtc(date.Value, zone) : null;
        DateTimeOffset? End(DateOnly? date) => date.HasValue && date.Value != DateOnly.MaxValue ? MidnightUtc(date.Value.AddDays(1), zone) : null;
    }

    private static bool InRange(DateTimeOffset value, DateTimeOffset? from, DateTimeOffset? until) =>
        (!from.HasValue || value >= from.Value) && (!until.HasValue || value < until.Value);

    private static DateTimeOffset MidnightUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // 자정에 시각이 건너뛰는 지역은 해당 날짜의 첫 유효 시각을 사용한다.
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
