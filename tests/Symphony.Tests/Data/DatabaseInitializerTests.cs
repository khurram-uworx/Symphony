using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Symphony.Data;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests;

[TestFixture, Category(TestCatory.Unit)]
class DatabaseInitializerTests
{
    sealed class TempDbContextFactory(string connectionString) : IDbContextFactory<SymphonyDbContext>
    {
        public SymphonyDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<SymphonyDbContext>()
                .UseSqlite(connectionString)
                .Options;
            return new SymphonyDbContext(options);
        }
    }

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
    public async Task InitializeAsync_MissingFile_CreatesIdentityTables()
    {
        var database = Path.Combine(directory, "symphony.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = database }.ToString();
        var factory = new TempDbContextFactory(connectionString);

        await DatabaseInitializer.InitializeAsync(factory);

        await using var db = factory.CreateDbContext();
        Assert.That(File.Exists(database), Is.True);
        Assert.That(await db.Users.CountAsync(), Is.Zero);
        Assert.That(await db.Roles.CountAsync(), Is.Zero);
    }
}
