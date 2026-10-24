using Symphony.Domain;

namespace Symphony.IssueTracker;

sealed class NullIssueTracker : IIssueTracker
{
    readonly Dictionary<string, Issue> issuesById;

    public NullIssueTracker(IEnumerable<Issue>? issues = null)
    {
        issuesById = new Dictionary<string, Issue>(StringComparer.Ordinal);
        if (issues is null)
            return;

        foreach (var issue in issues)
        {
            if (issue is null)
                continue;

            issuesById[issue.Id] = issue;
        }
    }

    public Task<IReadOnlyList<Issue>> FetchIssueStatesByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var result = new List<Issue>(ids?.Count ?? 0);
        if (ids is null)
            return Task.FromResult<IReadOnlyList<Issue>>(result);

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (issuesById.TryGetValue(id, out var existing))
                result.Add(existing);
        }

        return Task.FromResult<IReadOnlyList<Issue>>(result);
    }
}
