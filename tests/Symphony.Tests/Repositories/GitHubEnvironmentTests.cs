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

    [Test]
    public void Build_WithIdentity_SetsCommitIdentityAndDisablesPrompts()
    {
        var identity = new GitHubIdentity("Octo Cat", "octocat@users.noreply.github.com");

        var env = GitHubEnvironment.Build(null, "owner/repo", null, identity);

        Assert.That(env["GIT_AUTHOR_NAME"], Is.EqualTo("Octo Cat"));
        Assert.That(env["GIT_AUTHOR_EMAIL"], Is.EqualTo("octocat@users.noreply.github.com"));
        Assert.That(env["GIT_COMMITTER_NAME"], Is.EqualTo("Octo Cat"));
        Assert.That(env["GIT_COMMITTER_EMAIL"], Is.EqualTo("octocat@users.noreply.github.com"));
        Assert.That(env["GH_PROMPT_DISABLED"], Is.EqualTo("1"));
        Assert.That(env["GH_NO_UPDATE_NOTIFIER"], Is.EqualTo("1"));
    }

    [Test]
    public void Build_ExistingGitConfig_AppendsAuthHeaderInsteadOfOverwritingIt()
    {
        var baseEnvironment = new Dictionary<string, string>
        {
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "safe.directory",
            ["GIT_CONFIG_VALUE_0"] = "*"
        };

        var env = GitHubEnvironment.Build(baseEnvironment, "owner/repo", "ghp_secret");

        Assert.That(env["GIT_CONFIG_COUNT"], Is.EqualTo("2"));
        Assert.That(env["GIT_CONFIG_KEY_0"], Is.EqualTo("safe.directory"));
        Assert.That(env["GIT_CONFIG_VALUE_0"], Is.EqualTo("*"));
        Assert.That(env["GIT_CONFIG_KEY_1"], Does.Contain("extraheader"));
        Assert.That(env["GIT_CONFIG_VALUE_1"], Does.StartWith("Authorization: Basic "));
    }

    [Test]
    public void FromProcessEnvironment_IncludesProcessVariable()
    {
        const string name = "SYMPHONY_TEST_ENVIRONMENT_MARKER";
        Environment.SetEnvironmentVariable(name, "value");

        try
        {
            var env = GitHubEnvironment.FromProcessEnvironment();

            Assert.That(env[name], Is.EqualTo("value"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }
}
