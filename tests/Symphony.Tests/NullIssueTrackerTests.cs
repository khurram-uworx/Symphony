using NUnit.Framework;
using Symphony.Domain;
using Symphony.IssueTracker;
using System.Threading;

namespace Symphony.Tests;

public class NullIssueTrackerTests
{
    [Test]
    public async Task FetchCandidateIssuesAsync_ReturnsEmpty()
    {
        var tracker = new NullIssueTracker();

        var result = await tracker.FetchCandidateIssuesAsync(CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task FetchTerminalIssuesAsync_ReturnsEmpty()
    {
        var tracker = new NullIssueTracker();

        var result = await tracker.FetchTerminalIssuesAsync(CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task FetchIssueStatesByIdsAsync_WithSeededIssues_ReturnsThemUnchanged()
    {
        var issue = new Issue(
            "1",
            "SYM-1",
            "Title",
            "Description",
            2,
            "In Progress",
            null,
            null,
            new[] { "bug" },
            Array.Empty<BlockerRef>(),
            null,
            null);

        var tracker = new NullIssueTracker(new[] { issue });

        var result = await tracker.FetchIssueStatesByIdsAsync(new[] { issue.Id }, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(issue));
    }

    [Test]
    public async Task FetchIssueStatesByIdsAsync_WithIssueListInput_ReturnsItUnchanged()
    {
        var issue = new Issue(
            "2",
            "SYM-2",
            "Second",
            null,
            null,
            "Todo",
            null,
            null,
            Array.Empty<string>(),
            Array.Empty<BlockerRef>(),
            null,
            null);

        var tracker = new NullIssueTracker();
        var input = new[] { issue };

        var result = await tracker.FetchIssueStatesByIdsAsync(input, CancellationToken.None);

        Assert.That(result, Is.EqualTo(input));
    }
}
