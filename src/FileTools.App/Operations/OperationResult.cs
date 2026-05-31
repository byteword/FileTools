using System.Text;

namespace FileTools;

internal sealed class OperationResult
{
    public int CandidateCount { get; private set; }
    public int AppliedCount { get; private set; }
    public int SkippedCount { get; private set; }
    public List<string> Messages { get; } = [];
    public List<string> Errors { get; } = [];

    public bool HasErrors => Errors.Count > 0;

    public void AddCandidate()
    {
        CandidateCount++;
    }

    public void AddApplied(string message)
    {
        AppliedCount++;
        Messages.Add(message);
    }

    public void AddSkipped(string message)
    {
        SkippedCount++;
        Messages.Add("SKIP: " + message);
    }

    public void AddError(string message)
    {
        Errors.Add(message);
        FileToolsEnvironment.Log("ERROR", message);
    }

    public void Merge(OperationResult other)
    {
        CandidateCount += other.CandidateCount;
        AppliedCount += other.AppliedCount;
        SkippedCount += other.SkippedCount;
        Messages.AddRange(other.Messages);
        Errors.AddRange(other.Errors);
    }

    public string ToUserMessage(string title)
    {
        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine();
        builder.AppendLine($"대상: {CandidateCount}");
        builder.AppendLine($"적용: {AppliedCount}");
        builder.AppendLine($"건너뜀: {SkippedCount}");
        builder.AppendLine($"오류: {Errors.Count}");

        if (Errors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("오류:");
            foreach (var error in Errors.Take(20))
            {
                builder.AppendLine("- " + error);
            }
        }

        if (Messages.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("작업:");
            foreach (var message in Messages.Take(30))
            {
                builder.AppendLine("- " + message);
            }
        }

        return builder.ToString();
    }
}
