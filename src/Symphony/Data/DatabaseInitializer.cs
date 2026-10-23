using Microsoft.EntityFrameworkCore;

namespace Symphony.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}
