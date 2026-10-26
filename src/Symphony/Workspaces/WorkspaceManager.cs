using Symphony.Config;
using Symphony.Domain;
using Symphony.Repositories;
using Symphony.Security;
using Symphony.Utils;

namespace Symphony.Workspaces;

sealed class WorkspaceCloneException(string message) : Exception(message);

class WorkspaceManager
{
    static void cleanupTempArtifacts(string path)
    {
        var tmp = Path.Combine(path, "tmp");
        if (Directory.Exists(tmp))
            Directory.Delete(tmp, true);

        //var elixir = Path.Combine(path, ".elixir_ls");
        //if (Directory.Exists(elixir))
        //    Directory.Delete(elixir, true);
    }

    static bool hasCheckout(string path)
    {
        var gitEntry = Path.Combine(path, ".git");
        if (Directory.Exists(gitEntry))
            return File.Exists(Path.Combine(gitEntry, "HEAD"));

        return File.Exists(gitEntry);
    }

    static string quote(string value)
    {
        if (value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            throw new InvalidOperationException("Repository clone URL contains unsupported characters.");

        return $"\"{value}\"";
    }

    static string cloneCommand(RepositoryConfig repository) =>
        $"git clone --depth 1 --single-branch --no-tags {quote(repository.Url)} .";

    static Dictionary<string, string>? buildEnvironment(ServiceConfig config)
    {
        var repository = config.Repository;
        if (repository is null)
            return null;

        return GitHubEnvironment.Build(
            GitHubEnvironment.FromProcessEnvironment(),
            repository.Slug,
            repository.Token,
            repository.Identity);
    }

    readonly IServiceConfigProvider configProvider;
    readonly ShellCommandRunner shell;
    readonly ILogger<WorkspaceManager> logger;

    public WorkspaceManager(IServiceConfigProvider configProvider, ShellCommandRunner shell, ILogger<WorkspaceManager> logger)
    {
        this.configProvider = configProvider;
        this.shell = shell;
        this.logger = logger;
    }

    void resetDirectory(string path)
    {
        var entries = Directory.EnumerateFileSystemEntries(path).ToList();
        if (entries.Count == 0)
            return;

        logger.LogWarning("Clearing {Count} entries in {Path} before repository checkout.", entries.Count, path);
        foreach (var entry in entries)
        {
            if (Directory.Exists(entry))
                Directory.Delete(entry, true);
            else
                File.Delete(entry);
        }
    }

    async Task ensureCheckoutAsync(
        string path,
        ServiceConfig config,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken)
    {
        var repository = config.Repository;
        if (repository is null || hasCheckout(path))
            return;

        resetDirectory(path);

        var result = await shell.RunAsync(
            cloneCommand(repository),
            path,
            config.Workspace.CloneTimeoutMs,
            environment,
            cancellationToken);

        if (result.Success && hasCheckout(path))
            return;

        var detail = Redaction.RedactSecrets(result.StdErr, [repository.Token]).Trim();
        resetDirectory(path);
        throw new WorkspaceCloneException(string.IsNullOrWhiteSpace(detail)
            ? $"git clone failed for {repository.Slug}."
            : $"git clone failed for {repository.Slug}: {detail}");
    }

    public async Task<Workspace> CreateForIssueAsync(string identifier, CancellationToken cancellationToken)
    {
        var config = configProvider.GetConfig();
        var root = config.Workspace.Root;
        Directory.CreateDirectory(root);

        var workspaceKey = SanitizeKey(identifier);
        if (workspaceKey.Length == 0 || workspaceKey is "." or "..")
            throw new InvalidOperationException("Workspace identifier produced an invalid folder name.");

        var fullPath = WorkspacePath.EnsureContainedBy(root, Path.Combine(root, workspaceKey));

        var createdNow = false;
        if (File.Exists(fullPath) && !Directory.Exists(fullPath))
            throw new IOException($"Workspace path exists and is not a directory: {fullPath}");

        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
            createdNow = true;
        }

        cleanupTempArtifacts(fullPath);

        var environment = buildEnvironment(config);

        if (createdNow && !string.IsNullOrWhiteSpace(config.Hooks.AfterCreate))
        {
            var result = await shell.RunAsync(config.Hooks.AfterCreate, fullPath, config.Hooks.TimeoutMs, environment, cancellationToken);

            if (!result.Success)
            {
                var detail = Redaction.RedactSecrets(result.StdErr, [config.Repository?.Token]).Trim();
                if (config.Repository is null || hasCheckout(fullPath))
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                        ? "after_create hook failed."
                        : $"after_create hook failed: {detail}");

                try
                {
                    await ensureCheckoutAsync(fullPath, config, environment, cancellationToken);
                }
                catch (WorkspaceCloneException ex)
                {
                    throw new InvalidOperationException(
                        $"after_create hook failed and the repository checkout could not be completed: {ex.Message}",
                        ex);
                }

                logger.LogWarning("after_create hook failed; the repository checkout was completed by the platform.");
            }
        }

        await ensureCheckoutAsync(fullPath, config, environment, cancellationToken);

        return new Workspace(fullPath, workspaceKey, createdNow);
    }

    public async Task<bool> RunHookAsync(string? hook, string workspacePath, int timeoutMs, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hook))
            return true;

        var result = await shell.RunAsync(hook, workspacePath, timeoutMs, buildEnvironment(configProvider.GetConfig()), cancellationToken);
        return result.Success;
    }

    public async Task RunHookBestEffortAsync(string? hook, string workspacePath, int timeoutMs, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hook))
            return;

        var result = await shell.RunAsync(hook, workspacePath, timeoutMs, buildEnvironment(configProvider.GetConfig()), cancellationToken);

        if (!result.Success)
            logger.LogWarning("Hook failed (ignored): {Hook}", hook);
    }

    public async Task CleanupWorkspaceAsync(string identifier, CancellationToken cancellationToken)
    {
        var config = configProvider.GetConfig();
        var root = config.Workspace.Root;
        var workspaceKey = SanitizeKey(identifier);
        var path = WorkspacePath.EnsureContainedBy(root, Path.Combine(root, workspaceKey));

        if (!Directory.Exists(path))
            return;

        await RunHookBestEffortAsync(config.Hooks.BeforeRemove, path, config.Hooks.TimeoutMs, cancellationToken);

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove workspace {Path}", path);
        }
    }

    public static string SanitizeKey(string identifier)
    {
        var chars = identifier.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (!(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-'))
                chars[i] = '_';
        }

        return new string(chars);
    }
}
