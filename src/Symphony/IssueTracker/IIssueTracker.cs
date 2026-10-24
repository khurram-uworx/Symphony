using Symphony.Domain;

namespace Symphony.IssueTracker;

interface IIssueTracker
{
    Task<IReadOnlyList<Issue>> FetchIssueStatesByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
