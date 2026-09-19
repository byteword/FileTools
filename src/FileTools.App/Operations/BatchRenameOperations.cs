using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FileTools;

/// <summary>확정 뒤 같은 경로에 다른 파일이 생기는 경우도 확인하기 위한 Windows 파일 식별 정보.</summary>
internal sealed record RenameFileSnapshot(uint Volume, ulong FileId, ulong Length, ulong CreationTime, ulong LastWriteTime)
{
    public static RenameFileSnapshot Capture(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new IOException(Localizer.Get("BatchRenameFilesOnly"));
        FileOperationPathGuard.EnsureNoLinkedDirectory(Path.GetDirectoryName(path)!);
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!GetFileInformationByHandle(handle, out var info))
            throw new IOException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
        return new(info.Volume, Join(info.IndexHigh, info.IndexLow), Join(info.SizeHigh, info.SizeLow),
            Join((uint)info.Created.dwHighDateTime, (uint)info.Created.dwLowDateTime),
            Join((uint)info.Written.dwHighDateTime, (uint)info.Written.dwLowDateTime));
    }
    private static ulong Join(uint high, uint low) => ((ulong)high << 32) | low;
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
}

internal static class BatchRenameOperations
{
    /// <summary>확정한 이름을 그대로 적용한다. 재계산·충돌 번호 추가·덮어쓰기를 수행하지 않는다.</summary>
    public static OperationResult Apply(BatchRenamePreview row, string currentPath)
    {
        var result = new OperationResult();
        result.AddCandidate();
        try
        {
            var name = Path.GetFileName(row.TargetPath);
            if (row.Status != BatchRenameStatus.Ready || row.Snapshot is null ||
                !FileOperationPathGuard.Comparer.Equals(currentPath, row.OriginalPath) ||
                !FileOperationPathGuard.Comparer.Equals(Path.GetDirectoryName(row.OriginalPath), Path.GetDirectoryName(row.TargetPath)) ||
                !BatchRenamePlanBuilder.IsValidName(name))
                throw new IOException(Localizer.Get("BatchRenameRecheck"));
            if (RenameFileSnapshot.Capture(row.OriginalPath) != row.Snapshot)
                throw new IOException(Localizer.Get("BatchRenameSourceChanged"));
            if (!FileOperationPathGuard.Comparer.Equals(row.OriginalPath, row.TargetPath) && BatchRenamePlanBuilder.PathOccupied(row.TargetPath))
                throw new IOException(Localizer.Format("PlanPreviewTargetExistsFormat", row.TargetPath));
            File.Move(row.OriginalPath, row.TargetPath, overwrite: false);
            result.AddApplied(Path.GetFileName(row.OriginalPath) + " -> " + name);
            FileToolsEnvironment.Log("BATCH-RENAME", row.OriginalPath + " -> " + row.TargetPath);
        }
        catch (Exception ex) { result.AddError(currentPath + " | " + ex.Message); }
        return result;
    }
}
