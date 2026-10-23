using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Symphony.Data;

namespace Symphony.Migrations.PostgreSQL;

public sealed class PostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<SymphonyDbContext>
{
    public SymphonyDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.FirstOrDefault()
            ?? "Host=localhost;Database=symphony_design;Username=symphony;Password=design-time-only";
        var options = new DbContextOptionsBuilder<SymphonyDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(PostgreSqlDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new SymphonyDbContext(options);
    }
}
