using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Symphony.Data;

namespace Symphony.Migrations.SqlServer;

public sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SymphonyDbContext>
{
    public SymphonyDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.FirstOrDefault()
            ?? "Server=localhost;Database=symphony_design;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<SymphonyDbContext>()
            .UseSqlServer(
                connectionString,
                sqlServer => sqlServer.MigrationsAssembly(typeof(SqlServerDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new SymphonyDbContext(options);
    }
}
