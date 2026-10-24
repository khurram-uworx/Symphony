using System.ComponentModel.DataAnnotations.Schema;

namespace Symphony.Data;

public sealed class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? ExternalId { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public string WorkflowName { get; set; } = string.Empty;

    public Guid? RetryOfJobId { get; set; }

    public string Repository { get; set; } = string.Empty;

    public string? EncryptedRequestSnapshot { get; set; }

    public string State { get; set; } = "Queued";

    public DateTimeOffset QueuedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? SummaryJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public ICollection<JobIssue> Issues { get; set; } = [];

    [NotMapped]
    public ICollection<JobIssue> JobIssues
    {
        get => Issues;
        set => Issues = value ?? [];
    }

    public ICollection<JobEvent> Events { get; set; } = [];

    [NotMapped]
    public ICollection<JobEvent> JobEvents
    {
        get => Events;
        set => Events = value ?? [];
    }
}