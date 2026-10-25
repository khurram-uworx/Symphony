using Microsoft.AspNetCore.Identity;

namespace Symphony.Data;

public sealed class RepositoryWorkspace
{
    public int Id { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public string Repository { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}