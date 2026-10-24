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

    public Task<IReadOnlyList<Issue>> FetchCandidateIssuesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Issue>>(Array.Empty<Issue>());

    public Task<IReadOnlyList<Issue>> FetchTerminalIssuesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Issue>>(Array.Empty<Issue>());

    public Task<IReadOnlyList<Issue>> FetchIssueStatesByIdsAsync(
        IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var result = new List<Issue>(ids?.Count ?? 0);
        if (ids is not null)
        {
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (issuesById.TryGetValue(id, out var existing))
                {
                    result.Add(existing);
                    continue;
                }

                result.Add(new Issue(
                    id,
                    id,
                    "Inline work item",
                    null,
                    null,
                    "In Progress",
                    null,
                    null,
                    Array.Empty<string>(),
                    Array.Empty<BlockerRef>(),
                    null,
                    null));
            }
        }

        return Task.FromResult<IReadOnlyList<Issue>>(result);
    }

    public Task<IReadOnlyList<Issue>> FetchIssueStatesByIdsAsync(
        IEnumerable<string> ids, CancellationToken cancellationToken) =>
        FetchIssueStatesByIdsAsync(ids is IReadOnlyList<string> list ? list : ids?.ToList() ?? new List<string>(), cancellationToken);

    public Task<IReadOnlyList<Issue>> FetchIssueStatesByIdsAsync(
        IReadOnlyList<Issue> issues, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Issue>>(
            issues is null ? Array.Empty<Issue>() : issues);

    public Task<IReadOnlyList<Issue>> FetchIssueStatesByIdsAsync(
        IEnumerable<Issue> issues, CancellationToken cancellationToken)
    {
        if (issues is null)
            return Task.FromResult<IReadOnlyList<Issue>>(Array.Empty<Issue>());

        if (issues is IReadOnlyList<Issue> list)
            return FetchIssueStatesByIdsAsync(list, cancellationToken);

        return Task.FromResult<IReadOnlyList<Issue>>(issues.ToArray());
    }
}
