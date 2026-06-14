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
    bool IsDirectory = false,
    byte[]? LocalExtraData = null,
    byte[]? CentralDirectoryExtraData = null);

internal sealed record ZipEntrySnapshot(
    string Name,
    bool IsDirectory,
    string Content,
    DateTime LastModified,
    int ExternalAttributes,
    string? Comment);

internal sealed record ZipExtraFieldSnapshot(
    byte[] LocalHeader,
    byte[] CentralDirectory);

internal static class ZipTestData
{
    public static void CreateStoredZip(string path, params TestZipEntry[] entries)
    {
        CreateZip(path, CompressionMethod.Stored, archiveComment: null, entries);
    }

    public static void CreateStoredZipWithArchiveComment(string path, string archiveComment, params TestZipEntry[] entries)
    {
        CreateZip(path, CompressionMethod.Stored, archiveComment, entries);
    }

    public static void CreateDeflatedZip(string path, params TestZipEntry[] entries)
    {
        CreateZip(path, CompressionMethod.Deflated, archiveComment: null, entries);
    }

    public static void CreateLegacyStoredZip(string path, Encoding nameEncoding, params TestZipEntry[] entries)
    {
        CreateLegacyStoredZip(path, nameEncoding, archiveComment: null, entries);
    }

    public static void CreateLegacyStoredZip(
        string path,
        Encoding nameEncoding,
        string? archiveComment,
        params TestZipEntry[] entries)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var centralDirectory = new List<LegacyZipCentralEntry>();

        using (var file = File.Create(path))
        {
            foreach (var testEntry in entries)
            {
                var name = NormalizeEntryName(testEntry);
                var nameBytes = nameEncoding.GetBytes(name);
                var localExtraData = testEntry.LocalExtraData ?? testEntry.CentralDirectoryExtraData ?? [];
                var centralExtraData = testEntry.CentralDirectoryExtraData ?? testEntry.LocalExtraData ?? [];
                var commentBytes = string.IsNullOrWhiteSpace(testEntry.Comment)
                    ? []
                    : nameEncoding.GetBytes(testEntry.Comment);
                var content = testEntry.IsDirectory ? [] : Encoding.UTF8.GetBytes(testEntry.Content);
                var crc = new Crc32();
                crc.Update(content);
                var localHeaderOffset = checked((uint)file.Position);
                var (dosTime, dosDate) = ToDosTimestamp(testEntry.LastModified ?? DateTime.Now);

                WriteLocalHeader(
                    file,
                    nameBytes,
                    localExtraData,
                    crc.Value,
                    content.Length,
                    dosTime,
                    dosDate);
                file.Write(content);

                centralDirectory.Add(new LegacyZipCentralEntry(
                    nameBytes,
                    centralExtraData,
                    commentBytes,
                    crc.Value,
                    content.Length,
                    dosTime,
                    dosDate,
                    testEntry.IsDirectory
                        ? testEntry.ExternalAttributes == 0 ? 0x10 : testEntry.ExternalAttributes
                        : testEntry.ExternalAttributes,
                    localHeaderOffset));
            }

            var centralDirectoryOffset = checked((uint)file.Position);
            foreach (var entry in centralDirectory)
            {
                WriteCentralDirectoryHeader(file, entry);
            }

            var centralDirectorySize = checked((uint)(file.Position - centralDirectoryOffset));
            WriteEndOfCentralDirectory(
                file,
                centralDirectory.Count,
                centralDirectorySize,
                centralDirectoryOffset,
                string.IsNullOrWhiteSpace(archiveComment) ? [] : nameEncoding.GetBytes(archiveComment));
        }
    }

    private static void CreateZip(
        string path,
        CompressionMethod compressionMethod,
        string? archiveComment,
        params TestZipEntry[] entries)
    {
        using (var file = File.Create(path))
        using (var zip = new ZipOutputStream(file)
        {
            IsStreamOwner = false
        })
        {
            zip.SetLevel(compressionMethod == CompressionMethod.Stored ? 0 : 6);
            if (!string.IsNullOrWhiteSpace(archiveComment))
            {
                zip.SetComment(archiveComment);
            }

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

        PatchExtraFields(path, entries);
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

    public static ZipExtraFieldSnapshot ReadExtraFields(string path, string entryName)
    {
        var bytes = File.ReadAllBytes(path);
        var location = FindCentralDirectoryLocation(bytes, entryName)
            ?? throw new InvalidDataException("ZIP entry was not found: " + entryName);

        var centralExtraData = ReadCentralDirectoryExtraData(bytes, location.CentralDirectoryOffset);
        var localExtraData = ReadLocalExtraData(bytes, location.LocalHeaderOffset);
        return new ZipExtraFieldSnapshot(localExtraData, centralExtraData);
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

    private static byte[] ReadCentralDirectoryExtraData(byte[] bytes, int centralDirectoryOffset)
    {
        var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(centralDirectoryOffset, 4));
        if (signature != 0x02014b50)
        {
            throw new InvalidDataException("ZIP central directory signature is invalid.");
        }

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(centralDirectoryOffset + 28, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(centralDirectoryOffset + 30, 2));
        var extraStart = centralDirectoryOffset + 46 + nameLength;
        return CopyRange(bytes, extraStart, extraLength);
    }

    private static byte[] ReadLocalExtraData(byte[] bytes, int localHeaderOffset)
    {
        var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(localHeaderOffset, 4));
        if (signature != 0x04034b50)
        {
            throw new InvalidDataException("ZIP local file header signature is invalid.");
        }

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(localHeaderOffset + 26, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(localHeaderOffset + 28, 2));
        var extraStart = localHeaderOffset + 30 + nameLength;
        return CopyRange(bytes, extraStart, extraLength);
    }

    private static byte[] CopyRange(byte[] bytes, int start, int length)
    {
        if (start < 0 || length < 0 || start + length > bytes.Length)
        {
            throw new InvalidDataException("ZIP extra field extends past the end of the file.");
        }

        if (length == 0)
        {
            return [];
        }

        var result = new byte[length];
        Buffer.BlockCopy(bytes, start, result, 0, length);
        return result;
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

    private static void PatchExtraFields(string path, IReadOnlyList<TestZipEntry> entries)
    {
        if (!entries.Any(static entry => entry.LocalExtraData is not null || entry.CentralDirectoryExtraData is not null))
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        foreach (var entry in entries)
        {
            if (entry.LocalExtraData is null && entry.CentralDirectoryExtraData is null)
            {
                continue;
            }

            var entryName = entry.IsDirectory
                ? entry.Name.TrimEnd('/') + "/"
                : entry.Name.Replace('\\', '/');
            var location = FindCentralDirectoryLocation(bytes, entryName)
                ?? throw new InvalidDataException("ZIP entry was not found: " + entryName);

            if (entry.LocalExtraData is not null)
            {
                PatchLocalExtraData(bytes, location.LocalHeaderOffset, entry.LocalExtraData);
            }

            if (entry.CentralDirectoryExtraData is not null)
            {
                PatchCentralDirectoryExtraData(bytes, location.CentralDirectoryOffset, entry.CentralDirectoryExtraData);
            }
        }

        File.WriteAllBytes(path, bytes);
    }

    private static void PatchLocalExtraData(byte[] bytes, int localHeaderOffset, byte[] extraData)
    {
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(localHeaderOffset + 26, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(localHeaderOffset + 28, 2));
        if (extraLength != extraData.Length)
        {
            throw new InvalidDataException("Replacement local extra field must keep the original length.");
        }

        Buffer.BlockCopy(extraData, 0, bytes, localHeaderOffset + 30 + nameLength, extraData.Length);
    }

    private static void PatchCentralDirectoryExtraData(byte[] bytes, int centralDirectoryOffset, byte[] extraData)
    {
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(centralDirectoryOffset + 28, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(centralDirectoryOffset + 30, 2));
        if (extraLength != extraData.Length)
        {
            throw new InvalidDataException("Replacement central directory extra field must keep the original length.");
        }

        Buffer.BlockCopy(extraData, 0, bytes, centralDirectoryOffset + 46 + nameLength, extraData.Length);
    }

    private static void WriteDirectory(ZipOutputStream zip, TestZipEntry testEntry)
    {
        var entry = new ZipEntry(testEntry.Name.TrimEnd('/') + "/")
        {
            DateTime = testEntry.LastModified ?? DateTime.Now,
            ExternalFileAttributes = testEntry.ExternalAttributes == 0 ? 0x10 : testEntry.ExternalAttributes,
            IsUnicodeText = true
        };
        SetInitialExtraData(entry, testEntry);
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
        SetInitialExtraData(entry, testEntry);
        if (!string.IsNullOrWhiteSpace(testEntry.Comment))
        {
            entry.Comment = testEntry.Comment;
        }

        zip.PutNextEntry(entry);
        zip.Write(content, 0, content.Length);
        zip.CloseEntry();
    }

    private static void SetInitialExtraData(ZipEntry entry, TestZipEntry testEntry)
    {
        var extraData = testEntry.LocalExtraData ?? testEntry.CentralDirectoryExtraData;
        if (extraData is { Length: > 0 })
        {
            entry.ExtraData = extraData;
        }
    }

    private static string NormalizeEntryName(TestZipEntry entry)
    {
        return entry.IsDirectory
            ? entry.Name.Replace('\\', '/').TrimEnd('/') + "/"
            : entry.Name.Replace('\\', '/');
    }

    private static void WriteLocalHeader(
        Stream stream,
        byte[] nameBytes,
        byte[] extraData,
        long crc,
        int size,
        ushort dosTime,
        ushort dosDate)
    {
        WriteUInt32(stream, 0x04034b50);
        WriteUInt16(stream, 20);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, dosTime);
        WriteUInt16(stream, dosDate);
        WriteUInt32(stream, checked((uint)crc));
        WriteUInt32(stream, checked((uint)size));
        WriteUInt32(stream, checked((uint)size));
        WriteUInt16(stream, ToUInt16Length(nameBytes));
        WriteUInt16(stream, ToUInt16Length(extraData));
        stream.Write(nameBytes);
        stream.Write(extraData);
    }

    private static void WriteCentralDirectoryHeader(Stream stream, LegacyZipCentralEntry entry)
    {
        WriteUInt32(stream, 0x02014b50);
        WriteUInt16(stream, 0x0314);
        WriteUInt16(stream, 20);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, entry.DosTime);
        WriteUInt16(stream, entry.DosDate);
        WriteUInt32(stream, checked((uint)entry.Crc));
        WriteUInt32(stream, checked((uint)entry.Size));
        WriteUInt32(stream, checked((uint)entry.Size));
        WriteUInt16(stream, ToUInt16Length(entry.NameBytes));
        WriteUInt16(stream, ToUInt16Length(entry.ExtraData));
        WriteUInt16(stream, ToUInt16Length(entry.CommentBytes));
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt32(stream, checked((uint)entry.ExternalAttributes));
        WriteUInt32(stream, entry.LocalHeaderOffset);
        stream.Write(entry.NameBytes);
        stream.Write(entry.ExtraData);
        stream.Write(entry.CommentBytes);
    }

    private static void WriteEndOfCentralDirectory(
        Stream stream,
        int entryCount,
        uint centralDirectorySize,
        uint centralDirectoryOffset,
        byte[] commentBytes)
    {
        WriteUInt32(stream, 0x06054b50);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, ToUInt16Count(entryCount));
        WriteUInt16(stream, ToUInt16Count(entryCount));
        WriteUInt32(stream, centralDirectorySize);
        WriteUInt32(stream, centralDirectoryOffset);
        WriteUInt16(stream, ToUInt16Length(commentBytes));
        stream.Write(commentBytes);
    }

    private static (ushort Time, ushort Date) ToDosTimestamp(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        var year = Math.Clamp(local.Year, 1980, 2107);
        var dosDate = (ushort)(((year - 1980) << 9) | (local.Month << 5) | local.Day);
        var dosTime = (ushort)((local.Hour << 11) | (local.Minute << 5) | (local.Second / 2));
        return (dosTime, dosDate);
    }

    private static ushort ToUInt16Length(byte[] value)
    {
        return checked((ushort)value.Length);
    }

    private static ushort ToUInt16Count(int value)
    {
        return checked((ushort)value);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private sealed record LegacyZipCentralEntry(
        byte[] NameBytes,
        byte[] ExtraData,
        byte[] CommentBytes,
        long Crc,
        int Size,
        ushort DosTime,
        ushort DosDate,
        int ExternalAttributes,
        uint LocalHeaderOffset);
}
