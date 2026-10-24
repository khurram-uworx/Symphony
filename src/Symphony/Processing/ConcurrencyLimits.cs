using Microsoft.EntityFrameworkCore;
using Symphony.Data;

namespace Symphony.Processing;

static class ConcurrencyLimits
{
    public static async Task<int> GetGlobalConcurrencyAsync(IDbContextFactory<SymphonyDbContext> factory, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var setting = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(item => item.Key == "GlobalConcurrency", cancellationToken)
            ?? await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(item => item.Key == "MaxConcurrentAgents", cancellationToken);

        if (setting is not null && int.TryParse(setting.Value, out var value) && value > 0)
            return value;

        return 4;
    }

    public static int GetPerStateLimit(Config.ServiceConfig config, string state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return int.MaxValue;

        var key = state.Trim().ToLowerInvariant();
        if (config.Agent.MaxConcurrentByState.TryGetValue(key, out var limit) && limit > 0)
            return limit;

        return int.MaxValue;
    }
}
