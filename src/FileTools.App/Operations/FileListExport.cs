using System.Globalization;
using System.Text;

namespace FileTools;

internal enum FileListFormat { Csv, Text }
internal enum FileListTextField { FullPath, RelativePath, Name }
internal enum FileListColumn { Name, Extension, Kind, FullPath, RootPath, RelativePath, Size, Created, Modified }
internal sealed record FileListExportOptions
{
    public FileListFormat Format { get; init; }
    public IReadOnlyList<FileListColumn> Columns { get; init; } = Enum.GetValues<FileListColumn>();
    public FileListTextField TextField { get; init; }
}
internal sealed record FileListSaveResult(int Written, int ExcludedOutput);
internal sealed record FileListDestinationReview(string Path, RenameFileSnapshot? Snapshot)
{
    public static FileListDestinationReview Capture(string path)
    {
        var fullPath = FileOperationPathGuard.Normalize(path);
        return new(fullPath, BatchRenamePlanBuilder.PathOccupied(fullPath) ? RenameFileSnapshot.Capture(fullPath) : null);
    }
}

internal static class FileListExport
{
    /// <summary>같은 폴더의 임시 파일에 작성하고 완료 후 원자적으로 확정한다.</summary>
    public static FileListSaveResult Save(IEnumerable<FileCatalogEntry> entries, string outputPath,
        FileListExportOptions options, bool overwrite = false, CancellationToken cancellationToken = default,
        IProgress<int>? progress = null, FileListDestinationReview? destinationReview = null)
    {
        if (options.Format == FileListFormat.Csv && options.Columns.Count == 0)
            throw new ArgumentException(Localizer.Get("FileListChooseColumns"));
        cancellationToken.ThrowIfCancellationRequested();
        var output = FileOperationPathGuard.Normalize(outputPath);
        var parent = Path.GetDirectoryName(output)!;
        FileOperationPathGuard.EnsureNoLinkedDirectory(parent);
        RenameFileSnapshot? previous = null;
        if (BatchRenamePlanBuilder.PathOccupied(output))
        {
            if (!overwrite) throw new IOException(Localizer.Get("FileListOutputExists"));
            previous = RenameFileSnapshot.Capture(output);
        }
        if (destinationReview is not null && (!FileOperationPathGuard.Comparer.Equals(output, destinationReview.Path) || previous != destinationReview.Snapshot))
            throw new IOException(Localizer.Get("FileListOutputChanged"));
        var temporary = Path.Combine(parent, ".filetools-list-" + Guid.NewGuid().ToString("N") + ".tmp");
        var written = 0;
        var excluded = 0;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 65536, leaveOpen: true))
                {
                    writer.NewLine = "\r\n";
                    if (options.Format == FileListFormat.Csv)
                        writer.WriteLine(string.Join(",", options.Columns.Select(column => CsvText(Localizer.Get("CatalogColumn" + column)))));
                    foreach (var entry in entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (FileOperationPathGuard.Comparer.Equals(FileOperationPathGuard.Normalize(entry.FullPath), output)) { excluded++; continue; }
                        writer.WriteLine(options.Format == FileListFormat.Csv
                            ? string.Join(",", options.Columns.Select(column => column == FileListColumn.Size
                                ? entry.Size.ToString(CultureInfo.InvariantCulture) : CsvText(Value(entry, column))))
                            : options.TextField switch { FileListTextField.Name => entry.Name, FileListTextField.RelativePath => entry.RelativePath, _ => entry.FullPath });
                        written++;
                        if (written % 128 == 0) progress?.Report(written);
                    }
                }
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            FileOperationPathGuard.EnsureNoLinkedDirectory(parent);
            if (previous is not null)
            {
                if (RenameFileSnapshot.Capture(output) != previous) throw new IOException(Localizer.Get("FileListOutputChanged"));
                File.Replace(temporary, output, destinationBackupFileName: null);
            }
            else File.Move(temporary, output, overwrite: false);
            return new(written, excluded);
        }
        finally
        {
            // 이 메서드가 만든 무작위 이름의 임시 파일만 정리한다.
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static string CsvText(string text)
    {
        var trimmed = text.AsSpan().TrimStart();
        if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0]) || text.Length > 0 && text[0] is '\t' or '\r' or '\n') text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    internal static string Value(FileCatalogEntry entry, FileListColumn column) => column switch
    {
        FileListColumn.Name => entry.Name,
        FileListColumn.Extension => entry.Extension,
        FileListColumn.Kind => entry.Kind,
        FileListColumn.FullPath => entry.FullPath,
        FileListColumn.RootPath => entry.RootPath,
        FileListColumn.RelativePath => entry.RelativePath,
        FileListColumn.Size => entry.Size.ToString(CultureInfo.InvariantCulture),
        FileListColumn.Created => entry.CreatedUtc.ToString("O", CultureInfo.InvariantCulture),
        FileListColumn.Modified => entry.ModifiedUtc.ToString("O", CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(column))
    };
}
