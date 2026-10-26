using GitHub.Copilot;
using NUnit.Framework;
using Symphony.Agent;
using Symphony.Config;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Agent;

[TestFixture, Category(TestCatory.Unit)]
class CopilotMcpServersTests
{
    [Test]
    public void Build_LinearTrackerWithKey_RegistersHttpServerWithBearerHeader()
    {
        var servers = CopilotMcpServers.Build(Config("linear", "lin_api_key"));

        Assert.That(servers, Has.Count.EqualTo(1));
        Assert.That(servers.ContainsKey(CopilotMcpServers.LinearServerName), Is.True);

        var server = servers[CopilotMcpServers.LinearServerName] as McpHttpServerConfig;
        Assert.That(server, Is.Not.Null);
        Assert.That(server!.Url, Is.EqualTo(CopilotMcpServers.LinearMcpUrl));
        Assert.That(server.Headers, Does.ContainKey("Authorization"));
        Assert.That(server.Headers!["Authorization"], Is.EqualTo("Bearer lin_api_key"));
        Assert.That(server.Tools, Is.EquivalentTo(new[] { "*" }));
    }

    [Test]
    public void Build_KeyAlreadyHasBearerPrefix_DoesNotDuplicatePrefix()
    {
        var servers = CopilotMcpServers.Build(Config("linear", "Bearer lin_api_key"));
        var server = (McpHttpServerConfig)servers[CopilotMcpServers.LinearServerName];

        Assert.That(server.Headers!["Authorization"], Is.EqualTo("Bearer lin_api_key"));
    }

    [Test]
    public void Build_NonLinearTracker_ReturnsNoServers()
    {
        Assert.That(CopilotMcpServers.Build(Config("github", "token")), Is.Empty);
    }

    [Test]
    public void Build_LinearTrackerWithoutKey_ReturnsNoServers()
    {
        Assert.That(CopilotMcpServers.Build(Config("linear", " ")), Is.Empty);
    }

    [Test]
    public void Validate_TicketJobWithLinearTrackerAndNoKey_ReturnsCredentialsMissing()
    {
        var error = CopilotMcpServers.Validate(Config("linear", string.Empty), requiresLinearTracker: true);

        Assert.That(error, Does.Contain("linear_credentials_missing"));
        Assert.That(error, Does.Contain("LINEAR_API_KEY"));
    }

    [Test]
    public void Validate_TicketJobWithLinearTrackerAndKey_ReturnsNull()
    {
        Assert.That(CopilotMcpServers.Validate(Config("linear", "lin_api_key"), requiresLinearTracker: true), Is.Null);
    }

    [Test]
    public void Validate_WorkItemJobWithLinearTrackerAndNoKey_ReturnsNull()
    {
        Assert.That(CopilotMcpServers.Validate(Config("linear", string.Empty), requiresLinearTracker: false), Is.Null);
    }

    [Test]
    public void Validate_NonLinearTracker_ReturnsNull()
    {
        Assert.That(CopilotMcpServers.Validate(Config("github", string.Empty), requiresLinearTracker: true), Is.Null);
    }

    static ServiceConfig Config(string kind, string apiKey) =>
        new(
            new Workflows.WorkflowDefinition(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase), ""),
            new TrackerConfig(kind, "endpoint", apiKey, "slug", [], []),
            new WorkspaceConfig(Path.GetTempPath()),
            new HooksConfig(null, null, null, null, 60000),
            new AgentConfig(1, 1, 1, new Dictionary<string, int>()),
            null,
            null);
}
