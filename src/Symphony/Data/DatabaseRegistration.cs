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

    public static IServiceCollection AddSymphonyDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRoot)
    {
        var configured = configuration["Database:Provider"] ?? "sqlite";
        switch (normalize(configured))
        {
            case "sqlite":
                var connectionString = configuration.GetConnectionString("Sqlite")
                    ?? "Data Source=App_Data/symphony.db";
                var resolved = SqliteConnectionResolver.Resolve(connectionString, contentRoot);
                services.AddDbContextFactory<SymphonyDbContext>(options => options.UseSqlite(resolved));
                break;
            case "sqlserver":
            case "npgsql":
                throw new InvalidOperationException(
                    $"Database provider '{configured}' is not supported in this build. Only sqlite is available.");
            default:
                throw new InvalidOperationException($"Unsupported database provider '{configured}'.");
        }

        return services;
    }
}
