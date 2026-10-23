using Symphony.Domain;

namespace Symphony.IssueTracker;

interface IIssueTracker
{
    Task<IReadOnlyList<Issue>> FetchCandidateIssuesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Issue>> FetchTerminalIssuesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Issue>> FetchIssueStatesByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);
}
