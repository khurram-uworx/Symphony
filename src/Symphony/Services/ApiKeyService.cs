using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Symphony.Data;

namespace Symphony.Services;

public sealed class ApiKeyService(
    IDbContextFactory<SymphonyDbContext> factory,
    UserManager<IdentityUser> userManager,
    IConfiguration configuration,
    IWebHostEnvironment? environment = null)
{
    public const string ServerKeyConfigName = "ApiKeys:ServerKey";

    const string DevelopmentServerKey = "symphony-api-key-server-secret-change-in-prod";
    const int KeyNameMaxLength = 256;

    readonly byte[] serverKey = ResolveServerKey(configuration, environment);

    static byte[] ResolveServerKey(IConfiguration configuration, IWebHostEnvironment? environment)
    {
        var configured = configuration[ServerKeyConfigName];
        if (!string.IsNullOrWhiteSpace(configured))
            return Encoding.UTF8.GetBytes(configured);

        if (environment is null || !environment.IsProduction())
            return Encoding.UTF8.GetBytes(DevelopmentServerKey);

        throw new InvalidOperationException(
            $"{ServerKeyConfigName} must be configured when running in Production so API key hashes can be verified.");
    }

    public async Task<(string RawKey, ApiKey Metadata)> CreateAsync(
        string ownerUserId,
        string? name = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        name = NormalizeName(name);
        if (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Expiration must be in the future.", nameof(expiresAt));

        var rawKey = GenerateKey();
        var prefix = rawKey[..8];

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            OwnerUserId = ownerUserId,
            Name = name,
            KeyPrefix = prefix,
            KeyHash = ComputeHash(rawKey),
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(cancellationToken);
        return (rawKey, apiKey);
    }

    public async Task<(string RawKey, ApiKey Metadata)?> RotateAsync(
        string ownerUserId,
        string keyId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.OwnerUserId == ownerUserId, cancellationToken);
        if (existing is null) return null;

        var rawKey = GenerateKey();
        var now = DateTimeOffset.UtcNow;

        var newKey = new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            OwnerUserId = ownerUserId,
            Name = existing.Name,
            KeyPrefix = rawKey[..8],
            KeyHash = ComputeHash(rawKey),
            IsEnabled = true,
            CreatedAt = now,
            ExpiresAt = existing.ExpiresAt,
            RotatedFromId = existing.Id
        };

        existing.IsEnabled = false;
        existing.RevokedAt ??= now;

        db.ApiKeys.Add(newKey);
        await db.SaveChangesAsync(cancellationToken);
        return (rawKey, newKey);
    }

    public async Task<bool> SetEnabledAsync(
        string ownerUserId,
        string keyId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.OwnerUserId == ownerUserId, cancellationToken);
        if (key is null) return false;

        if (enabled && key.RevokedAt is not null)
            return false;

        key.IsEnabled = enabled;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RevokeAsync(
        string ownerUserId,
        string keyId,
        CancellationToken cancellationToken = default)
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
        var rows = await db.ApiKeys.AsNoTracking().Where(k => k.OwnerUserId == ownerUserId).ToListAsync(cancellationToken);
        return rows.OrderByDescending(k => k.CreatedAt).ToList();
    }

    public async Task<IdentityUser?> ValidateAsync(string rawKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return null;

        var prefix = rawKey.Length >= 8 ? rawKey[..8] : rawKey;
        var hash = ComputeHash(rawKey);
        var now = DateTimeOffset.UtcNow;

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var candidates = await db.ApiKeys.Where(k => k.KeyPrefix == prefix && k.IsEnabled && k.RevokedAt == null).ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            if (candidate.ExpiresAt.HasValue && candidate.ExpiresAt.Value < now) continue;
            if (!FixedTimeEquals(candidate.KeyHash, hash)) continue;

            candidate.LastUsedAt = now;
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException) { /* stamping LastUsedAt is best-effort; never fail auth over it */ }

            var user = await userManager.FindByIdAsync(candidate.OwnerUserId);
            if (user is null) return null;

            var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
            if (lockoutEnd.HasValue && lockoutEnd.Value > now) return null;
            return user;
        }

        return null;
    }

    public async Task InvalidateAllForUserAsync(string ownerUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var keys = await db.ApiKeys.Where(k => k.OwnerUserId == ownerUserId && k.IsEnabled).ToListAsync(cancellationToken);
        if (keys.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var key in keys)
        {
            key.IsEnabled = false;
            key.RevokedAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var trimmed = name!.Trim();
        if (trimmed.Length > KeyNameMaxLength)
            throw new ArgumentException($"Key name must be at most {KeyNameMaxLength} characters.", nameof(name));
        return trimmed;
    }

    static string GenerateKey()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    string ComputeHash(string key)
    {
        using var hmac = new HMACSHA256(serverKey);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(key)));
    }

    static bool FixedTimeEquals(string a, string b)
    {
        try
        {
            var ba = Convert.FromBase64String(a);
            var bb = Convert.FromBase64String(b);
            if (ba.Length != bb.Length) return false;

            int result = 0;
            for (int i = 0; i < ba.Length; i++) result |= ba[i] ^ bb[i];
            return result == 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
