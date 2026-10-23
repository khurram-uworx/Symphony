using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Symphony.Data;

namespace Symphony.Migrations.Sqlite;

public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SymphonyDbContext>
{
    public SymphonyDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.FirstOrDefault() ?? "Data Source=symphony.design.db";
        var options = new DbContextOptionsBuilder<SymphonyDbContext>()
            .UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsAssembly(typeof(SqliteDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new SymphonyDbContext(options);
    }
}
