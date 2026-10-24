using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Symphony.Config;
using Symphony.Utils;
using Symphony.Workflows;
using Symphony.Workspaces;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Workspaces;

[TestFixture, Category(TestCatory.Unit)]
class WorkspacePathSafetyTests
{
    static string Root => Path.Combine(Path.GetTempPath(), "symphony-path-tests");

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }

    [Test]
    public void RequireAbsoluteRoot_RelativePath_Throws()
    {
        Assert.That(
            () => WorkspacePath.RequireAbsoluteRoot("workspaces"),
            Throws.ArgumentException.With.Message.Contains("absolute"));
    }

    [Test]
    public void RequireAbsoluteRoot_FileSystemRoot_Throws()
    {
        var filesystemRoot = Path.GetPathRoot(Path.GetFullPath(Root))!;
        Assert.That(
            () => WorkspacePath.RequireAbsoluteRoot(filesystemRoot),
            Throws.ArgumentException.With.Message.Contains("filesystem root"));
    }

    [Test]
    public void RequireAbsoluteRoot_AbsolutePath_ReturnsFullPath()
    {
        var result = WorkspacePath.RequireAbsoluteRoot(Path.Combine(Root, "nested"));
        Assert.That(result, Is.EqualTo(Path.GetFullPath(Path.Combine(Root, "nested"))));
    }

    [Test]
    public void EnsureContainedBy_SiblingPrefix_Throws()
    {
        var root = Path.Combine(Root, "user1");
        var sibling = Path.Combine(Root, "user10");

        Assert.That(
            () => WorkspacePath.EnsureContainedBy(root, sibling),
            Throws.InvalidOperationException.With.Message.Contains("escapes"));
    }

    [Test]
    public void EnsureContainedBy_ParentTraversal_Throws()
    {
        var root = Path.Combine(Root, "user1");

        Assert.That(
            () => WorkspacePath.EnsureContainedBy(root, Path.Combine(root, "..", "user2")),
            Throws.InvalidOperationException.With.Message.Contains("escapes"));
    }

    [Test]
    public void EnsureContainedBy_NestedPath_ReturnsFullPath()
    {
        var root = Path.Combine(Root, "user1");
        var nested = Path.Combine(root, "ISSUE-1");

        Assert.That(WorkspacePath.EnsureContainedBy(root, nested), Is.EqualTo(Path.GetFullPath(nested)));
    }

    [Test]
    public void UserScopedRoot_TwoOwners_ProduceDifferentRoots()
    {
        var first = WorkspacePath.UserScopedRoot(Root, "user-1");
        var second = WorkspacePath.UserScopedRoot(Root, "user-2");

        Assert.That(first, Is.Not.EqualTo(second));
        Assert.That(first, Does.EndWith(Path.Combine("user-1")));
        Assert.That(second, Does.EndWith(Path.Combine("user-2")));
    }

    [Test]
    public void UserScopedRoot_SeparatorInOwnerId_Throws()
    {
        Assert.That(
            () => WorkspacePath.UserScopedRoot(Root, "..\\other"),
            Throws.ArgumentException.With.Message.Contains("separators"));
    }

    [Test]
    public void CreateForIssueAsync_EscapingIdentifier_Throws()
    {
        var manager = createManager(Path.Combine(Root, "user-1"));

        Assert.That(
            async () => await manager.CreateForIssueAsync("..", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("invalid folder name"));
    }

    [Test]
    public void CleanupWorkspaceAsync_EscapingIdentifier_Throws()
    {
        var manager = createManager(Path.Combine(Root, "user-1"));

        Assert.That(
            async () => await manager.CleanupWorkspaceAsync("..", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("escapes"));
    }

    static WorkspaceManager createManager(string root)
    {
        var config = new ServiceConfig(
            new WorkflowDefinition(new Dictionary<string, object>(), string.Empty),
            new TrackerConfig(TrackerConfig.None, string.Empty, string.Empty, string.Empty, [], []),
            new WorkspaceConfig(root),
            new HooksConfig(null, null, null, null, 60000),
            new AgentConfig(1, 300000, 1, new Dictionary<string, int>()),
            null,
            null);

        return new WorkspaceManager(
            new StaticServiceConfigProvider(config),
            new ShellCommandRunner(NullLogger<ShellCommandRunner>.Instance),
            NullLogger<WorkspaceManager>.Instance);
    }
}
