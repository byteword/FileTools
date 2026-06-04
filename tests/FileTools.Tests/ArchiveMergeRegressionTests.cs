using System.Text;
using FileTools;

namespace FileTools.Tests;

public sealed class ArchiveMergeRegressionTests
{
    [Fact]
    public void Merge_PreservesZipEntryMetadataAndDirectoryEntries()
    {
        using var temp = TempDirectory.Create();
        var sourceA = temp.GetPath("source-a.zip");
        var sourceB = temp.GetPath("source-b.zip");
        var output = temp.GetPath("merged.zip");
        var expectedModified = new DateTime(2024, 3, 5, 6, 7, 8, DateTimeKind.Unspecified);

        ZipTestData.CreateStoredZip(
            sourceA,
            new TestZipEntry("folder/", IsDirectory: true, LastModified: expectedModified, ExternalAttributes: 0x10),
            new TestZipEntry(
                "folder/file.txt",
                "alpha",
                expectedModified,
                ExternalAttributes: 0x20,
                Comment: "file metadata comment"));
        ZipTestData.CreateStoredZip(
            sourceB,
            new TestZipEntry("other.txt", "bravo", expectedModified.AddMinutes(1), ExternalAttributes: 0x20));

        var result = Merge([sourceA, sourceB], output);

        Assert.Empty(result.Errors);
        Assert.True(File.Exists(output), string.Join(Environment.NewLine, result.Messages));
        var entries = ZipTestData.ReadEntries(output);
        Assert.True(entries["folder/"].IsDirectory);
        Assert.Equal("alpha", entries["folder/file.txt"].Content);
        Assert.Equal(0x20, entries["folder/file.txt"].ExternalAttributes);
        Assert.Equal("file metadata comment", entries["folder/file.txt"].Comment);
        Assert.InRange(
            Math.Abs((entries["folder/file.txt"].LastModified - expectedModified).TotalSeconds),
            0,
            3);
        Assert.Equal("bravo", entries["other.txt"].Content);
    }

    [Fact]
    public void Merge_SkipFailedArchive_ContinuesWithReadableSources()
    {
        using var temp = TempDirectory.Create();
        var unreadable = temp.GetPath("unreadable.zip");
        var readable = temp.GetPath("readable.zip");
        var output = temp.GetPath("merged.zip");

        ZipTestData.CreateStoredZip(unreadable, new TestZipEntry("broken-source.txt", "bad"));
        ZipTestData.TruncateEnd(unreadable, bytesToRemove: 22);
        ZipTestData.CreateStoredZip(readable, new TestZipEntry("readable.txt", "ok"));

        var result = Merge(
            [unreadable, readable],
            output,
            failurePolicy: ArchiveMergeFailurePolicy.SkipFailedArchive);

        Assert.NotEmpty(result.Errors);
        Assert.True(File.Exists(output), string.Join(Environment.NewLine, result.Errors));
        var entries = ZipTestData.ReadEntries(output);
        Assert.False(entries.ContainsKey("broken-source.txt"));
        Assert.Equal("ok", entries["readable.txt"].Content);
    }

    [Fact]
    public void Merge_SkipFailedEntry_WritesReadableEntriesFromCorruptArchive()
    {
        using var temp = TempDirectory.Create();
        var partiallyCorrupt = temp.GetPath("partially-corrupt.zip");
        var readable = temp.GetPath("readable.zip");
        var output = temp.GetPath("merged.zip");

        ZipTestData.CreateDeflatedZip(
            partiallyCorrupt,
            new TestZipEntry("healthy.txt", "healthy"),
            new TestZipEntry("corrupt.txt", "this payload will be damaged"));
        ZipTestData.MakeEntryUseUnsupportedCompressionMethod(partiallyCorrupt, "corrupt.txt");
        ZipTestData.CreateStoredZip(readable, new TestZipEntry("readable.txt", "ok"));

        var result = Merge(
            [partiallyCorrupt, readable],
            output,
            failurePolicy: ArchiveMergeFailurePolicy.SkipFailedEntry);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains(result.Messages, static message => message.Contains("corrupt.txt", StringComparison.Ordinal));
        var entries = ZipTestData.ReadEntries(output);
        Assert.Equal("healthy", entries["healthy.txt"].Content);
        Assert.Equal("ok", entries["readable.txt"].Content);
        Assert.False(entries.ContainsKey("corrupt.txt"));
    }

    [Fact]
    public void Merge_OutputParentIsFile_ReportsErrorWithoutCreatingOutput()
    {
        using var temp = TempDirectory.Create();
        var sourceA = temp.GetPath("source-a.zip");
        var sourceB = temp.GetPath("source-b.zip");
        var fileUsedAsParent = temp.GetPath("not-a-directory");
        var output = Path.Combine(fileUsedAsParent, "merged.zip");

        ZipTestData.CreateStoredZip(sourceA, new TestZipEntry("a.txt", "a"));
        ZipTestData.CreateStoredZip(sourceB, new TestZipEntry("b.txt", "b"));
        File.WriteAllText(fileUsedAsParent, "parent path is a file", Encoding.UTF8);

        var result = Merge([sourceA, sourceB], output);

        Assert.NotEmpty(result.Errors);
        Assert.True(File.Exists(fileUsedAsParent));
        Assert.False(Directory.Exists(fileUsedAsParent));
    }

    [Fact]
    public void Merge_FinalMoveFails_ReportsErrorAndDeletesTempArchive()
    {
        using var temp = TempDirectory.Create();
        var sourceA = temp.GetPath("source-a.zip");
        var sourceB = temp.GetPath("source-b.zip");
        var output = temp.GetPath("merged.zip");
        var fileSystem = new MoveFailureArchiveMergeFileSystem();

        ZipTestData.CreateStoredZip(sourceA, new TestZipEntry("a.txt", "a"));
        ZipTestData.CreateStoredZip(sourceB, new TestZipEntry("b.txt", "b"));

        var result = Merge([sourceA, sourceB], output, fileSystem: fileSystem);

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, static error => error.Contains("simulated final move failure", StringComparison.Ordinal));
        Assert.False(File.Exists(output));
        Assert.NotNull(fileSystem.TempPath);
        Assert.Contains(fileSystem.TempPath, fileSystem.DeletedPaths);
        Assert.False(File.Exists(fileSystem.TempPath));
    }

    private static OperationResult Merge(
        IReadOnlyList<string> sourcePaths,
        string outputPath,
        ArchiveMergeLayout layout = ArchiveMergeLayout.PreserveInternalPaths,
        ArchiveMergeCollisionPolicy collisionPolicy = ArchiveMergeCollisionPolicy.AutoNumber,
        ArchiveMergeDuplicatePolicy duplicatePolicy = ArchiveMergeDuplicatePolicy.KeepBoth,
        ArchiveMergeFailurePolicy failurePolicy = ArchiveMergeFailurePolicy.AbortAll,
        ArchiveMergeCompressionLevel compressionLevel = ArchiveMergeCompressionLevel.StoreOnly,
        IArchiveMergeFileSystem? fileSystem = null)
    {
        var options = new ArchiveMergeOptions
        {
            SourcePaths = sourcePaths.ToList(),
            OutputPath = outputPath,
            Layout = layout,
            CollisionPolicy = collisionPolicy,
            DuplicatePolicy = duplicatePolicy,
            FailurePolicy = failurePolicy,
            CompressionLevel = compressionLevel
        };

        return fileSystem is null
            ? ArchiveMergeOperations.Merge(options, CancellationToken.None)
            : ArchiveMergeOperations.Merge(options, CancellationToken.None, fileSystem);
    }

    private sealed class MoveFailureArchiveMergeFileSystem : IArchiveMergeFileSystem
    {
        private readonly IArchiveMergeFileSystem _inner = PhysicalArchiveMergeFileSystem.Instance;

        public string? TempPath { get; private set; }

        public List<string> DeletedPaths { get; } = [];

        public void CreateDirectory(string path)
        {
            _inner.CreateDirectory(path);
        }

        public bool FileExists(string path)
        {
            return _inner.FileExists(path);
        }

        public bool DirectoryExists(string path)
        {
            return _inner.DirectoryExists(path);
        }

        public string CreateTempArchivePath(string outputDirectory)
        {
            TempPath = Path.Combine(outputDirectory, ".FileTools.Tests.Merge.tmp.zip");
            return TempPath;
        }

        public void MoveFile(string sourcePath, string destinationPath)
        {
            throw new IOException("simulated final move failure");
        }

        public void DeleteFileIfExists(string path)
        {
            DeletedPaths.Add(path);
            _inner.DeleteFileIfExists(path);
        }
    }
}
