using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Symphony.Data;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests;

[TestFixture, Category(TestCatory.Unit)]
class DatabaseRegistrationTests
{
    string contentRoot = null!;

    [SetUp]
    public void SetUp()
    {
        contentRoot = Path.Combine(Path.GetTempPath(), "symphony-reg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(contentRoot))
            Directory.Delete(contentRoot, recursive: true);
    }

    [Test]
    public void AddSymphonyDatabase_SqliteProvider_RegistersFactoryAndContext()
    {
        var services = new ServiceCollection();

        services.AddSymphonyDatabase(configuration("sqlite", "Data Source=:memory:"), contentRoot);

        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetService<IDbContextFactory<SymphonyDbContext>>(), Is.Not.Null);
        using var scope = provider.CreateScope();
        Assert.That(scope.ServiceProvider.GetService<SymphonyDbContext>(), Is.Not.Null);
    }

    [Test]
    public async Task AddSymphonyDatabase_MissingDirectory_CreatesDatabaseOnInitialize()
    {
        var services = new ServiceCollection();
        services.AddSymphonyDatabase(
            configuration("sqlite", "Data Source=App_Data/symphony.db"),
            contentRoot);
        await using var provider = services.BuildServiceProvider();

        await DatabaseInitializer.InitializeAsync(
            provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>());

        var database = Path.Combine(contentRoot, "App_Data", "symphony.db");
        Assert.That(File.Exists(database), Is.True);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SymphonyDbContext>();
        Assert.That(await db.Users.CountAsync(), Is.Zero);
        Assert.That(await db.Roles.CountAsync(), Is.Zero);
    }

    [Test]
    public void AddSymphonyDatabase_SqlServerProvider_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSymphonyDatabase(configuration("sqlserver"), contentRoot));

        Assert.That(exception!.Message, Does.Contain("not supported in this build"));
    }

    [Test]
    public void AddSymphonyDatabase_PostgreSqlAlias_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSymphonyDatabase(configuration("postgresql"), contentRoot));

        Assert.That(exception!.Message, Does.Contain("not supported in this build"));
    }

    [Test]
    public void AddSymphonyDatabase_UnknownProvider_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSymphonyDatabase(configuration("oracle"), contentRoot));

        Assert.That(exception!.Message, Does.Contain("Unsupported database provider"));
    }

    static IConfiguration configuration(string provider, string? sqlite = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider
        };
        if (sqlite is not null)
            values["ConnectionStrings:Sqlite"] = sqlite;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
