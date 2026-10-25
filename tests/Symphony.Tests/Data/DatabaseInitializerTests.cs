using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Symphony.Data;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests;

[TestFixture, Category(TestCatory.Unit)]
class DatabaseInitializerTests
{
    string directory = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "symphony-db-" + Guid.NewGuid().ToString("N"));
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
    public async Task InitializeAsync_MissingFile_CreatesIdentityTablesAndHistory()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();

        await DatabaseInitializer.InitializeAsync(factory);

        Assert.That(File.Exists(Path.Combine(directory, "symphony.db")), Is.True);
        await using var db = await factory.CreateDbContextAsync();
        Assert.That(await db.Users.CountAsync(), Is.Zero);
        Assert.That(await db.Roles.CountAsync(), Is.Zero);
        Assert.That(await historyIds(db), Is.EqualTo(new[] { "InitialIdentity", "AddUserConfiguration", "AddWorkflow", "AddAppSetting", "AddJobAggregate", "AddApiKey" }));
    }

    [Test]
    public async Task InitializeAsync_SecondCall_DoesNotAddAnotherHistoryRow()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();

        await DatabaseInitializer.InitializeAsync(factory);
        await DatabaseInitializer.InitializeAsync(factory);

        await using var db = await factory.CreateDbContextAsync();
        Assert.That(await historyIds(db), Is.EqualTo(new[] { "InitialIdentity", "AddUserConfiguration", "AddWorkflow", "AddAppSetting", "AddJobAggregate", "AddApiKey" }));
        Assert.That(await db.Users.CountAsync(), Is.Zero);
        Assert.That(await db.Roles.CountAsync(), Is.Zero);
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

    static async Task<string[]> historyIds(SymphonyDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM \"__EFMigrationsHistory\" ORDER BY MigrationId";
        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var separator = id.IndexOf('_');
            ids.Add(separator < 0 ? id : id[(separator + 1)..]);
        }

        return ids.ToArray();
    }
}
