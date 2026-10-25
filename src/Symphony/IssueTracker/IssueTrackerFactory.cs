using Microsoft.Extensions.Logging.Abstractions;
using Symphony.Config;

namespace Symphony.IssueTracker;

sealed class IssueTrackerFactory
{
    public static IIssueTracker Create(ServiceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var kind = config.Tracker.Kind?.Trim() ?? string.Empty;
        var normalized = kind.ToLowerInvariant();

        return normalized switch
        {
            "" or "none" or "inline" or "null" => new NullIssueTracker(),
            "linear" => new LinearClient(new SimpleHttpClientFactory(), NullLogger<LinearClient>.Instance, config),
            "jira" => new JiraClient(NullLogger<JiraClient>.Instance, config),
            "github" => new GitHubIssuesClient(new SimpleHttpClientFactory(), NullLogger<GitHubIssuesClient>.Instance, config),
            _ => throw new ArgumentException(
                $"Unsupported tracker kind '{config.Tracker.Kind}'. Supported values: 'none', 'linear', 'jira', 'github'.",
                nameof(config))
        };
    }

    sealed class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
