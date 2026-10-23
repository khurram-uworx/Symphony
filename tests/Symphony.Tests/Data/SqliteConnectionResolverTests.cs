using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Symphony.Data;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests;

[TestFixture, Category(TestCatory.Unit)]
class SqliteConnectionResolverTests
{
    string contentRoot = null!;

    [SetUp]
    public void SetUp()
    {
        contentRoot = Path.Combine(Path.GetTempPath(), "symphony-sqlite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(contentRoot))
            Directory.Delete(contentRoot, recursive: true);
    }

    [Test]
    public void Resolve_RelativeDataSource_UsesContentRootAndCreatesDirectory()
    {
        var resolved = SqliteConnectionResolver.Resolve("Data Source=App_Data/symphony.db", contentRoot);

        var dataSource = new SqliteConnectionStringBuilder(resolved).DataSource;
        var expected = Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "symphony.db"));
        Assert.That(dataSource, Is.EqualTo(expected));
        Assert.That(Directory.Exists(Path.GetDirectoryName(expected)), Is.True);
    }

    [Test]
    public void Resolve_AbsoluteDataSource_KeepsPathAndCreatesDirectory()
    {
        var directory = Path.Combine(contentRoot, "absolute");
        var database = Path.Combine(directory, "symphony.db");

        var resolved = SqliteConnectionResolver.Resolve($"Data Source={database}", contentRoot);

        Assert.That(new SqliteConnectionStringBuilder(resolved).DataSource, Is.EqualTo(database));
        Assert.That(Directory.Exists(directory), Is.True);
    }

    [Test]
    public void Resolve_MemoryDataSource_LeavesDataSourceUnchanged()
    {
        var resolved = SqliteConnectionResolver.Resolve("Data Source=:memory:", contentRoot);

        Assert.That(new SqliteConnectionStringBuilder(resolved).DataSource, Is.EqualTo(":memory:"));
        Assert.That(Directory.GetFileSystemEntries(contentRoot), Is.Empty);
    }
}
