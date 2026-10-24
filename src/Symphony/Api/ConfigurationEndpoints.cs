using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Symphony.Data;
using Symphony.Dto;
using Symphony.Security;
using Symphony.Services;

namespace Symphony.Api;

public static class ConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/configuration").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapGet("", GetAsync);
        group.MapPut("/", PutAsync);
        group.MapPut("", PutAsync);

        return endpoints;
    }

    public static IEndpointRouteBuilder MapConfigurationApi(this IEndpointRouteBuilder endpoints) =>
        MapConfigurationEndpoints(endpoints);

    public static IEndpointRouteBuilder Map(this IEndpointRouteBuilder endpoints) =>
        MapConfigurationEndpoints(endpoints);

    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder endpoints) =>
        MapConfigurationEndpoints(endpoints);

    public static IEndpointRouteBuilder RegisterEndpoints(this IEndpointRouteBuilder endpoints) =>
        MapConfigurationEndpoints(endpoints);

    public static IEndpointRouteBuilder MapApi(this IEndpointRouteBuilder endpoints) =>
        MapConfigurationEndpoints(endpoints);

    static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        ConfigurationService configurationService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var configuration = await configurationService.GetAsync(userId, cancellationToken);
        if (configuration is null)
            configuration = new UserConfiguration { UserId = userId };

        return Results.Ok(ConfigurationDto.From(configuration));
    }

    static async Task<IResult> PutAsync(
        ClaimsPrincipal user,
        ConfigurationDto request,
        ConfigurationService configurationService,
        ISecretProtector secretProtector,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        if (request is null)
            return Results.BadRequest(new { error = "Request body is required." });

        if (!request.IsValid(out var validationError))
            return Results.BadRequest(new { error = validationError });

        var existing = await configurationService.GetAsync(userId, cancellationToken);

        var configuration = request.ToUserConfiguration();
        configuration.UserId = userId;

        if (string.IsNullOrWhiteSpace(request.TrackerApiKey) ||
            string.Equals(request.TrackerApiKey, Redaction.Redacted, StringComparison.Ordinal))
        {
            configuration.TrackerApiKey = existing?.TrackerApiKey ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(request.CopilotApiKey) ||
            string.Equals(request.CopilotApiKey, Redaction.Redacted, StringComparison.Ordinal))
        {
            configuration.CopilotApiKey = existing?.CopilotApiKey;
        }

        var saved = await configurationService.SaveAsync(userId, configuration, cancellationToken);
        return Results.Ok(ConfigurationDto.From(saved));
    }

    static string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? user.Identity?.Name;
}