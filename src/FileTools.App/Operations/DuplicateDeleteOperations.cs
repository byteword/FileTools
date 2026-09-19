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
    public static OperationResult MoveFileToRecycleBin(string path, DuplicateDeleteVerification? verification = null,
        CancellationToken cancellationToken = default, Action<string, Action>? recycle = null)
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
            if (verification is null) throw new IOException(Localizer.Get("DuplicateRecheckRequired"));
            verification.WithVerifiedFiles(path, cancellationToken,
                verify => (recycle ?? WindowsRecycleBin.RecycleFile)(path, verify));
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
