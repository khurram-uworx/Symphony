using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Symphony.Data;
using Symphony.Services;

namespace Symphony.Tests;

public class ApiKeyServiceTests
{
    SqliteConnection connection = null!;
    DbContextOptions<SymphonyDbContext> options;
    SymphonyDbContext storeContext = null!;
    IDbContextFactory<SymphonyDbContext> factory = null!;
    UserManager<IdentityUser> userManager = null!;
    ApiKeyService service = null!;
    IdentityUser user = null!;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<SymphonyDbContext>().UseSqlite(connection).Options;

        storeContext = new SymphonyDbContext(options);
        storeContext.Database.EnsureCreated();

        factory = new FakeDbContextFactory(options);
        var store = new UserStore<IdentityUser>(storeContext);
        userManager = new UserManager<IdentityUser>(store, null, new PasswordHasher<IdentityUser>(), null, null, new UpperInvariantLookupNormalizer(), null, null, null);
        user = new IdentityUser { UserName = "u1", Id = Guid.NewGuid().ToString() };
        await userManager.CreateAsync(user);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        service = new ApiKeyService(factory, userManager, configuration);
    }

    [TearDown]
    public void TearDown()
    {
        storeContext.Dispose();
        connection.Close();
        connection.Dispose();
    }

    sealed class FakeDbContextFactory(DbContextOptions<SymphonyDbContext> options) : IDbContextFactory<SymphonyDbContext>
    {
        public SymphonyDbContext CreateDbContext() => new(options);
        public Task<SymphonyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    [Test]
    public async Task CreateAsync_GeneratesRawKeyWithPrefix_AndValidateResolvesOwner()
    {
        var (rawKey, metadata) = await service.CreateAsync(user.Id, "test-key");
        Assert.That(rawKey, Has.Length.GreaterThanOrEqualTo(20));
        Assert.That(metadata.KeyPrefix, Is.EqualTo(rawKey[..8]));
        Assert.That(metadata.KeyHash, Does.Not.Contain(rawKey));

        var validated = await service.ValidateAsync(rawKey);
        Assert.That(validated, Is.Not.Null);
        Assert.That(validated!.Id, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task ValidateAsync_UnknownKey_ReturnsNull()
    {
        var validated = await service.ValidateAsync("c29tZS13aGl0LXJhbmRvbS1rZXktdmFsdWU");
        Assert.That(validated, Is.Null);
    }

    [Test]
    public async Task RotateAsync_DisablesOldKey_AndIssuesWorkingNewKey()
    {
        var (oldRaw, meta) = await service.CreateAsync(user.Id);
        var rotated = await service.RotateAsync(user.Id, meta.Id);
        Assert.That(rotated, Is.Not.Null);
        Assert.That(rotated!.Value.Metadata.RotatedFromId, Is.EqualTo(meta.Id));

        Assert.That(await service.ValidateAsync(oldRaw), Is.Null);
        Assert.That((await service.ValidateAsync(rotated.Value.RawKey))!.Id, Is.EqualTo(user.Id));

        await using var db = new SymphonyDbContext(options);
        var old = await db.ApiKeys.FindAsync(meta.Id);
        Assert.That(old, Is.Not.Null);
        Assert.That(old!.IsEnabled, Is.False);
        Assert.That(old.RevokedAt, Is.Not.Null);
    }

    [Test]
    public async Task RotateAsync_KeyOfOtherOwner_ReturnsNull()
    {
        var (_, meta) = await service.CreateAsync(user.Id);
        Assert.That(await service.RotateAsync("other-user", meta.Id), Is.Null);
    }

    [Test]
    public async Task SetEnabledAsync_False_InvalidatesKey_TrueRestores()
    {
        var (rawKey, meta) = await service.CreateAsync(user.Id);
        await service.SetEnabledAsync(user.Id, meta.Id, false);
        Assert.That(await service.ValidateAsync(rawKey), Is.Null);

        await service.SetEnabledAsync(user.Id, meta.Id, true);
        Assert.That(await service.ValidateAsync(rawKey), Is.Not.Null);
    }

    [Test]
    public async Task SetEnabledAsync_RejectsInvalidInput_AndRefusesEnablingRevokedKey()
    {
        Assert.ThrowsAsync<ArgumentException>(async () => await service.CreateAsync(user.Id, new string('x', 257)));
        Assert.ThrowsAsync<ArgumentException>(async () => await service.CreateAsync(user.Id, "past", DateTimeOffset.UtcNow.AddMinutes(-1)));

        var (rawKey, meta) = await service.CreateAsync(user.Id, "future", DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.That(await service.ValidateAsync(rawKey), Is.Not.Null);

        await service.RevokeAsync(user.Id, meta.Id);
        Assert.That(await service.SetEnabledAsync(user.Id, meta.Id, true), Is.False);
        Assert.That(await service.ValidateAsync(rawKey), Is.Null);
    }

    [Test]
    public async Task RevokeAsync_PermanentlyInvalidatesKey()
    {
        var (rawKey, meta) = await service.CreateAsync(user.Id);
        await service.RevokeAsync(user.Id, meta.Id);
        Assert.That(await service.ValidateAsync(rawKey), Is.Null);

        await service.SetEnabledAsync(user.Id, meta.Id, true);
        Assert.That(await service.ValidateAsync(rawKey), Is.Null);
    }

    [Test]
    public async Task ListAsync_ReturnsOnlyOwnerKeys()
    {
        var secondUser = new IdentityUser { UserName = "u2", Id = Guid.NewGuid().ToString() };
        await userManager.CreateAsync(secondUser);

        await service.CreateAsync(user.Id, "mine");
        await service.CreateAsync(secondUser.Id, "theirs");

        var keys = await service.ListAsync(user.Id);
        Assert.That(keys, Has.Count.EqualTo(1));
        Assert.That(keys[0].OwnerUserId, Is.EqualTo(user.Id));
    }
}
