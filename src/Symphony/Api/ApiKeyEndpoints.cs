using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Symphony.Services;

namespace Symphony.Api;

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/api-keys").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, ApiKeyService service, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var keys = await service.ListAsync(userId, ct);
            var result = keys.Select(k => new ApiKeyDto(k.Id, k.Name, k.KeyPrefix, k.IsEnabled, k.CreatedAt, k.ExpiresAt, k.RevokedAt, k.LastUsedAt)).ToList();
            return Results.Ok(result);
        });

        group.MapPost("/", async (CreateApiKeyRequest req, ClaimsPrincipal user, ApiKeyService service, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var (rawKey, metadata) = await service.CreateAsync(userId, req.Name, req.ExpiresAt, ct);
            return Results.Ok(new CreateApiKeyResponse(metadata.Id, metadata.Name, metadata.KeyPrefix, rawKey, metadata.CreatedAt, metadata.ExpiresAt));
        });

        group.MapPost("/{id}/rotate", async (string id, ClaimsPrincipal user, ApiKeyService service, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await service.RotateAsync(userId, id, ct);
            if (result is null) return Results.NotFound();
            var (rawKey, metadata) = result.Value;
            return Results.Ok(new CreateApiKeyResponse(metadata.Id, metadata.Name, metadata.KeyPrefix, rawKey, metadata.CreatedAt, metadata.ExpiresAt));
        });

        group.MapPatch("/{id}", async (string id, UpdateApiKeyRequest req, ClaimsPrincipal user, ApiKeyService service, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (req.Revoke == true)
            {
                var revoked = await service.RevokeAsync(userId, id, ct);
                return revoked ? Results.Ok() : Results.NotFound();
            }
            if (req.Enabled.HasValue)
            {
                var set = await service.SetEnabledAsync(userId, id, req.Enabled.Value, ct);
                return set ? Results.Ok() : Results.NotFound();
            }
            return Results.BadRequest();
        });

        group.MapDelete("/{id}", async (string id, ClaimsPrincipal user, ApiKeyService service, CancellationToken ct) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var revoked = await service.RevokeAsync(userId, id, ct);
            return revoked ? Results.Ok() : Results.NotFound();
        });

        return endpoints;
    }

    public sealed record ApiKeyDto(string Id, string? Name, string KeyPrefix, bool IsEnabled, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt, DateTimeOffset? LastUsedAt);
    public sealed record CreateApiKeyRequest(string? Name, DateTimeOffset? ExpiresAt);
    public sealed record CreateApiKeyResponse(string Id, string? Name, string KeyPrefix, string RawKey, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);
    public sealed record UpdateApiKeyRequest(bool? Enabled, bool? Revoke);
}
