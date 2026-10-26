using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Symphony.Repositories;

public sealed class GitHubIdentityResolver
{
    static readonly HttpClient sharedClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    static readonly Dictionary<string, GitHubIdentity> cache = new(StringComparer.Ordinal);

    readonly HttpClient client;

    public GitHubIdentityResolver(HttpClient? client = null) =>
        this.client = client ?? sharedClient;

    public async Task<GitHubIdentity> ResolveAsync(
        string? token,
        string? fallbackUserName,
        CancellationToken cancellationToken = default)
    {
        var fallback = GitHubIdentity.Fallback(fallbackUserName);
        if (string.IsNullOrWhiteSpace(token))
            return fallback;

        var key = cacheKey(token);
        lock (cache)
        {
            if (cache.TryGetValue(key, out var cached))
                return cached;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.UserAgent.ParseAdd("Symphony");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return fallback;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            var login = readString(root, "login");
            if (string.IsNullOrWhiteSpace(login))
                return fallback;

            var name = readString(root, "name");
            var identity = new GitHubIdentity(
                string.IsNullOrWhiteSpace(name) ? login! : name!.Trim(),
                $"{login}@users.noreply.github.com");

            lock (cache)
                cache[key] = identity;

            return identity;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return fallback;
        }
    }

    static string? readString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var node) && node.ValueKind == JsonValueKind.String
            ? node.GetString()
            : null;

    static string cacheKey(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
