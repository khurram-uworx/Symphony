using NUnit.Framework;
using Symphony.Dto;
using Symphony.Repositories;
using Symphony.Security;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Repositories;

[TestFixture, Category(TestCatory.Unit)]
class GitHubRepositoryTests
{
    [Test]
    public void TryParse_AcceptedForms_CanonicalizeToOwnerSlashName()
    {
        var inputs = new[]
        {
            "khurram-uworx/Symphony",
            "https://github.com/khurram-uworx/Symphony",
            "https://github.com/khurram-uworx/Symphony.git",
            "https://github.com/khurram-uworx/Symphony/",
            "git@github.com:khurram-uworx/Symphony.git"
        };

        foreach (var input in inputs)
        {
            Assert.That(GitHubRepository.TryParse(input, out var repository, out var error), Is.True, input);
            Assert.That(error, Is.Null, input);
            Assert.That(repository!.Slug, Is.EqualTo("khurram-uworx/Symphony"), input);
            Assert.That(repository.CloneUrl, Is.EqualTo("https://github.com/khurram-uworx/Symphony.git"), input);
        }
    }

    [Test]
    public void TryParse_InvalidForms_ReturnError()
    {
        var inputs = new[]
        {
            "",
            "   ",
            "symphony",
            "owner/name/extra",
            "https://gitlab.com/owner/name",
            "https://github.com/owner",
            "https://github.com/owner/name/extra",
            "https://github.com/owner/name?tab=readme",
            "https://github.com/owner/name#readme",
            "ssh://git@github.com/owner/name.git",
            "owner/na me",
            "-owner/name",
            "owner/.",
            "owner/.."
        };

        foreach (var input in inputs)
        {
            Assert.That(GitHubRepository.TryParse(input, out var repository, out var error), Is.False, input);
            Assert.That(repository, Is.Null, input);
            Assert.That(error, Is.Not.Null.And.Not.Empty, input);
        }
    }

    [Test]
    public void WorkRequest_InvalidRepository_ReportsValidationError()
    {
        var request = new WorkRequest
        {
            GitHub = new WorkGitHubRequest { Repository = "not a repository", PersonalAccessToken = "token" },
            WorkflowName = "Workflow",
            WorkItems = [System.Text.Json.JsonSerializer.SerializeToElement("do work")]
        };

        Assert.That(request.IsValid(out var error), Is.False);
        Assert.That(error, Does.Contain("GitHub repository"));
    }

    [Test]
    public void WorkRequest_SshRepository_IsStoredAsCanonicalSlug()
    {
        var request = new WorkRequest
        {
            GitHub = new WorkGitHubRequest
            {
                Repository = "git@github.com:khurram-uworx/Symphony.git",
                PersonalAccessToken = "token"
            },
            WorkflowName = "Workflow",
            WorkItems = [System.Text.Json.JsonSerializer.SerializeToElement("do work")]
        };

        Assert.That(request.IsValid(out var error), Is.True, error);
        Assert.That(request.GitHub!.Repository, Is.EqualTo("khurram-uworx/Symphony"));
    }

    [Test]
    public void RedactSecrets_KnownToken_IsReplacedEverywhere()
    {
        const string token = "ghp_exampleToken1234567890";
        var text = $"clone failed for {token} and again {token}";

        var redacted = Redaction.RedactSecrets(text, [token]);

        Assert.That(redacted, Does.Not.Contain(token));
        Assert.That(redacted, Is.EqualTo($"clone failed for {Redaction.Redacted} and again {Redaction.Redacted}"));
    }
}
