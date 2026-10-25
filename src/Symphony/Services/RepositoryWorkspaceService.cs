using Microsoft.EntityFrameworkCore;
using Symphony.Data;

namespace Symphony.Services;

public sealed class RepositoryWorkspaceService(
    IDbContextFactory<SymphonyDbContext> factory)
{
    readonly IDbContextFactory<SymphonyDbContext> factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public async Task<int> GetOrCreateAsync(string ownerUserId, string repository, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("Owner user ID is required.", nameof(ownerUserId));

        var normalizedRepository = (repository ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedRepository))
            throw new ArgumentException("Repository is required.", nameof(repository));

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var existing = await db.RepositoryWorkspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.OwnerUserId == ownerUserId && item.Repository == normalizedRepository, cancellationToken);

        if (existing is not null)
            return existing.Id;

        var created = new RepositoryWorkspace
        {
            OwnerUserId = ownerUserId,
            Repository = normalizedRepository,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.RepositoryWorkspaces.Add(created);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created.Id;
        }
        catch (DbUpdateException)
        {
            var retry = await db.RepositoryWorkspaces
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.OwnerUserId == ownerUserId && item.Repository == normalizedRepository, cancellationToken);
            if (retry is not null)
                return retry.Id;

            throw;
        }
    }
}