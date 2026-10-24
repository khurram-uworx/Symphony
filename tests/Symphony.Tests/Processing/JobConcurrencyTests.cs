using NUnit.Framework;
using Symphony.Processing;
using Symphony.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Processing;

[TestFixture, Category(TestCatory.Unit)]
class JobConcurrencyTests
{
    string directory = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "symphony-concurrency-" + Guid.NewGuid().ToString("N"));
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
    public async Task GetGlobalConcurrencyAsync_UsesAppSetting()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);

        await using (var db = await factory.CreateDbContextAsync())
        {
            var setting = await db.AppSettings.FirstAsync(s => s.Key == "GlobalConcurrency");
            setting.Value = "7";
            await db.SaveChangesAsync();
        }

        var value = await ConcurrencyLimits.GetGlobalConcurrencyAsync(factory, CancellationToken.None);
        Assert.That(value, Is.EqualTo(7));
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
