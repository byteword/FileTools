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
        Messages.Add(Localizer.Get("SkipPrefix") + message);
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
