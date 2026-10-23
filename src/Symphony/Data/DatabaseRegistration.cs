using Microsoft.EntityFrameworkCore;

namespace Symphony.Data;

public static class DatabaseRegistration
{
    static string normalize(string provider) =>
        provider.Trim().ToLowerInvariant() switch
        {
            "postgres" or "postgresql" or "npgsql" => "npgsql",
            var value => value
        };

    static string require(IConfiguration configuration, string provider, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration.GetConnectionString(key);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        var names = string.Join("' or '", keys);
        throw new InvalidOperationException(
            $"Connection string '{names}' is required for Database:Provider '{provider}'.");
    }

    public static IServiceCollection AddSymphonyDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRoot)
    {
        var configured = configuration["Database:Provider"] ?? "sqlite";
        switch (normalize(configured))
        {
            case "sqlite":
                var sqlite = configuration.GetConnectionString("Sqlite");
                if (string.IsNullOrWhiteSpace(sqlite))
                    sqlite = "Data Source=App_Data/symphony.db";
                var resolved = SqliteConnectionResolver.Resolve(sqlite, contentRoot);
                services.AddDbContextFactory<SymphonyDbContext>(options =>
                    options.UseSqlite(resolved, sqlite => sqlite.MigrationsAssembly("Symphony.Migrations.Sqlite")));
                break;
            case "sqlserver":
                var sqlServer = require(configuration, configured, "SqlServer");
                services.AddDbContextFactory<SymphonyDbContext>(options =>
                    options.UseSqlServer(sqlServer, sql => sql.MigrationsAssembly("Symphony.Migrations.SqlServer")));
                break;
            case "npgsql":
                var postgres = require(configuration, configured, "PostgreSQL", "Npgsql");
                services.AddDbContextFactory<SymphonyDbContext>(options =>
                    options.UseNpgsql(postgres, npgsql => npgsql.MigrationsAssembly("Symphony.Migrations.PostgreSQL")));
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider '{configured}'.");
        }

        return services;
    }
}
