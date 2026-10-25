using NUnit.Framework;
using Symphony.Domain;
using Symphony.IssueTracker;

namespace Symphony.Tests;

public class NullIssueTrackerTests
{
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
    public async Task FetchIssueStatesByIdsAsync_WithUnknownIds_OmitsThem()
    {
        var tracker = new NullIssueTracker();

        var result = await tracker.FetchIssueStatesByIdsAsync(new[] { "ENG-1", "ENG-2" }, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task FetchIssueStatesByIdsAsync_MixedKnownAndUnknown_ReturnsOnlyKnown()
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

        var tracker = new NullIssueTracker(new[] { issue });

        var result = await tracker.FetchIssueStatesByIdsAsync(new[] { "missing", issue.Id }, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(issue.Id));
    }
}
