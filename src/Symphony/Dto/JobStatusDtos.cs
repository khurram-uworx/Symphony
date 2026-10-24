using System.Text.Json;
using Symphony.Data;

namespace Symphony.Dto;

public class JobIssueStatusDto
{
    public Guid Id { get; set; }

    public string? IssueId { get; set; }

    public string? Identifier { get; set; }

    public string? Title { get; set; }

    public string? State { get; set; }

    public bool Success { get; set; }

    public string? Reason { get; set; }

    public int TurnCount { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public static JobIssueStatusDto From(Symphony.Data.JobIssue source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new JobIssueStatusDto
        {
            Id = source.Id,
            IssueId = source.IssueId,
            Identifier = source.Identifier,
            Title = source.Title,
            State = source.State,
            Success = source.Success,
            Reason = source.Reason,
            TurnCount = source.TurnCount,
            StartedAt = source.StartedAt,
            CompletedAt = source.CompletedAt
        };
    }
}

public class JobTrackerStatusDto
{
    public string? Kind { get; set; }

    public string? ProjectSlug { get; set; }

    public string? Endpoint { get; set; }
}

public class JobAgentStatusDto
{
    public string? Type { get; set; }

    public string? Provider { get; set; }

    public string? ModelId { get; set; }
}

public class JobStatusDto
{
    public Guid Id { get; set; }

    public string? ExternalId { get; set; }

    public string? Owner { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public string? WorkflowName { get; set; }

    public string State { get; set; } = "Queued";

    public DateTimeOffset QueuedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public JobTrackerStatusDto? Tracker { get; set; }

    public JobAgentStatusDto? Agent { get; set; }

    public List<JobIssueStatusDto> Issues { get; set; } = [];

    public int IssueCount { get; set; }

    public int FailedIssueCount { get; set; }

    public string? Error { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? Summary { get; set; }

    public Guid? RetryOfJobId { get; set; }

    public static JobStatusDto From(
        Symphony.Data.Job source,
        IEnumerable<Symphony.Data.JobIssue>? issues = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var issueList = issues?.ToList() ?? source.Issues?.ToList() ?? new List<Symphony.Data.JobIssue>();
        return new JobStatusDto
        {
            Id = source.Id,
            ExternalId = source.ExternalId,
            Owner = source.OwnerUserId,
            OwnerUserId = source.OwnerUserId,
            WorkflowName = source.WorkflowName,
            State = source.State,
            QueuedAt = source.QueuedAt,
            StartedAt = source.StartedAt,
            CompletedAt = source.CompletedAt,
            Issues = issueList.Select(JobIssueStatusDto.From).ToList(),
            IssueCount = issueList.Count,
            FailedIssueCount = issueList.Count(issue => !issue.Success),
            Error = string.IsNullOrWhiteSpace(source.ErrorMessage)
                ? source.ErrorCode
                : source.ErrorMessage,
            ErrorCode = source.ErrorCode,
            ErrorMessage = source.ErrorMessage,
            Summary = TryReadSummary(source.SummaryJson),
            RetryOfJobId = source.RetryOfJobId
        };
    }

    static string? TryReadSummary(string? summaryJson)
    {
        if (string.IsNullOrWhiteSpace(summaryJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(summaryJson!);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("summary", out var summary) &&
                summary.ValueKind == JsonValueKind.String)
            {
                return summary.GetString();
            }
        }
        catch
        {
            // SummaryJson is not guaranteed to be JSON.
        }

        return summaryJson;
    }
}

public sealed class JobStatusResponse : JobStatusDto { }
public sealed class JobStatus : JobStatusDto { }
public sealed class JobStatusView : JobStatusDto { }
public sealed class JobStatusModel : JobStatusDto { }
public sealed class JobStatusItem : JobStatusDto { }
public sealed class JobDto : JobStatusDto { }
public sealed class JobView : JobStatusDto { }
public sealed class JobModel : JobStatusDto { }
public sealed class JobQueryDto : JobStatusDto { }
public sealed class JobSummaryDto : JobStatusDto { }