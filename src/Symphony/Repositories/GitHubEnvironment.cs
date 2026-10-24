using System.Text;

namespace Symphony.Repositories;

public static class GitHubEnvironment
{
    public static Dictionary<string, string> Build(Dictionary<string, string>? baseEnvironment, string repositorySlug, string? token)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (baseEnvironment is not null)
        {
            foreach (var pair in baseEnvironment)
                if (pair.Key is not null && pair.Value is not null)
                    environment[pair.Key] = pair.Value;
        }

        environment["GITHUB_REPOSITORY"] = repositorySlug;

        if (string.IsNullOrWhiteSpace(token))
            return environment;

        environment["GH_TOKEN"] = token;
        environment["GITHUB_TOKEN"] = token;
        environment["GIT_TERMINAL_PROMPT"] = "0";

        // Avoid embedding the token in a clone URL. Use git's extraheader so it never touches .git/config.
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"x-access-token:{token}"));
        environment["GIT_CONFIG_COUNT"] = "1";
        environment["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader";
        environment["GIT_CONFIG_VALUE_0"] = $"Authorization: Basic {auth}";
        return environment;
    }
}
