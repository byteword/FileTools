using FileTools;

// Compare the native policy's exported matrix to the actual context-command entry point.
// The oracle is the resulting directory tree and file contents, not another visibility table.
internal static class MenuContractTests
{
    private static readonly (int Bit, ContextMenuCommand Command)[] Commands =
    [
        (1, ContextMenuCommand.FolderUnwrapSameNameSingleFile),
        (2, ContextMenuCommand.FolderUnwrapKeepFileName),
        (4, ContextMenuCommand.FolderUnwrapUseFolderName),
        (8, ContextMenuCommand.FolderUnwrapPrefixFolderName),
        (16, ContextMenuCommand.FolderMoveInnerFilesUp)
    ];
    private static int _runs;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected native policy matrix CSV path.");
        var outcomes = new Dictionary<(int Kinds, int Skip, int Bit), (string Before, string After)>();
        // Empty, case-only name differences, extensionless files and custom mismatch settings
        // are included along with regular names. Collision and no-collision results stay paired.
        for (var kinds = 0; kinds < 16; kinds++)
        for (var skip = 0; skip < 2; skip++)
        foreach (var (bit, command) in Commands)
        {
            var results = (from collision in new[] { false, true }
                           from variant in new[] { 0, 1, 2 }
                           select Run(kinds, skip, command, collision, variant)).ToArray();
            outcomes[(kinds, skip, bit)] = (string.Join("\n---\n", results.Select(x => x.Before)),
                string.Join("\n---\n", results.Select(x => x.After)));
        }

        var rows = 0;
        foreach (var line in File.ReadLines(args[0]).Skip(1))
        {
            var values = line.Split(',').Select(int.Parse).ToArray();
            var (kinds, enabled, skip, visible) = (values[0], values[1], values[2], values[3]);
            var retained = Commands.Where(x => (visible & x.Bit) != 0).ToArray();
            foreach (var (bit, _) in Commands.Where(x => (enabled & x.Bit) != 0))
            {
                var outcome = outcomes[(kinds, skip, bit)];
                if (outcome.Before == outcome.After)
                {
                    Require((visible & bit) == 0, $"No-op command retained: {line}, command={bit}");
                    continue;
                }
                if ((visible & bit) == 0)
                    Require(retained.Any(x => outcomes[(kinds, skip, x.Bit)].After == outcome.After),
                        $"Hidden command has no equivalent execution: {line}, command={bit}");
            }
            for (var a = 0; a < retained.Length; a++)
            for (var b = a + 1; b < retained.Length; b++)
                Require(outcomes[(kinds, skip, retained[a].Bit)].After != outcomes[(kinds, skip, retained[b].Bit)].After,
                    $"Duplicate commands retained: {line}");
            rows++;
        }
        Require(rows == 1024, "Incomplete native matrix.");
        ValidateChangedSelection();
        Console.WriteLine($"Passed {rows} native/managed policy combinations against {_runs} actual executions; changed-selection revalidation passed.");
    }

    private static (string Before, string After) Run(int kinds, int skip, ContextMenuCommand command, bool collision, int variant)
    {
        using var fixture = new Fixture();
        var paths = new List<string>();
        var extension = variant == 2 ? "" : ".txt";
        if ((kinds & 1) != 0)
            paths.Add(fixture.Folder("Same", (variant == 1 ? "sAmE" : "Same") + extension, "same"));
        if ((kinds & 2) != 0)
            paths.Add(fixture.Folder("Different", "Original" + extension, "different"));
        if ((kinds & 4) != 0)
        {
            paths.Add(fixture.Folder("Many", "One.txt", "one"));
            File.WriteAllText(Path.Combine(fixture.Root, "Many", "Two.txt"), "two");
            Directory.CreateDirectory(Path.Combine(fixture.Root, "Many", "Nested"));
        }
        if ((kinds & 8) != 0) paths.Add(fixture.Folder("Empty"));
        if (collision)
        {
            foreach (var name in new[] { "Same" + extension, "Original" + extension,
                "Different" + extension, "Different-Original" + extension, "One.txt" })
            {
                var target = Path.Combine(fixture.Root, name);
                if (!Directory.Exists(target)) File.WriteAllText(target, "existing");
            }
        }
        var settings = new FileToolsSettings
        {
            FolderStructureConflictPolicy = skip == 1 ? NameCollisionPolicy.Skip : NameCollisionPolicy.AutoNumber,
            FolderUnwrapNameMismatchMode = FolderUnwrapNameMismatchMode.CustomTemplate,
            FolderUnwrapMismatchFileNameTemplate = "CUSTOM-{FileName}"
        };
        var before = Snapshot(fixture.Root);
        var result = Program.ExecuteContextCommand(command, paths, settings)!;
        Require(result.Errors.Count == 0, string.Join("\n", result.Errors));
        _runs++;
        return (before, Snapshot(fixture.Root));
    }

    private static void ValidateChangedSelection()
    {
        using var fixture = new Fixture();
        var path = fixture.Folder("Same", "Same.txt", "same");
        var command = ContextMenuCommand.FolderUnwrapSameNameSingleFile;
        Require(Program.FilterContextCommandPaths(command, [path]).Length == 1, "Initial eligibility");
        File.WriteAllText(Path.Combine(path, "Added.txt"), "added after menu opened");
        var before = Snapshot(fixture.Root);
        Program.ExecuteContextCommand(command, [path], new FileToolsSettings());
        Require(before == Snapshot(fixture.Root), "Changed folder must not be unwrapped as a single file");
        File.Delete(Path.Combine(path, "Added.txt"));
        File.Delete(Path.Combine(path, "Same.txt"));
        Require(Program.FilterContextCommandPaths(command, [path]).Length == 0, "Deleted contents revalidated");
        foreach (var policy in new[] { NameCollisionPolicy.Ask, NameCollisionPolicy.MergeIntoExisting })
            Require(FolderStructureCollisionOptions.Create(new FileToolsSettings { FolderStructureConflictPolicy = policy },
                NameCollisionTargetKind.File).Policy == NameCollisionPolicy.Skip, "Registry collision policy normalization");
    }

    private static string Snapshot(string root) => string.Join("\n",
        Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path) + (Directory.Exists(path) ? "/" : "=" + File.ReadAllText(path)))
            .Order(StringComparer.Ordinal));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "FileTools.MenuContract-" + Guid.NewGuid().ToString("N"));
        public Fixture() => Directory.CreateDirectory(Root);
        public string Folder(string name, string? file = null, string content = "")
        {
            var path = Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            if (file != null) File.WriteAllText(Path.Combine(path, file), content);
            return path;
        }
        public void Dispose()
        {
            Require(Path.GetDirectoryName(Path.GetFullPath(Root)) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()) &&
                Path.GetFileName(Root).StartsWith("FileTools.MenuContract-", StringComparison.Ordinal), "Unsafe fixture cleanup");
            Directory.Delete(Root, recursive: true);
        }
    }
}
