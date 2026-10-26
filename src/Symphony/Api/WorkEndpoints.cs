using Symphony.Dto;
using Symphony.Processing;
using Symphony.Services;
using System.Security.Claims;

namespace Symphony.Api;

public static class WorkEndpoints
{
    public static IEndpointRouteBuilder MapWorkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/work", CreateAsync).RequireRateLimiting(ApiRateLimitPolicy.PolicyName).DisableCookieRedirect();
        return endpoints;
    }

    public static IEndpointRouteBuilder MapWorkApi(this IEndpointRouteBuilder endpoints) =>
        MapWorkEndpoints(endpoints);

    public static IEndpointRouteBuilder MapWork(this IEndpointRouteBuilder endpoints) =>
        MapWorkEndpoints(endpoints);

    static async Task<IResult> CreateAsync(
        ClaimsPrincipal user,
        WorkRequest? request,
        JobService jobService,
        JobQueue? jobQueue,
        ConfigurationService? configurationService = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        if (request?.GitHub is not null && string.IsNullOrWhiteSpace(request.GitHub.PersonalAccessToken) &&
            configurationService is not null)
        {
            var configuration = await configurationService.GetAsync(userId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(configuration?.GitHubPersonalAccessToken))
                request.GitHub.PersonalAccessToken = configuration.GitHubPersonalAccessToken;
        }

        if (request?.GitHub is not null && string.IsNullOrWhiteSpace(request.GitHub.PersonalAccessToken))
            return Results.BadRequest(new { error = "GitHub personal access token is required. Configure it on Account/Configuration or provide it in the request." });

        if (!WorkRequest.IsValid(request, out var validationError))
            return Results.BadRequest(new { error = validationError });

        try
        {
            var job = await jobService.CreateAsync(userId, request!, cancellationToken);

            try { await (jobQueue?.SignalInstanceAsync(cancellationToken) ?? Task.CompletedTask); }
            catch { /* best-effort */ }

            var statusUrl = $"/api/jobs/{job.Id}";
            return Results.Accepted(statusUrl, new { jobId = job.Id, statusUrl });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
    }

    static string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? user.Identity?.Name;
}