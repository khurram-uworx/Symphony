namespace Symphony.Repositories;

public sealed record GitHubIdentity(string Name, string Email)
{
    public const string FallbackName = "Symphony Agent";

    public static GitHubIdentity Fallback(string? userName)
    {
        var name = string.IsNullOrWhiteSpace(userName) ? FallbackName : userName.Trim();
        var slug = new string(name
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
            .ToArray())
            .ToLowerInvariant();

        if (slug.Length == 0)
            slug = "symphony";

        return new GitHubIdentity(name, $"{slug}@users.noreply.github.com");
    }
}
