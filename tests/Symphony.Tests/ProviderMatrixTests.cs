using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using Symphony.Data;

namespace Symphony.Tests;

[TestFixture]
[Category("Integration")]
public class ProviderMatrixTests
{
    [Test]
    public async Task SqliteSchemaInitAndBasicFlow()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll(typeof(IDbContextFactory<SymphonyDbContext>));
                    services.RemoveAll(typeof(DbContextOptions<SymphonyDbContext>));
                    var dbPath = Path.Combine(Path.GetTempPath(), $"symphony-test-{Guid.NewGuid()}.db");
                    services.AddDbContextFactory<SymphonyDbContext>(options =>
                        options.UseSqlite($"Data Source={dbPath}"));
                });
            });

        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        Assert.That(await db.Database.CanConnectAsync(), Is.True);
        Assert.That(await db.AppSettings.CountAsync(), Is.GreaterThan(0));

        var client = factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.That((int)response.StatusCode, Is.LessThan(500));
    }

    [Test]
    public async Task SqlServerSchemaInitIfConfigured()
    {
        var conn = Environment.GetEnvironmentVariable("SYMPHONY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(conn))
            Assert.Ignore("SYMPHONY_TEST_SQLSERVER not configured.");

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll(typeof(IDbContextFactory<SymphonyDbContext>));
                    services.RemoveAll(typeof(DbContextOptions<SymphonyDbContext>));
                    services.AddDbContextFactory<SymphonyDbContext>(options =>
                        options.UseSqlServer(conn));
                });
            });

        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        Assert.That(await db.Database.CanConnectAsync(), Is.True);
        Assert.That(await db.AppSettings.CountAsync(), Is.GreaterThan(0));
    }

    [Test]
    public async Task PostgresSchemaInitIfConfigured()
    {
        var conn = Environment.GetEnvironmentVariable("SYMPHONY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(conn))
            Assert.Ignore("SYMPHONY_TEST_POSTGRES not configured.");

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll(typeof(IDbContextFactory<SymphonyDbContext>));
                    services.RemoveAll(typeof(DbContextOptions<SymphonyDbContext>));
                    services.AddDbContextFactory<SymphonyDbContext>(options =>
                        options.UseNpgsql(conn));
                });
            });

        using var scope = factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        Assert.That(await db.Database.CanConnectAsync(), Is.True);
        Assert.That(await db.AppSettings.CountAsync(), Is.GreaterThan(0));
    }
}
