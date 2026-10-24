using System.Text.RegularExpressions;

namespace Symphony.Repositories;

public sealed record GitHubRepository(string Owner, string Name)
{
    const string GitHubHost = "github.com";
    const string SshPrefix = "git@github.com:";
    static readonly Regex OwnerPattern = new("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$", RegexOptions.Compiled);
    static readonly Regex NamePattern = new("^[A-Za-z0-9._-]{1,100}$", RegexOptions.Compiled);

    public string Slug => $"{Owner}/{Name}";

    public string CloneUrl => $"https://{GitHubHost}/{Slug}.git";

    public override string ToString() => Slug;

    public static bool TryParse(string? input, out GitHubRepository? repository, out string? error)
    {
        repository = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "GitHub repository is required.";
            return false;
        }

        var value = input.Trim();
        string path;

        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Host, GitHubHost, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                error = "GitHub repository must be an owner/name, a github.com HTTPS URL, or a git@github.com SSH address.";
                return false;
            }

            path = uri.AbsolutePath;
        }
        else if (value.StartsWith(SshPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = value[SshPrefix.Length..];
        }
        else if (value.Contains("://", StringComparison.Ordinal) || value.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            error = "GitHub repository must be an owner/name, a github.com HTTPS URL, or a git@github.com SSH address.";
            return false;
        }
        else
        {
            path = value;
        }

        path = path.Trim('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            path = path[..^4];

        var segments = path.Split('/');
        if (segments.Length != 2)
        {
            error = "GitHub repository must contain exactly an owner and a repository name.";
            return false;
        }

        var owner = segments[0];
        var name = segments[1];

        if (!OwnerPattern.IsMatch(owner))
        {
            error = "GitHub repository owner is not a valid GitHub account name.";
            return false;
        }

        if (!NamePattern.IsMatch(name) || name is "." or "..")
        {
            error = "GitHub repository name contains unsupported characters.";
            return false;
        }

        repository = new GitHubRepository(owner, name);
        return true;
    }

    public static string? Canonicalize(string? input) =>
        TryParse(input, out var repository, out _) ? repository!.Slug : null;
}
