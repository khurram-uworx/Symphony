using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Symphony.Config;
using Symphony.Domain;
using Symphony.IssueTracker;

namespace Symphony.Tests.IssueTracker;

sealed class StaticHttpClientFactory : IHttpClientFactory
{
    readonly HttpMessageHandler handler;
    public StaticHttpClientFactory(HttpMessageHandler handler) => this.handler = handler;
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

sealed class StubHandler : HttpMessageHandler
{
    readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder;
    public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) => this.responder = responder;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        responder(request, cancellationToken);
}

[TestFixture]
class GitHubIssuesClientTests
{
    static ServiceConfig CreateConfig(string endpoint = "https://api.github.com", string slug = "owner/repo", string token = "token")
    {
        return new ServiceConfig(
            new Symphony.Workflows.WorkflowDefinition(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase), ""),
            new TrackerConfig("github", endpoint, token, slug, new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            new WorkspaceConfig(Path.GetTempPath()),
            new HooksConfig(null, null, null, null, 60000),
            new AgentConfig(1, 1, 1, new Dictionary<string, int>()),
            null,
            null);
    }

    static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    [Test]
    public async Task FetchIssueStatesByIdsAsync_ParsesIssue()
    {
        var payload = """
        {
          "number": 42,
          "title": "Fix issue",
          "body": "Body text",
          "state": "open",
          "html_url": "https://github.com/owner/repo/issues/42",
          "labels": [ { "name": "Bug" } ],
          "created_at": "2026-01-01T00:00:00Z",
          "updated_at": "2026-01-02T00:00:00Z"
        }
        """;

        var handler = new StubHandler((_, _) => Task.FromResult(Json(payload)));
        var client = new GitHubIssuesClient(new StaticHttpClientFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubIssuesClient>.Instance, CreateConfig());

        var result = await client.FetchIssueStatesByIdsAsync(new[] { "42" }, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo("42"));
        Assert.That(result[0].Identifier, Is.EqualTo("42"));
        Assert.That(result[0].Title, Is.EqualTo("Fix issue"));
        Assert.That(result[0].Description, Is.EqualTo("Body text"));
        Assert.That(result[0].State, Is.EqualTo("open"));
        Assert.That(result[0].Url, Is.EqualTo("https://github.com/owner/repo/issues/42"));
        Assert.That(result[0].Labels, Is.EqualTo(new[] { "bug" }));
    }

    [Test]
    public async Task FetchIssueStatesByIdsAsync_SkipsPullRequests()
    {
        var payload = """
        {
          "number": 43,
          "title": "PR",
          "state": "open",
          "pull_request": { "url": "x" }
        }
        """;

        var handler = new StubHandler((_, _) => Task.FromResult(Json(payload)));
        var client = new GitHubIssuesClient(new StaticHttpClientFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubIssuesClient>.Instance, CreateConfig());

        var result = await client.FetchIssueStatesByIdsAsync(new[] { "43" }, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task FetchIssueStatesByIdsAsync_EmptyIds_NoCall()
    {
        var handler = new StubHandler((_, _) => throw new InvalidOperationException("should not be called"));
        var client = new GitHubIssuesClient(new StaticHttpClientFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubIssuesClient>.Instance, CreateConfig());

        var result = await client.FetchIssueStatesByIdsAsync(Array.Empty<string>(), CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void FetchIssueStatesByIdsAsync_HttpError_Throws()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json("{}", HttpStatusCode.NotFound)));
        var client = new GitHubIssuesClient(new StaticHttpClientFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubIssuesClient>.Instance, CreateConfig());

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.FetchIssueStatesByIdsAsync(new[] { "99" }, CancellationToken.None));
    }

    [Test]
    public async Task FetchIssueStatesByIdsAsync_EnterpriseEndpoint_BuildsCorrectUrl()
    {
        string? captured = null;
        var payload = """{ "number": 7, "title": "Enterprise", "state": "closed" }""";
        var handler = new StubHandler((req, _) =>
        {
            captured = req.RequestUri!.ToString();
            return Task.FromResult(Json(payload));
        });
        var client = new GitHubIssuesClient(new StaticHttpClientFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubIssuesClient>.Instance, CreateConfig("https://github.example.com/api/v3"));

        var result = await client.FetchIssueStatesByIdsAsync(new[] { "7" }, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(captured, Does.Contain("/api/v3/repos/owner/repo/issues/7"));
    }
}