using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Symphony.Data;
using Symphony.Services;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Data;

[TestFixture, Category(TestCatory.Unit)]
class JobServiceRetryTests
{
    string directory = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "symphony-jobservice-retry-" + Guid.NewGuid().ToString("N"));
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
    public async Task CreateRetryAsync_CopiesEncryptedSnapshot_AndSetsRetryOfJobId()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);

        var sourceId = Guid.NewGuid();
        const string snapshot = "{\"externalId\":\"x\",\"ticketIds\":[],\"workItems\":[\"test\"],\"github\":{\"repository\":\"owner/repo\",\"personalAccessToken\":\"token\"},\"workflowName\":\"Workflow\"}";
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser { Id = "user-1", UserName = "user-1", NormalizedUserName = "USER-1" });
            db.Jobs.Add(new Job
            {
                Id = sourceId,
                OwnerUserId = "user-1",
                WorkflowName = "Workflow",
                Repository = "owner/repo",
                EncryptedRequestSnapshot = snapshot,
                State = "Completed",
                QueuedAt = DateTimeOffset.UtcNow
            });
            db.Workflows.Add(new Workflow { Id = Guid.NewGuid().ToString(), Name = "Workflow", OwnerUserId = "user-1", Content = "---\n---\n", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var service = new JobService(factory);
        var retry = await service.CreateRetryAsync(sourceId, "user-1", false, CancellationToken.None);

        Assert.That(retry.Id, Is.Not.EqualTo(sourceId));
        Assert.That(retry.RetryOfJobId, Is.EqualTo(sourceId));
        Assert.That(retry.State, Is.EqualTo("Queued"));
        Assert.That(retry.EncryptedRequestSnapshot, Is.EqualTo(snapshot));
        Assert.That(retry.Repository, Is.EqualTo("owner/repo"));
        Assert.That(retry.OwnerUserId, Is.EqualTo("user-1"));
    }

    [Test]
    public async Task CreateRetryAsync_MissingSnapshot_Throws()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);

        var sourceId = Guid.NewGuid();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser { Id = "user-1", UserName = "user-1", NormalizedUserName = "USER-1" });
            db.Jobs.Add(new Job { Id = sourceId, OwnerUserId = "user-1", WorkflowName = "Workflow", Repository = "owner/repo", EncryptedRequestSnapshot = null, State = "Failed", QueuedAt = DateTimeOffset.UtcNow });
            db.Workflows.Add(new Workflow { Id = Guid.NewGuid().ToString(), Name = "Workflow", OwnerUserId = "user-1", Content = "---\n---\n", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var service = new JobService(factory);
        Assert.That(async () => await service.CreateRetryAsync(sourceId, "user-1", false, CancellationToken.None), Throws.InvalidOperationException.With.Message.Contains("snapshot is missing"));
    }

    [Test]
    public async Task CreateRetryAsync_DifferentOwner_NotAdmin_Throws()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);

        var sourceId = Guid.NewGuid();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser { Id = "user-1", UserName = "user-1", NormalizedUserName = "USER-1" });
            db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser { Id = "user-2", UserName = "user-2", NormalizedUserName = "USER-2" });
            db.Jobs.Add(new Job { Id = sourceId, OwnerUserId = "user-1", WorkflowName = "Workflow", Repository = "owner/repo", EncryptedRequestSnapshot = "{\"github\":{\"repository\":\"owner/repo\",\"personalAccessToken\":\"t\"}}", State = "Failed", QueuedAt = DateTimeOffset.UtcNow });
            db.Workflows.Add(new Workflow { Id = Guid.NewGuid().ToString(), Name = "Workflow", OwnerUserId = "user-1", Content = "---\n---\n", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var service = new JobService(factory);
        Assert.That(async () => await service.CreateRetryAsync(sourceId, "user-2", false, CancellationToken.None), Throws.TypeOf<UnauthorizedAccessException>());
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
