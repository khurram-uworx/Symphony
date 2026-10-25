using Microsoft.EntityFrameworkCore;
using Symphony.Config;
using Symphony.Data;
using Symphony.Workspaces;

namespace Symphony.Services;

public sealed class SystemWorkspaceRootProvider(
    IDbContextFactory<SymphonyDbContext> dbFactory,
    ILogger<SystemWorkspaceRootProvider>? logger = null)
{
    readonly IDbContextFactory<SymphonyDbContext> dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    readonly ILogger<SystemWorkspaceRootProvider> logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SystemWorkspaceRootProvider>.Instance;

    public const string EnvironmentVariableName = "SYMPHONY_WORKSPACE_ROOT";
    public const string SettingKey = "SystemWorkspaceRoot";
    public const string DefaultTemplate = "%TEMP%/Symphony";

    public async Task<string> GetAsync(CancellationToken cancellationToken = default)
    {
        var envValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            if (TryResolve(envValue, out var root))
                return root;
            logger.LogWarning("Environment variable {Name} value '{Value}' is invalid; falling back.", EnvironmentVariableName, envValue);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var setting = await db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == SettingKey, cancellationToken);

        if (setting is not null && !string.IsNullOrWhiteSpace(setting.Value))
        {
            if (TryResolve(setting.Value, out var root))
                return root;
            logger.LogWarning("AppSetting {Key} value '{Value}' is invalid; falling back.", SettingKey, setting.Value);
        }

        return WorkspacePath.RequireAbsoluteRoot(
            ConfigReader.ResolvePath(RootDefault()));
    }

    static string RootDefault() => Path.Combine(Path.GetTempPath(), "Symphony");

    static bool TryResolve(string raw, out string root)
    {
        root = string.Empty;
        try
        {
            var expanded = Expand(raw);
            root = WorkspacePath.RequireAbsoluteRoot(ConfigReader.ResolvePath(expanded));
            return true;
        }
        catch
        {
            return false;
        }
    }

    static string Expand(string raw)
    {
        var text = raw.Trim();
        if (text.Contains("%TEMP%", StringComparison.OrdinalIgnoreCase))
            text = text.Replace("%TEMP%", Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

        text = Environment.ExpandEnvironmentVariables(text);
        return text;
    }
}