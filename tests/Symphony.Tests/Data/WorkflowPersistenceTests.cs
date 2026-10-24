using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Symphony.Data;
using Symphony.Security;
using Symphony.Services;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Data;

[TestFixture, Category(TestCatory.Unit)]
class WorkflowPersistenceTests
{
    string directory = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "symphony-wf-" + Guid.NewGuid().ToString("N"));
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
    public async Task CreateAsync_StoresMultipleWorkflowsForOwner()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);
        await ensureUserAsync(provider, "user-1");

        var service = new WorkflowService(factory);

        await service.CreateAsync("user-1", "WORKFLOW", "default content");
        await service.CreateAsync("user-1", "custom", "custom content");

        var list = await service.ListAsync("user-1");
        Assert.That(list, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetAsync_IsCaseInsensitive()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);
        await ensureUserAsync(provider, "user-1");

        var service = new WorkflowService(factory);
        await service.CreateAsync("user-1", "MyFlow", "content");

        var loaded = await service.GetAsync("user-1", "myflow");
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Name, Is.EqualTo("MyFlow"));
        Assert.That(loaded.Content, Is.EqualTo("content"));
    }

    [Test]
    public async Task CreateAsync_RejectsDuplicateNameCaseInsensitively()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);
        await ensureUserAsync(provider, "user-1");

        var service = new WorkflowService(factory);
        await service.CreateAsync("user-1", "Flow", "content");

        var exception = await Assert.ThrowsAsync<WorkflowConflictException>(async () =>
            await service.CreateAsync("user-1", "flow", "other"));
        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    public async Task DeleteAsync_BlocksDeletingLastWorkflow()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);
        await ensureUserAsync(provider, "user-1");

        var service = new WorkflowService(factory);
        await service.CreateAsync("user-1", "Only", "content");

        var exception = await Assert.ThrowsAsync<WorkflowConflictException>(async () =>
            await service.DeleteAsync("user-1", "Only"));
        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    public async Task UpdateAsync_MarkingDefault_ClearsOtherDefaults()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);
        await ensureUserAsync(provider, "user-1");

        var service = new WorkflowService(factory);
        await service.CreateAsync("user-1", "First", "content-1");
        await service.CreateAsync("user-1", "Second", "content-2");

        await service.UpdateAsync("user-1", "Second", new Workflow
        {
            Name = "Second",
            Content = "content-2",
            IsDefault = true
        });

        var list = await service.ListAsync("user-1");
        Assert.That(list.Single(workflow => workflow.Name == "Second").IsDefault, Is.True);
        Assert.That(list.Single(workflow => workflow.Name == "First").IsDefault, Is.False);
    }

    [Test]
    public async Task DeleteAsync_DefaultWorkflow_Throws()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);
        await ensureUserAsync(provider, "user-1");

        var service = new WorkflowService(factory);
        await service.CreateAsync("user-1", "Default", "content-1", isDefault: true);
        await service.CreateAsync("user-1", "Other", "content-2");

        var exception = await Assert.ThrowsAsync<WorkflowConflictException>(async () =>
            await service.DeleteAsync("user-1", "Default"));

        Assert.That(exception!.Code, Is.EqualTo("default_workflow"));
    }

    [Test]
    public async Task DeleteAsync_DeletesNonLastWorkflow()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);
        await ensureUserAsync(provider, "user-1");

        var service = new WorkflowService(factory);
        await service.CreateAsync("user-1", "First", "content-1");
        await service.CreateAsync("user-1", "Second", "content-2");

        var deleted = await service.DeleteAsync("user-1", "FIRST");
        Assert.That(deleted, Is.True);

        var remaining = await service.ListAsync("user-1");
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].Name, Is.EqualTo("Second"));
    }

    [Test]
    public async Task CreateAsync_SetsTimestamps()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);
        await ensureUserAsync(provider, "user-1");

        var service = new WorkflowService(factory);
        var created = await service.CreateAsync("user-1", "Flow", "content");

        Assert.That(created.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(created.UpdatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    static async Task ensureUserAsync(ServiceProvider provider, string userId)
    {
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if (await users.FindByIdAsync(userId) is null)
        {
            var user = new IdentityUser { Id = userId, UserName = userId };
            await users.CreateAsync(user, "Password123!");
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
        services.AddLogging();
        services.AddSymphonyDatabase(configuration, directory);
        services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<SymphonyDbContext>();
        return services.BuildServiceProvider();
    }
}
