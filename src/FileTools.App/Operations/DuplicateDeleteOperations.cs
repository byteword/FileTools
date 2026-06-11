using Microsoft.VisualBasic.FileIO;

namespace FileTools;

/// <summary>
/// 중복 파일을 휴지통으로 이동해 삭제 처리하는 전용 유틸리티.
/// </summary>
internal static class DuplicateDeleteOperations
{
    /// <summary>
    /// 단일 경로를 휴지통으로 이동한다.
    /// 경로가 없으면 스킵 처리한다.
    /// </summary>
    public static OperationResult MoveFileToRecycleBin(string path)
    {
        var result = new OperationResult();
        result.AddCandidate();

        if (!File.Exists(path))
        {
            result.AddSkipped(Localizer.Format("DuplicateDeleteMissingFileFormat", path));
            return result;
        }

        try
        {
            FileSystem.DeleteFile(
                path,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException);
            result.AddApplied(Localizer.Format("DuplicateDeleteMovedToRecycleBinFormat", path));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.AddError(Localizer.Format("DuplicateDeleteFailedFormat", path, ex.Message));
        }

        return result;
    }
}
