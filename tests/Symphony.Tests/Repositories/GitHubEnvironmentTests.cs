using NUnit.Framework;
using Symphony.Repositories;
using Symphony.Security;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Repositories;

[TestFixture, Category(TestCatory.Unit)]
class GitHubEnvironmentTests
{
    [Test]
    public void Build_WithToken_SetsHeadersAndRepositoryWithoutTokenInCloneUrl()
    {
        var env = GitHubEnvironment.Build(new Dictionary<string, string> { ["PATH"] = "x" }, "owner/repo", "ghp_secret");

        Assert.That(env["GITHUB_REPOSITORY"], Is.EqualTo("owner/repo"));
        Assert.That(env["GH_TOKEN"], Is.EqualTo("ghp_secret"));
        Assert.That(env["GITHUB_TOKEN"], Is.EqualTo("ghp_secret"));
        Assert.That(env["GIT_TERMINAL_PROMPT"], Is.EqualTo("0"));
        Assert.That(env.ContainsKey("GIT_CONFIG_KEY_0"), Is.True);
        Assert.That(env["GIT_CONFIG_KEY_0"], Does.Contain("extraheader"));
        Assert.That(env["GIT_CONFIG_VALUE_0"], Does.StartWith("Authorization: Basic "));
        Assert.That(env["GIT_CONFIG_VALUE_0"], Does.Not.Contain("ghp_secret"));
    }

    [Test]
    public void Build_WithoutToken_DoesNotInjectAuth()
    {
        var env = GitHubEnvironment.Build(null, "owner/repo", null);

        Assert.That(env.ContainsKey("GH_TOKEN"), Is.False);
        Assert.That(env.ContainsKey("GITHUB_TOKEN"), Is.False);
        Assert.That(env.ContainsKey("GIT_CONFIG_COUNT"), Is.False);
    }

    [Test]
    public void Build_KnownToken_RedactedFromString()
    {
        const string token = "ghp_exampleToken1234567890";
        var env = GitHubEnvironment.Build(null, "owner/repo", token);
        var serialized = string.Join(";", env.Select(pair => pair.Key + "=" + pair.Value));

        Assert.That(Redaction.RedactSecrets(serialized, [token]), Does.Not.Contain(token));
    }
}
