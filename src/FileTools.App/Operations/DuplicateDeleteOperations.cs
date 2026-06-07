using Microsoft.VisualBasic.FileIO;

namespace FileTools;

internal static class DuplicateDeleteOperations
{
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
