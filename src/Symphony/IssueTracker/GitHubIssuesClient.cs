using System.Net.Http.Headers;
using System.Text.Json;
using Symphony.Config;
using Symphony.Domain;

namespace Symphony.IssueTracker;

sealed class GitHubIssuesClient : IIssueTracker
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    readonly IHttpClientFactory httpClientFactory;
    readonly ILogger<GitHubIssuesClient> logger;
    readonly ServiceConfig config;

    public GitHubIssuesClient(IHttpClientFactory httpClientFactory, ILogger<GitHubIssuesClient> logger, ServiceConfig config)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<IReadOnlyList<Issue>> FetchIssueStatesByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
            return Array.Empty<Issue>();

        var token = config.Tracker.ApiKey;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("GitHub tracker api key is not configured.");

        var (owner, repo) = ParseRepository(config.Tracker.ProjectSlug);

        var tasks = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => FetchIssueAsync(owner, repo, token, id.Trim(), cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        return results.Where(issue => issue is not null).Cast<Issue>().ToList();
    }

    async Task<Issue?> FetchIssueAsync(string owner, string repository, string token, string id, CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildIssueUri(config.Tracker.Endpoint, owner, repository, id));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Headers.UserAgent.ParseAdd("Symphony");

            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GitHub issue fetch failed for {Id}: {Status} {Reason}", id, (int)response.StatusCode, response.ReasonPhrase);
                throw new InvalidOperationException($"GitHub API error: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            using var document = JsonDocument.Parse(content);
            var element = document.RootElement;
            if (element.TryGetProperty("pull_request", out _))
            {
                logger.LogInformation("Skipping pull request {Id}.", id);
                return null;
            }

            return ParseIssue(element, id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Failed to fetch GitHub issue {Id}.", id);
            throw;
        }
    }

    static Issue ParseIssue(JsonElement element, string fallbackId)
    {
        var number = element.TryGetProperty("number", out var numberElement) && numberElement.ValueKind == JsonValueKind.Number
            ? numberElement.GetInt32().ToString()
            : fallbackId;

        var id = string.IsNullOrWhiteSpace(number) ? fallbackId : number;

        var labels = new List<string>();
        if (element.TryGetProperty("labels", out var labelsElement) && labelsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var label in labelsElement.EnumerateArray())
            {
                if (label.ValueKind == JsonValueKind.Object &&
                    label.TryGetProperty("name", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    var name = nameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        labels.Add(name.Trim().ToLowerInvariant());
                }
            }
        }

        DateTimeOffset? createdAt = ParseDate(element, "created_at");
        DateTimeOffset? updatedAt = ParseDate(element, "updated_at");

        return new Issue(
            id,
            id,
            element.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String
                ? titleElement.GetString() ?? string.Empty
                : string.Empty,
            element.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String
                ? bodyElement.GetString()
                : null,
            null,
            element.TryGetProperty("state", out var stateElement) && stateElement.ValueKind == JsonValueKind.String
                ? stateElement.GetString() ?? string.Empty
                : string.Empty,
            null,
            element.TryGetProperty("html_url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String
                ? urlElement.GetString()
                : null,
            labels,
            Array.Empty<BlockerRef>(),
            createdAt,
            updatedAt);
    }

    static DateTimeOffset? ParseDate(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var dateElement) &&
            dateElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(dateElement.GetString(), out var date))
        {
            return date;
        }

        return null;
    }

    static (string Owner, string Repository) ParseRepository(string projectSlug)
    {
        if (string.IsNullOrWhiteSpace(projectSlug))
            throw new InvalidOperationException("GitHub tracker project slug is not configured.");

        var segments = projectSlug.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
            throw new InvalidOperationException("GitHub tracker project slug must be in owner/repo form.");

        return (segments[0], segments[1]);
    }

    static Uri BuildIssueUri(string endpoint, string owner, string repository, string id)
    {
        var baseEndpoint = string.IsNullOrWhiteSpace(endpoint)
            ? "https://api.github.com"
            : endpoint.TrimEnd('/');

        return new Uri($"{baseEndpoint}/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/issues/{Uri.EscapeDataString(id)}");
    }

    HttpClient CreateClient() => httpClientFactory.CreateClient();
}