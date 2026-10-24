using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Symphony.Data;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Data;

[TestFixture, Category(TestCatory.Unit)]
class JobPersistenceTests
{
    string directory = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "symphony-job-" + Guid.NewGuid().ToString("N"));
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
    public async Task PersistingJobWithIssuesAndEvents_Succeeds()
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
            db.JobIssues.Add(new JobIssue
            {
                JobId = jobId,
                Identifier = "ISSUE-1",
                Title = "Test issue",
                State = "Queued",
                Success = false
            });
            db.JobEvents.Add(new JobEvent
            {
                JobId = jobId,
                Timestamp = DateTimeOffset.UtcNow,
                EventName = "job.queued",
                Summary = "Queued"
            });

            await db.SaveChangesAsync();
        }

        await using (var db = await factory.CreateDbContextAsync())
        {
            var job = await db.Jobs
                .Include(j => j.Issues)
                .Include(j => j.Events)
                .SingleAsync(j => j.Id == jobId);

            Assert.That(job.Issues, Has.Count.EqualTo(1));
            Assert.That(job.Events, Has.Count.EqualTo(1));
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