using System.Text;

namespace Symphony.Repositories;

public static class GitHubEnvironment
{
    const string ConfigKeyPrefix = "GIT_CONFIG_KEY_";

    public static Dictionary<string, string> FromProcessEnvironment()
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is null)
                continue;

            environment[entry.Key.ToString()!] = entry.Value?.ToString() ?? string.Empty;
        }

        return environment;
    }

    public static Dictionary<string, string> Build(
        Dictionary<string, string>? baseEnvironment,
        string? repositorySlug,
        string? token,
        GitHubIdentity? identity = null)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (baseEnvironment is not null)
        {
            foreach (var pair in baseEnvironment)
                if (pair.Key is not null && pair.Value is not null)
                    environment[pair.Key] = pair.Value;
        }

        if (!string.IsNullOrWhiteSpace(repositorySlug))
            environment["GITHUB_REPOSITORY"] = repositorySlug;

        environment["GIT_TERMINAL_PROMPT"] = "0";
        environment["GH_PROMPT_DISABLED"] = "1";
        environment["GH_NO_UPDATE_NOTIFIER"] = "1";

        if (identity is not null)
        {
            environment["GIT_AUTHOR_NAME"] = identity.Name;
            environment["GIT_AUTHOR_EMAIL"] = identity.Email;
            environment["GIT_COMMITTER_NAME"] = identity.Name;
            environment["GIT_COMMITTER_EMAIL"] = identity.Email;
        }

        if (string.IsNullOrWhiteSpace(token))
            return environment;

        environment["GH_TOKEN"] = token;
        environment["GITHUB_TOKEN"] = token;

        // Avoid embedding the token in a clone URL. Use git's extraheader so it never touches .git/config.
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"x-access-token:{token}"));
        var index = nextConfigIndex(environment);
        environment[ConfigKeyPrefix + index] = "http.https://github.com/.extraheader";
        environment["GIT_CONFIG_VALUE_" + index] = $"Authorization: Basic {auth}";
        environment["GIT_CONFIG_COUNT"] = (index + 1).ToString();
        return environment;
    }

    static int nextConfigIndex(Dictionary<string, string> environment)
    {
        var highest = -1;
        foreach (var key in environment.Keys)
        {
            if (!key.StartsWith(ConfigKeyPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (int.TryParse(key[ConfigKeyPrefix.Length..], out var index) && index > highest)
                highest = index;
        }

        var count = 0;
        if (environment.TryGetValue("GIT_CONFIG_COUNT", out var raw))
            int.TryParse(raw, out count);

        return Math.Max(count, highest + 1);
    }
}
