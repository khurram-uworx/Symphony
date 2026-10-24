namespace Symphony.Data;

public sealed class JobEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }

    public Guid? JobIssueId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string EventName { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? PayloadJson { get; set; }

    public Job? Job { get; set; }

    public JobIssue? JobIssue { get; set; }
}