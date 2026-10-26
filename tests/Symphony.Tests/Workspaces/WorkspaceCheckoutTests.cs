using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Symphony.Config;
using Symphony.Repositories;
using Symphony.Utils;
using Symphony.Workflows;
using Symphony.Workspaces;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Workspaces;

[TestFixture, Category(TestCatory.Unit)]
class WorkspaceCheckoutTests
{
    const string Token = "ghp_checkoutTestToken1234567890";

    string root = string.Empty;

    [SetUp]
    public void SetUp() => root = Path.Combine(Path.GetTempPath(), "symphony-checkout-tests", Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (!Directory.Exists(root))
            return;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task CreateForIssueAsync_RepositoryWithoutCheckout_ClonesShallowWithRepositoryEnvironment()
    {
        var runner = new ScriptedShellRunner((_, directory) =>
        {
            seedCheckout(directory);
            return new CommandResult(true, 0, string.Empty, string.Empty);
        });

        var workspace = await createManager(runner, repository: createRepository())
            .CreateForIssueAsync("ISSUE-1", CancellationToken.None);

        var environment = runner.Environments[0]!;
        Assert.That(runner.Commands, Has.Count.EqualTo(1));
        Assert.That(runner.Commands[0], Does.StartWith("git clone --depth 1 --single-branch --no-tags"));
        Assert.That(runner.Commands[0], Does.Contain("https://github.com/owner/repo.git"));
        Assert.That(environment["GH_TOKEN"], Is.EqualTo(Token));
        Assert.That(environment["GITHUB_REPOSITORY"], Is.EqualTo("owner/repo"));
        Assert.That(environment["GIT_TERMINAL_PROMPT"], Is.EqualTo("0"));
        Assert.That(environment["GIT_AUTHOR_EMAIL"], Is.EqualTo("agent@users.noreply.github.com"));
        Assert.That(workspace.CreatedNow, Is.True);
    }

    [Test]
    public async Task CreateForIssueAsync_ExistingCheckout_SkipsClone()
    {
        seedCheckout(Path.Combine(root, "ISSUE-1"));
        var runner = new ScriptedShellRunner((_, _) => new CommandResult(true, 0, string.Empty, string.Empty));

        var workspace = await createManager(runner, repository: createRepository())
            .CreateForIssueAsync("ISSUE-1", CancellationToken.None);

        Assert.That(runner.Commands, Is.Empty);
        Assert.That(workspace.CreatedNow, Is.False);
    }

    [Test]
    public async Task CreateForIssueAsync_LegacyCloneHook_RunsOnceAndIsNotRepeatedByThePlatform()
    {
        var runner = new ScriptedShellRunner((command, directory) =>
        {
            if (!command.StartsWith("git clone", StringComparison.Ordinal))
                return new CommandResult(true, 0, string.Empty, string.Empty);

            seedCheckout(directory);
            File.WriteAllText(Path.Combine(directory, "hook.marker"), "hook");
            return new CommandResult(true, 0, string.Empty, string.Empty);
        });

        var hooks = new HooksConfig("git clone \"${GITHUB_REPOSITORY}\" .", null, null, null, 60000);

        await createManager(runner, createRepository(), hooks).CreateForIssueAsync("ISSUE-1", CancellationToken.None);

        Assert.That(runner.Commands, Has.Count.EqualTo(1));
        Assert.That(File.Exists(Path.Combine(root, "ISSUE-1", "hook.marker")), Is.True);
    }

    [Test]
    public async Task CreateForIssueAsync_FailingCreateHook_FallsBackToPlatformClone()
    {
        var runner = new ScriptedShellRunner((command, directory) =>
        {
            if (command == "broken-hook")
                return new CommandResult(false, 1, string.Empty, "hook exploded");

            seedCheckout(directory);
            return new CommandResult(true, 0, string.Empty, string.Empty);
        });

        var hooks = new HooksConfig("broken-hook", null, null, null, 60000);

        await createManager(runner, createRepository(), hooks).CreateForIssueAsync("ISSUE-1", CancellationToken.None);

        Assert.That(runner.Commands, Has.Count.EqualTo(2));
        Assert.That(runner.Commands[1], Does.StartWith("git clone"));
        Assert.That(Directory.Exists(Path.Combine(root, "ISSUE-1", ".git")), Is.True);
    }

    [Test]
    public async Task CreateForIssueAsync_CloneFails_ThrowsCloneExceptionWithRedactedStderrAndClearsWorkspace()
    {
        var runner = new ScriptedShellRunner((_, directory) =>
        {
            File.WriteAllText(Path.Combine(directory, "partial.txt"), "partial");
            return new CommandResult(false, 128, string.Empty, $"fatal: could not read from {Token}");
        });

        var exception = await Assert.ThrowsAsync<WorkspaceCloneException>(async () =>
            await createManager(runner, repository: createRepository()).CreateForIssueAsync("ISSUE-1", CancellationToken.None));

        var directory = Path.Combine(root, "ISSUE-1");
        Assert.That(exception!.Message, Does.StartWith("git clone failed for owner/repo"));
        Assert.That(exception.Message, Does.Contain("***"));
        Assert.That(exception.Message, Does.Not.Contain(Token));
        Assert.That(Directory.EnumerateFileSystemEntries(directory), Is.Empty);
    }

    [Test]
    public async Task CreateForIssueAsync_LocalBareRepository_ProducesShallowCheckoutAndKeepsExistingWork()
    {
        Assume.That(hasGit(), "git must be on PATH to run this test.");

        var shell = new ShellCommandRunner(NullLogger<ShellCommandRunner>.Instance);
        var bareRepository = await createBareRepositoryAsync(shell);
        var repository = createRepository(cloneUrl: new Uri(bareRepository).AbsoluteUri);

        var manager = createManager(new ShellCommandRunner(NullLogger<ShellCommandRunner>.Instance), repository);

        var workspace = await manager.CreateForIssueAsync("ISSUE-1", CancellationToken.None);
        var readme = Path.Combine(workspace.Path, "README.md");
        var shallow = await shell.RunAsync(
            $"git -C \"{workspace.Path}\" rev-parse --is-shallow-repository",
            root,
            60000);

        Assert.That(File.ReadAllText(readme), Is.EqualTo("hello"));
        Assert.That(shallow.StdOut.Trim(), Is.EqualTo("true"));

        File.WriteAllText(Path.Combine(workspace.Path, "agent.txt"), "work");
        await manager.CreateForIssueAsync("ISSUE-1", CancellationToken.None);

        Assert.That(File.Exists(Path.Combine(workspace.Path, "agent.txt")), Is.True);
    }

    async Task<string> createBareRepositoryAsync(ShellCommandRunner shell)
    {
        var origin = Path.Combine(root, "origin");
        var bare = Path.Combine(root, "origin.git");

        Directory.CreateDirectory(origin);
        File.WriteAllText(Path.Combine(origin, "README.md"), "hello");

        await run("git init -q");
        await run("git config user.name \"Symphony Test\"");
        await run("git config user.email \"test@example.com\"");
        await run("git add README.md");
        await run("git commit -q -m init");
        await run($"git clone -q --bare \"{origin}\" \"{bare}\"");

        return bare;

        async Task run(string command)
        {
            var result = await shell.RunAsync(command, origin, 60000);
            Assert.That(result.Success, Is.True, $"{command}: {result.StdErr}");
        }
    }

    static void seedCheckout(string directory)
    {
        var git = Path.Combine(directory, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main");
    }

    static bool hasGit()
    {
        var names = OperatingSystem.IsWindows() ? new[] { "git.exe", "git.cmd" } : new[] { "git" };
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return paths.Any(path => names.Any(name => File.Exists(Path.Combine(path, name))));
    }

    static RepositoryConfig createRepository(string? cloneUrl = null) =>
        new(
            new GitHubRepository("owner", "repo"),
            Token,
            new GitHubIdentity("Agent", "agent@users.noreply.github.com"),
            cloneUrl);

    WorkspaceManager createManager(ShellCommandRunner runner, RepositoryConfig? repository = null, HooksConfig? hooks = null)
    {
        var config = new ServiceConfig(
            new WorkflowDefinition(new Dictionary<string, object>(), string.Empty),
            new TrackerConfig(TrackerConfig.None, string.Empty, string.Empty, string.Empty, [], []),
            new WorkspaceConfig(root),
            hooks ?? new HooksConfig(null, null, null, null, 60000),
            new AgentConfig(1, 300000, 1, new Dictionary<string, int>()),
            null,
            null,
            repository);

        return new WorkspaceManager(
            new StaticServiceConfigProvider(config),
            runner,
            NullLogger<WorkspaceManager>.Instance);
    }

    sealed class ScriptedShellRunner(Func<string, string, CommandResult> respond) : ShellCommandRunner(NullLogger<ShellCommandRunner>.Instance)
    {
        public List<string> Commands { get; } = [];
        public List<IReadOnlyDictionary<string, string>?> Environments { get; } = [];

        public override Task<CommandResult> RunAsync(
            string command,
            string workingDirectory,
            int timeoutMs,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken)
        {
            Commands.Add(command);
            Environments.Add(environment);
            return Task.FromResult(respond(command, workingDirectory));
        }
    }
}
