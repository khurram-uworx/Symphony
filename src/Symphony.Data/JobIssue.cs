namespace Symphony.Data;

public sealed class JobIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }

    public string? IssueId { get; set; }

    public string? Identifier { get; set; }

    public string? Title { get; set; }

    public string? State { get; set; }

    public bool Success { get; set; }

    public string? Reason { get; set; }

    public int TurnCount { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? WorkspacePath { get; set; }

    public Job? Job { get; set; }
}