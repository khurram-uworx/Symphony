using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Symphony.Data;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests;

[TestFixture, Category(TestCatory.Unit)]
class DatabaseRegistrationTests
{
    const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";
    const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
    const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

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
        using var provider = register("sqlite", sqlite: "Data Source=:memory:");

        Assert.That(provider.GetService<IDbContextFactory<SymphonyDbContext>>(), Is.Not.Null);
        using var scope = provider.CreateScope();
        Assert.That(scope.ServiceProvider.GetService<SymphonyDbContext>(), Is.Not.Null);
        Assert.That(activeProvider(provider), Is.EqualTo(SqliteProvider));
        Assert.That(migrationsAssembly(provider), Is.EqualTo("Symphony.Migrations.Sqlite"));
    }

    [Test]
    public async Task AddSymphonyDatabase_MissingDirectory_CreatesDatabaseOnInitialize()
    {
        await using var provider = register("sqlite", sqlite: "Data Source=App_Data/symphony.db");

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
    public void AddSymphonyDatabase_SqlServerProvider_RegistersSqlServer()
    {
        using var provider = register("sqlserver", sqlServer: "Server=localhost;Database=symphony");

        Assert.That(activeProvider(provider), Is.EqualTo(SqlServerProvider));
        Assert.That(migrationsAssembly(provider), Is.EqualTo("Symphony.Migrations.SqlServer"));
    }

    [Test]
    public void AddSymphonyDatabase_SqlServerIgnoringCase_RegistersSqlServer()
    {
        using var provider = register("SqlServer", sqlServer: "Server=localhost;Database=symphony");

        Assert.That(activeProvider(provider), Is.EqualTo(SqlServerProvider));
    }

    [Test]
    public void AddSymphonyDatabase_PostgreSqlProvider_RegistersNpgsql()
    {
        using var provider = register("postgresql", postgreSql: "Host=localhost;Database=symphony");

        Assert.That(activeProvider(provider), Is.EqualTo(NpgsqlProvider));
        Assert.That(migrationsAssembly(provider), Is.EqualTo("Symphony.Migrations.PostgreSQL"));
    }

    [Test]
    public void AddSymphonyDatabase_PostgresAlias_RegistersNpgsql()
    {
        using var provider = register("postgres", postgreSql: "Host=localhost;Database=symphony");

        Assert.That(activeProvider(provider), Is.EqualTo(NpgsqlProvider));
        Assert.That(migrationsAssembly(provider), Is.EqualTo("Symphony.Migrations.PostgreSQL"));
    }

    [Test]
    public void AddSymphonyDatabase_NpgsqlAlias_RegistersNpgsql()
    {
        using var provider = register("npgsql", npgsql: "Host=localhost;Database=symphony");

        Assert.That(activeProvider(provider), Is.EqualTo(NpgsqlProvider));
        Assert.That(migrationsAssembly(provider), Is.EqualTo("Symphony.Migrations.PostgreSQL"));
    }

    [Test]
    public void AddSymphonyDatabase_PostgreSqlKey_PreferredOverNpgsql()
    {
        using var provider = register(
            "postgresql",
            postgreSql: "Host=preferred;Database=symphony",
            npgsql: "Host=alias;Database=symphony");

        Assert.That(connectionString(provider), Does.Contain("preferred").And.Not.Contain("alias"));
    }

    [Test]
    public void AddSymphonyDatabase_ConfigSwitch_ChangesActiveProvider()
    {
        using var sqlite = register("sqlite", sqlite: "Data Source=:memory:");
        using var sqlServer = register("sqlserver", sqlServer: "Server=localhost;Database=symphony");
        using var postgres = register("postgresql", postgreSql: "Host=localhost;Database=symphony");

        Assert.That(activeProvider(sqlite), Is.EqualTo(SqliteProvider));
        Assert.That(activeProvider(sqlServer), Is.EqualTo(SqlServerProvider));
        Assert.That(activeProvider(postgres), Is.EqualTo(NpgsqlProvider));
    }

    [Test]
    public void AddSymphonyDatabase_MissingSqlServerConnectionString_NamesProviderAndKey()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => register("sqlserver"));

        Assert.That(exception!.Message, Does.Contain("Connection string 'SqlServer'"));
        Assert.That(exception.Message, Does.Contain("Database:Provider 'sqlserver'"));
    }

    [Test]
    public void AddSymphonyDatabase_BlankSqlServerConnectionString_NamesProviderAndKey()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            register("SQLServer", sqlServer: "   "));

        Assert.That(exception!.Message, Does.Contain("Connection string 'SqlServer'"));
        Assert.That(exception.Message, Does.Contain("Database:Provider 'SQLServer'"));
    }

    [Test]
    public void AddSymphonyDatabase_MissingPostgreSqlConnectionString_NamesProviderAndKey()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => register("npgsql"));

        Assert.That(exception!.Message, Does.Contain("PostgreSQL"));
        Assert.That(exception.Message, Does.Contain("Npgsql"));
        Assert.That(exception.Message, Does.Contain("Database:Provider 'npgsql'"));
    }

    [Test]
    public void AddSymphonyDatabase_UnknownProvider_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => register("oracle"));

        Assert.That(exception!.Message, Does.Contain("Unsupported database provider"));
    }

    ServiceProvider register(
        string provider,
        string? sqlite = null,
        string? sqlServer = null,
        string? postgreSql = null,
        string? npgsql = null)
    {
        var services = new ServiceCollection();
        services.AddSymphonyDatabase(
            configuration(provider, sqlite, sqlServer, postgreSql, npgsql),
            contentRoot);
        return services.BuildServiceProvider();
    }

    static string migrationsAssembly(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SymphonyDbContext>>();
        return RelationalOptionsExtension.Extract(options).MigrationsAssembly!;
    }

    static string activeProvider(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SymphonyDbContext>().Database.ProviderName!;
    }

    static string connectionString(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SymphonyDbContext>().Database.GetDbConnection().ConnectionString;
    }

    static IConfiguration configuration(
        string provider,
        string? sqlite = null,
        string? sqlServer = null,
        string? postgreSql = null,
        string? npgsql = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider
        };
        if (sqlite is not null)
            values["ConnectionStrings:Sqlite"] = sqlite;
        if (sqlServer is not null)
            values["ConnectionStrings:SqlServer"] = sqlServer;
        if (postgreSql is not null)
            values["ConnectionStrings:PostgreSQL"] = postgreSql;
        if (npgsql is not null)
            values["ConnectionStrings:Npgsql"] = npgsql;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
