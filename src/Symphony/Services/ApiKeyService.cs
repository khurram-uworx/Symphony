using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Symphony.Data;

namespace Symphony.Services;

public sealed class ApiKeyService
{
    readonly IDbContextFactory<SymphonyDbContext> factory;
    readonly UserManager<IdentityUser> userManager;
    readonly byte[] serverKey;

    public ApiKeyService(IDbContextFactory<SymphonyDbContext> factory, UserManager<IdentityUser> userManager, IConfiguration configuration)
    {
        this.factory = factory;
        this.userManager = userManager;
        var key = configuration[\
ApiKeys:ServerKey\] ?? configuration[\DataProtection:ServerKey\] ?? \symphony-api-key-server-secret-change-in-prod\;
        serverKey = Encoding.UTF8.GetBytes(key);
    }

    public async Task<(string RawKey, ApiKey Metadata)> CreateAsync(string ownerUserId, string? name = null, DateTimeOffset? expiresAt = null, CancellationToken cancellationToken = default)
    {
        var rawKey = GenerateKey();
        var prefix = rawKey.Length >= 8 ? rawKey[..8] : rawKey;
        var hash = ComputeHash(rawKey);
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            OwnerUserId = ownerUserId,
            Name = name,
            KeyPrefix = prefix,
            KeyHash = hash,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(cancellationToken);
        return (rawKey, apiKey);
    }

    public async Task<(string RawKey, ApiKey Metadata)?> RotateAsync(string ownerUserId, string keyId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.OwnerUserId == ownerUserId, cancellationToken);
        if (existing is null) return null;
        var rawKey = GenerateKey();
        var prefix = rawKey.Length >= 8 ? rawKey[..8] : rawKey;
        var hash = ComputeHash(rawKey);
        var newKey = new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            OwnerUserId = ownerUserId,
            Name = existing.Name,
            KeyPrefix = prefix,
            KeyHash = hash,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = existing.ExpiresAt,
            RotatedFromId = existing.Id
        };
        existing.IsEnabled = false;
        existing.RevokedAt = DateTimeOffset.UtcNow;
        db.ApiKeys.Add(newKey);
        await db.SaveChangesAsync(cancellationToken);
        return (rawKey, newKey);
    }

    public async Task<bool> SetEnabledAsync(string ownerUserId, string keyId, bool enabled, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.OwnerUserId == ownerUserId, cancellationToken);
        if (key is null) return false;
        key.IsEnabled = enabled;
        if (!enabled) key.RevokedAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RevokeAsync(string ownerUserId, string keyId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.OwnerUserId == ownerUserId, cancellationToken);
        if (key is null) return false;
        key.IsEnabled = false;
        key.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<ApiKey>> ListAsync(string ownerUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.ApiKeys.Where(k => k.OwnerUserId == ownerUserId).OrderByDescending(k => k.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IdentityUser?> ValidateAsync(string rawKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return null;
        var prefix = rawKey.Length >= 8 ? rawKey[..8] : rawKey;
        var hash = ComputeHash(rawKey);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var candidates = await db.ApiKeys.Where(k => k.KeyPrefix == prefix && k.IsEnabled && k.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            if (candidate.ExpiresAt.HasValue && candidate.ExpiresAt.Value < DateTimeOffset.UtcNow) continue;
            if (FixedTimeEquals(candidate.KeyHash, hash))
            {
                candidate.LastUsedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                var user = await userManager.FindByIdAsync(candidate.OwnerUserId);
                if (user is null) return null;
                var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
                if (lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow) return null;
                return user;
            }
        }
        return null;
    }

    public async Task InvalidateAllForUserAsync(string ownerUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var keys = await db.ApiKeys.Where(k => k.OwnerUserId == ownerUserId && k.IsEnabled).ToListAsync(cancellationToken);
        foreach (var k in keys) { k.IsEnabled = false; k.RevokedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(cancellationToken);
    }

    static string GenerateKey() { var bytes = RandomNumberGenerator.GetBytes(32); return Convert.ToBase64String(bytes).Replace(\
+\,\-\).Replace(\/\,\_\).TrimEnd('='); }
    string ComputeHash(string key) { using var hmac = new HMACSHA256(serverKey); var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(key)); return Convert.ToBase64String(bytes); }
    static bool FixedTimeEquals(string a,string b){ try{ var ba=Convert.FromBase64String(a); var bb=Convert.FromBase64String(b); if(ba.Length!=bb.Length)return false; int r=0; for(int i=0;i<ba.Length;i++)r|=ba[i]^bb[i]; return r==0; }catch{return false;} }
}
