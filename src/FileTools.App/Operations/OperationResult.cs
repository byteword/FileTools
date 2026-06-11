using System.Text;

namespace FileTools;

/// <summary>
/// 작업 실행 집계 결과를 보관하는 컨테이너.
/// </summary>
internal sealed class OperationResult
{
    /// <summary>
    /// 현재 집계 대상(입력 후보) 건수.
    /// </summary>
    public int CandidateCount { get; private set; }
    /// <summary>
    /// 실제 적용 성공 건수.
    /// </summary>
    public int AppliedCount { get; private set; }
    /// <summary>
    /// 스킵 건수.
    /// </summary>
    public int SkippedCount { get; private set; }
    /// <summary>
    /// 사용자 메시지 목록(성공/스킵 포함).
    /// </summary>
    public List<string> Messages { get; } = [];
    /// <summary>
    /// 오류 메시지 목록.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// 에러 메시지 존재 여부.
    /// </summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>
    /// 후보 1건을 추가한다.
    /// </summary>
    public void AddCandidate()
    {
        CandidateCount++;
    }

    /// <summary>
    /// 적용 메시지를 추가한다.
    /// </summary>
    public void AddApplied(string message)
    {
        AppliedCount++;
        Messages.Add(message);
    }

    /// <summary>
    /// 스킵 메시지(표준 접두어 포함)를 추가한다.
    /// </summary>
    public void AddSkipped(string message)
    {
        SkippedCount++;
        Messages.Add(Localizer.Get("SkipPrefix") + message);
    }

    /// <summary>
    /// 오류 메시지를 추가하고 로그에 기록한다.
    /// </summary>
    public void AddError(string message)
    {
        Errors.Add(message);
        FileToolsEnvironment.Log("ERROR", message);
    }

    /// <summary>
    /// 다른 결과를 현재 인스턴스로 병합한다.
    /// </summary>
    public void Merge(OperationResult other)
    {
        CandidateCount += other.CandidateCount;
        AppliedCount += other.AppliedCount;
        SkippedCount += other.SkippedCount;
        Messages.AddRange(other.Messages);
        Errors.AddRange(other.Errors);
    }

    /// <summary>
    /// 사용자 메시지 본문을 생성한다.
    /// </summary>
    /// <param name="title">제목/헤더</param>
    public string ToUserMessage(string title)
    {
        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine();
        builder.AppendLine(Localizer.Format("ResultTargetCount", CandidateCount));
        builder.AppendLine(Localizer.Format("ResultAppliedCount", AppliedCount));
        builder.AppendLine(Localizer.Format("ResultSkippedCount", SkippedCount));
        builder.AppendLine(Localizer.Format("ResultErrorCount", Errors.Count));

        if (Errors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(Localizer.Get("ResultErrorsHeader"));
            foreach (var error in Errors.Take(20))
            {
                builder.AppendLine("- " + error);
            }
        }

        if (Messages.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(Localizer.Get("ResultOperationsHeader"));
            foreach (var message in Messages.Take(30))
            {
                builder.AppendLine("- " + message);
            }
        }

        return builder.ToString();
    }
}
