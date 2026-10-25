using System;
using Microsoft.AspNetCore.Identity;

namespace Symphony.Data;

public sealed class ApiKey
{
    public string Id { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public IdentityUser? Owner { get; set; }

    public string? Name { get; set; }

    public string KeyPrefix { get; set; } = string.Empty;

    public string KeyHash { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public string? RotatedFromId { get; set; }
}
