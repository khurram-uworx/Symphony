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
class UserConfigurationPersistenceTests
{
    string directory = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "symphony-uc-" + Guid.NewGuid().ToString("N"));
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
    public async Task SaveAsync_CreatesRowAndEncryptsSecret()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);

        await ensureUserAsync(provider, "user-1");

        var protector = new SecretProtector(new EphemeralDataProtectionProvider());
        var service = new ConfigurationService(factory, protector);

        await service.SaveAsync("user-1", new UserConfiguration
        {
            TrackerKind = "linear",
            TrackerApiKey = "super-secret",

        });

        await using var db = await factory.CreateDbContextAsync();
        var row = await db.UserConfigurations.SingleAsync();
        Assert.That(row.UserId, Is.EqualTo("user-1"));
        Assert.That(row.TrackerApiKey, Is.Not.EqualTo("super-secret"));
        Assert.That(row.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));

        var loaded = await service.GetAsync("user-1");
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.TrackerApiKey, Is.EqualTo("super-secret"));
    }

    [Test]
    public async Task SaveAsync_UpdatesExistingRowWithoutDuplicating()
    {
        await using var provider = createProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await DatabaseInitializer.InitializeAsync(factory);

        await ensureUserAsync(provider, "user-1");

        var protector = new SecretProtector(new EphemeralDataProtectionProvider());
        var service = new ConfigurationService(factory, protector);

        await service.SaveAsync("user-1", new UserConfiguration { TrackerApiKey = "first" });
        await service.SaveAsync("user-1", new UserConfiguration { TrackerApiKey = "second" });

        await using var db = await factory.CreateDbContextAsync();
        Assert.That(await db.UserConfigurations.CountAsync(), Is.EqualTo(1));

        var loaded = await service.GetAsync("user-1");
        Assert.That(loaded!.TrackerApiKey, Is.EqualTo("second"));
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