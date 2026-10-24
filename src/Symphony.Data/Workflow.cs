using Microsoft.AspNetCore.Identity;

namespace Symphony.Data;

public sealed class Workflow
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string OwnerUserId { get; set; } = string.Empty;

    public IdentityUser? Owner { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
