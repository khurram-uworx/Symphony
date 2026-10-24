using Microsoft.EntityFrameworkCore;
using Symphony.Services;

namespace Symphony.Data;

public static class DatabaseInitializer
{
    static readonly KeyValuePair<string, string>[] DefaultAppSettings =
    [
        new("GlobalConcurrency", "10"),
        new("MaxConcurrentAgents", "10"),
        new("DefaultWorkspaceRoot", "workspaces"),
        new("WorkspaceRoot", "workspaces"),
        new("MaxRetryBackoffMs", "300000"),
        new("MaxRetryBackoff", "300000")
    ];

    public static async Task InitializeAsync(
        IDbContextFactory<SymphonyDbContext> factory,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        await SeedAppSettingsAsync(db, cancellationToken);
        await SeedDefaultWorkflowsAsync(db, cancellationToken);
    }

    static async Task SeedAppSettingsAsync(SymphonyDbContext db, CancellationToken cancellationToken)
    {
        var keys = await db.AppSettings
            .Select(setting => setting.Key)
            .ToListAsync(cancellationToken);

        foreach (var (key, value) in DefaultAppSettings)
        {
            if (keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                continue;

            db.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = value
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    static async Task SeedDefaultWorkflowsAsync(SymphonyDbContext db, CancellationToken cancellationToken)
    {
        var userIds = await db.Users
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
            await WorkflowService.EnsureDefaultWorkflowAsync(db, userId, cancellationToken);
    }
}