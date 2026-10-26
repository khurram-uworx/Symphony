namespace Symphony.Workspaces;

public static class WorkspacePath
{
    static StringComparison comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string RequireAbsoluteRoot(string? root, string parameterName = "root")
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Workspace root is required.", parameterName);

        if (!Path.IsPathFullyQualified(root))
            throw new ArgumentException("Workspace root must be an absolute path or start with '~'.", parameterName);

        var full = Path.GetFullPath(root);
        var filesystemRoot = Path.GetPathRoot(full);
        if (filesystemRoot is not null &&
            string.Equals(
                Path.TrimEndingDirectorySeparator(full),
                Path.TrimEndingDirectorySeparator(filesystemRoot),
                comparison))
        {
            throw new ArgumentException("Workspace root cannot be a filesystem root.", parameterName);
        }

        return full;
    }

    public static string EnsureContainedBy(string root, string path)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var separator = Path.DirectorySeparatorChar.ToString();

        if (string.Equals(fullPath, fullRoot, comparison))
            return fullPath;

        if (!fullPath.StartsWith(fullRoot + separator, comparison))
            throw new InvalidOperationException("Workspace path escapes configured root.");

        return fullPath;
    }

    public static string UserScopedRoot(string baseRoot, string ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("Owner user ID is required.", nameof(ownerUserId));

        if (ownerUserId is "." or ".." ||
            ownerUserId.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\']) >= 0)
        {
            throw new ArgumentException("Owner user ID must not contain path separators.", nameof(ownerUserId));
        }

        var root = RequireAbsoluteRoot(baseRoot);
        return EnsureContainedBy(root, Path.Combine(root, ownerUserId));
    }

    internal static string CopilotHomeFor(string userRoot) =>
        EnsureContainedBy(userRoot, Path.Combine(userRoot, ".copilot"));
}
