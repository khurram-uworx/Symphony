using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Symphony.Config;
using Symphony.Dto;

namespace Symphony.IssueTracker;

sealed class IssueTrackerFactory
{
    public static IIssueTracker Create(ServiceConfig config) =>
        Create(config, submissionContext: null);

    public static IIssueTracker Create(ServiceConfig config, object? submissionContext)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (IsInlineOnly(submissionContext))
            return new NullIssueTracker();

        var kind = config.Tracker.Kind?.Trim() ?? string.Empty;

        return kind.ToLowerInvariant() switch
        {
            "linear" => new LinearClient(new SimpleHttpClientFactory(), NullLogger<LinearClient>.Instance, config),
            "jira" => new JiraClient(NullLogger<JiraClient>.Instance, config),
            "null" or "none" or "inline" or "" => new NullIssueTracker(),
            _ => throw new ArgumentException(
                $"Unsupported tracker kind '{config.Tracker.Kind}'. Supported values: 'linear', 'jira'.",
                nameof(config))
        };
    }

    static bool IsInlineOnly(object? submissionContext)
    {
        if (submissionContext is null)
            return false;

        if (submissionContext is WorkRequest request)
            return request.TicketIds is null || request.TicketIds.Count == 0;

        if (submissionContext is SubmissionContext context)
            return context.TicketIds is null || context.TicketIds.Count == 0;

        var ticketCount = TryGetCollectionCount(submissionContext, "TicketIds")
            ?? TryGetCollectionCount(submissionContext, "ticketIds")
            ?? TryGetCollectionCount(submissionContext, "TicketID");

        if (ticketCount.HasValue)
            return ticketCount.Value == 0;

        return false;
    }

    static int? TryGetCollectionCount(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName);
        if (property is null)
            return null;

        var raw = property.GetValue(value);
        if (raw is null)
            return 0;

        if (raw is System.Collections.ICollection collection)
            return collection.Count;

        if (raw is System.Collections.IEnumerable enumerable)
        {
            var count = 0;
            foreach (var _ in enumerable)
                count++;
            return count;
        }

        return null;
    }

    sealed class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
