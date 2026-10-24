using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Symphony.Agent;
using Symphony.Data;
using Symphony.Domain;
using Symphony.Services;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Data;

[TestFixture, Category(TestCatory.Unit)]
class JobServicePersistenceTests
{
    string directory = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "symphony-jobservice-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    [Test]
    public async Task PersistenceMethods_CreateUpdateIssue_AppendRedactedEvent_SetSummary()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);

        var jobId = Guid.NewGuid();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Jobs.Add(new Job
            {
                Id = jobId,
                OwnerUserId = "user-1",
                WorkflowName = "WORKFLOW",
                Repository = "owner/repo",
                State = "Queued",
                QueuedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var service = new JobService(factory);
        var issue = new Issue(
            Guid.NewGuid().ToString("N"),
            "ISSUE-1",
            "Test issue",
            null,
            null,
            "Todo",
            null,
            null,
            [],
            [],
            DateTimeOffset.UtcNow,
            null);

        var issueRowId = await service.CreateIssueRowAsync(jobId, issue, "C:\\workspaces", CancellationToken.None);

        await service.UpdateIssueRowAsync(
            issueRowId,
            issue,
            new AgentRunResult(true, "done", null, 3),
            "C:\\workspaces",
            CancellationToken.None);

        await service.AppendEventAsync(
            jobId,
            issueRowId,
            "issue.payload",
            "Payload event",
            new { repository = "owner/repo", personalAccessToken = "super-secret-pat", apiKey = "secret-key" },
            CancellationToken.None);

        await service.MarkJobStateAsync(
            jobId,
            "Completed",
            null,
            null,
            "{\"summary\":\"done\"}",
            CancellationToken.None);

        await using (var db = await factory.CreateDbContextAsync())
        {
            var job = await db.Jobs.Include(j => j.Issues).Include(j => j.Events).SingleAsync(j => j.Id == jobId);
            Assert.That(job.SummaryJson, Is.EqualTo("{\"summary\":\"done\"}"));

            var row = job.Issues.Single();
            Assert.That(row.Success, Is.True);
            Assert.That(row.Reason, Is.EqualTo("done"));
            Assert.That(row.TurnCount, Is.EqualTo(3));
            Assert.That(row.WorkspacePath, Does.Contain("ISSUE-1"));

            var ev = job.Events.Single(e => e.EventName == "issue.payload");
            Assert.That(ev.PayloadJson, Does.Not.Contain("super-secret-pat"));
            Assert.That(ev.PayloadJson, Does.Not.Contain("secret-key"));
        }
    }

    ServiceProvider createProvider()
    {
        var database = Path.Combine(directory, "symphony.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = database }.ToString();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "sqlite",
                ["ConnectionStrings:Sqlite"] = connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSymphonyDatabase(configuration, directory);
        return services.BuildServiceProvider();
    }
}
