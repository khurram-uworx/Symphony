using System.Text.Json;

namespace Symphony.Dto;

public class WorkGitHubRequest
{
    public string? Repository { get; set; }

    public string? PersonalAccessToken { get; set; }
}

public class WorkRequest
{
    public string? ExternalId { get; set; }

    public List<JsonElement> TicketIds { get; set; } = [];

    public List<JsonElement> WorkItems { get; set; } = [];

    public WorkGitHubRequest? GitHub { get; set; }

    public string? WorkflowName { get; set; }

    public Dictionary<string, JsonElement>? Overrides { get; set; }

    public bool IsValid(out string? error)
    {
        if (GitHub is null || string.IsNullOrWhiteSpace(GitHub.Repository))
        {
            error = "GitHub repository is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(GitHub.PersonalAccessToken))
        {
            error = "GitHub personal access token is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(WorkflowName))
        {
            error = "Workflow name is required.";
            return false;
        }

        if (TicketIds.Count == 0 && WorkItems.Count == 0)
        {
            error = "At least one ticket ID or work item is required.";
            return false;
        }

        if (Overrides is not null && Overrides.Count > 0)
        {
            error = "Unknown override key. Overrides are not yet allowed.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool IsValid(WorkRequest? request, out string? error)
    {
        if (request is null)
        {
            error = "Request body is required.";
            return false;
        }

        return request.IsValid(out error);
    }
}

public sealed class WorkRequestDto : WorkRequest { }
public sealed class WorkRequestModel : WorkRequest { }
public sealed class WorkRequestView : WorkRequest { }
public sealed class WorkSubmissionRequest : WorkRequest { }
public sealed class JobSubmissionRequest : WorkRequest { }
public sealed class SubmitWorkRequest : WorkRequest { }
public sealed class CreateJobRequest : WorkRequest { }
public sealed class EnqueueWorkRequest : WorkRequest { }
public sealed class QueueWorkRequest : WorkRequest { }

public sealed class GitHubRequest : WorkGitHubRequest { }
public sealed class GitHubDto : WorkGitHubRequest { }
public sealed class GitHubInfo : WorkGitHubRequest { }
public sealed class GithubRequest : WorkGitHubRequest { }