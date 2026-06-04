using System.Buffers.Binary;
using System.Text;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;

namespace FileTools.Tests;

internal sealed record TestZipEntry(
    string Name,
    string Content = "",
    DateTime? LastModified = null,
    int ExternalAttributes = 0,
    string? Comment = null,
    bool IsDirectory = false);

internal sealed record ZipEntrySnapshot(
    string Name,
    bool IsDirectory,
    string Content,
    DateTime LastModified,
    int ExternalAttributes,
    string? Comment);

internal static class ZipTestData
{
    public static void CreateStoredZip(string path, params TestZipEntry[] entries)
    {
        CreateZip(path, CompressionMethod.Stored, entries);
    }

    public static void CreateDeflatedZip(string path, params TestZipEntry[] entries)
    {
        CreateZip(path, CompressionMethod.Deflated, entries);
    }

    private static void CreateZip(string path, CompressionMethod compressionMethod, params TestZipEntry[] entries)
    {
        using var file = File.Create(path);
        using var zip = new ZipOutputStream(file)
        {
            IsStreamOwner = false
        };
        zip.SetLevel(compressionMethod == CompressionMethod.Stored ? 0 : 6);

        foreach (var testEntry in entries)
        {
            if (testEntry.IsDirectory)
            {
                WriteDirectory(zip, testEntry);
                continue;
            }

            WriteFile(zip, testEntry, compressionMethod);
        }

        zip.Finish();
    }

    public static IReadOnlyDictionary<string, ZipEntrySnapshot> ReadEntries(string path)
    {
        using var file = File.OpenRead(path);
        using var zip = new ZipFile(file);
        var entries = new Dictionary<string, ZipEntrySnapshot>(StringComparer.Ordinal);

        foreach (ZipEntry entry in zip)
        {
            var content = "";
            if (!entry.IsDirectory)
            {
                using var input = zip.GetInputStream(entry);
                using var memory = new MemoryStream();
                input.CopyTo(memory);
                content = Encoding.UTF8.GetString(memory.ToArray());
            }

            entries[entry.Name] = new ZipEntrySnapshot(
                entry.Name,
                entry.IsDirectory,
                content,
                entry.DateTime,
                entry.ExternalFileAttributes,
                entry.Comment);
        }

        return entries;
    }

    public static void TruncateEnd(string path, int bytesToRemove)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytesToRemove <= 0 || bytesToRemove >= bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesToRemove));
        }

        File.WriteAllBytes(path, bytes[..^bytesToRemove]);
    }

    public static void CorruptLocalFilePayload(string path, string entryName)
    {
        var bytes = File.ReadAllBytes(path);
        var payload = FindPayloadFromCentralDirectory(bytes, entryName)
            ?? FindPayloadFromLocalHeaders(bytes, entryName)
            ?? throw new InvalidDataException("ZIP entry was not found: " + entryName);
        if (payload.CompressedSize == 0)
        {
            throw new InvalidDataException("Cannot corrupt an empty ZIP payload.");
        }

        bytes[payload.PayloadStart + payload.CompressedSize / 2] ^= 0x5A;
        File.WriteAllBytes(path, bytes);
    }

    public static void MakeEntryUseUnsupportedCompressionMethod(string path, string entryName)
    {
        var bytes = File.ReadAllBytes(path);
        var location = FindCentralDirectoryLocation(bytes, entryName)
            ?? throw new InvalidDataException("ZIP entry was not found: " + entryName);

        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(location.CentralDirectoryOffset + 10, 2), 99);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(location.LocalHeaderOffset + 8, 2), 99);
        File.WriteAllBytes(path, bytes);
    }

    private static (int PayloadStart, int CompressedSize)? FindPayloadFromCentralDirectory(byte[] bytes, string entryName)
    {
        var location = FindCentralDirectoryLocation(bytes, entryName);
        if (location is null)
        {
            return null;
        }

        var payloadStart = GetLocalPayloadStart(bytes, location.Value.LocalHeaderOffset);
        if (payloadStart + location.Value.CompressedSize > bytes.Length)
        {
            throw new InvalidDataException("ZIP central directory payload extends past the end of the file.");
        }

        return (payloadStart, location.Value.CompressedSize);
    }

    private static (int CentralDirectoryOffset, int LocalHeaderOffset, int CompressedSize)? FindCentralDirectoryLocation(
        byte[] bytes,
        string entryName)
    {
        var offset = 0;
        while (offset + 46 < bytes.Length)
        {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
            if (signature != 0x02014b50)
            {
                offset++;
                continue;
            }

            var compressedSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 20, 4));
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28, 2));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 30, 2));
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 32, 2));
            var localHeaderOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 42, 4));
            var nameStart = offset + 46;
            var nextOffset = nameStart + nameLength + extraLength + commentLength;
            if (nextOffset > bytes.Length)
            {
                return null;
            }

            var name = Encoding.UTF8.GetString(bytes, nameStart, nameLength);
            if (string.Equals(name, entryName, StringComparison.Ordinal))
            {
                if (localHeaderOffset < 0 || localHeaderOffset + 30 > bytes.Length)
                {
                    throw new InvalidDataException("ZIP local file header offset is invalid.");
                }

                _ = GetLocalPayloadStart(bytes, localHeaderOffset);
                return (offset, localHeaderOffset, compressedSize);
            }

            offset = nextOffset;
        }

        return null;
    }

    private static int GetLocalPayloadStart(byte[] bytes, int localHeaderOffset)
    {
        var localSignature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(localHeaderOffset, 4));
        if (localSignature != 0x04034b50)
        {
            throw new InvalidDataException("ZIP local file header signature is invalid.");
        }

        var localNameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(localHeaderOffset + 26, 2));
        var localExtraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(localHeaderOffset + 28, 2));
        return localHeaderOffset + 30 + localNameLength + localExtraLength;
    }

    private static (int PayloadStart, int CompressedSize)? FindPayloadFromLocalHeaders(byte[] bytes, string entryName)
    {
        var offset = 0;
        while (offset + 30 < bytes.Length)
        {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
            if (signature != 0x04034b50)
            {
                offset++;
                continue;
            }

            var compressedSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 18, 4));
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 26, 2));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28, 2));
            var nameStart = offset + 30;
            var payloadStart = nameStart + nameLength + extraLength;
            var payloadEnd = payloadStart + compressedSize;
            if (payloadEnd > bytes.Length)
            {
                throw new InvalidDataException("ZIP local file payload extends past the end of the file.");
            }

            var name = Encoding.UTF8.GetString(bytes, nameStart, nameLength);
            if (string.Equals(name, entryName, StringComparison.Ordinal))
            {
                return (payloadStart, compressedSize);
            }

            offset = payloadEnd;
        }

        return null;
    }

    private static void WriteDirectory(ZipOutputStream zip, TestZipEntry testEntry)
    {
        var entry = new ZipEntry(testEntry.Name.TrimEnd('/') + "/")
        {
            DateTime = testEntry.LastModified ?? DateTime.Now,
            ExternalFileAttributes = testEntry.ExternalAttributes == 0 ? 0x10 : testEntry.ExternalAttributes,
            IsUnicodeText = true
        };
        if (!string.IsNullOrWhiteSpace(testEntry.Comment))
        {
            entry.Comment = testEntry.Comment;
        }

        zip.PutNextEntry(entry);
        zip.CloseEntry();
    }

    private static void WriteFile(ZipOutputStream zip, TestZipEntry testEntry, CompressionMethod compressionMethod)
    {
        var content = Encoding.UTF8.GetBytes(testEntry.Content);
        var crc = new Crc32();
        crc.Update(content);
        var entry = new ZipEntry(testEntry.Name.Replace('\\', '/'))
        {
            DateTime = testEntry.LastModified ?? DateTime.Now,
            Size = content.Length,
            Crc = crc.Value,
            CompressionMethod = compressionMethod,
            ExternalFileAttributes = testEntry.ExternalAttributes,
            IsUnicodeText = true
        };
        if (!string.IsNullOrWhiteSpace(testEntry.Comment))
        {
            entry.Comment = testEntry.Comment;
        }

        zip.PutNextEntry(entry);
        zip.Write(content, 0, content.Length);
        zip.CloseEntry();
    }
}
