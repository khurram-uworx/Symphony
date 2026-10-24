using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Symphony.Data;
using Symphony.Services;

namespace Symphony.Tests;

public class ApiKeyServiceTests
{
    static IDbContextFactory<SymphonyDbContext> CreateFactory(string name)
    {
        var options = new DbContextOptionsBuilder<SymphonyDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new InMemoryFactory(options);
    }

    class InMemoryFactory(DbContextOptions<SymphonyDbContext> options) : IDbContextFactory<SymphonyDbContext>
    {
        public SymphonyDbContext CreateDbContext() => new SymphonyDbContext(options);
        public Task<SymphonyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    [Test]
    public async Task CreateAndValidate_Works()
    {
        var name = Guid.NewGuid().ToString();
        var factory = CreateFactory(name);
        var options = new DbContextOptionsBuilder<SymphonyDbContext>().UseInMemoryDatabase(name + "_store").Options;
        var context = new SymphonyDbContext(options);
        var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<IdentityUser>(context);
        var userManager = new UserManager<IdentityUser>(userStore, null, new PasswordHasher<IdentityUser>(), null, null, null, null, null, null);
        var user = new IdentityUser { UserName = "u1", Id = Guid.NewGuid().ToString() };
        await userManager.CreateAsync(user);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var service = new ApiKeyService(factory, userManager, config);
        var (raw, meta) = await service.CreateAsync(user.Id, "test");
        Assert.That(string.IsNullOrEmpty(raw), Is.False);
        Assert.That(meta.KeyPrefix.Length, Is.GreaterThan(0));
        var validated = await service.ValidateAsync(raw);
        Assert.That(validated, Is.Not.Null);
    }

    [Test]
    public async Task Rotate_InvalidatesOld()
    {
        var name = Guid.NewGuid().ToString();
        var factory = CreateFactory(name);
        var options = new DbContextOptionsBuilder<SymphonyDbContext>().UseInMemoryDatabase(name + "_store").Options;
        var context = new SymphonyDbContext(options);
        var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<IdentityUser>(context);
        var userManager = new UserManager<IdentityUser>(userStore, null, new PasswordHasher<IdentityUser>(), null, null, null, null, null, null);
        var user = new IdentityUser { UserName = "u2", Id = Guid.NewGuid().ToString() };
        await userManager.CreateAsync(user);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var service = new ApiKeyService(factory, userManager, config);
        var (raw1, meta1) = await service.CreateAsync(user.Id);
        var rotated = await service.RotateAsync(user.Id, meta1.Id);
        Assert.That(rotated, Is.Not.Null);
        var validatedOld = await service.ValidateAsync(raw1);
        Assert.That(validatedOld, Is.Null);
        var validatedNew = await service.ValidateAsync(rotated!.Value.RawKey);
        Assert.That(validatedNew, Is.Not.Null);
    }

    [Test]
    public async Task Disable_Invalidates()
    {
        var name = Guid.NewGuid().ToString();
        var factory = CreateFactory(name);
        var options = new DbContextOptionsBuilder<SymphonyDbContext>().UseInMemoryDatabase(name + "_store").Options;
        var context = new SymphonyDbContext(options);
        var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<IdentityUser>(context);
        var userManager = new UserManager<IdentityUser>(userStore, null, new PasswordHasher<IdentityUser>(), null, null, null, null, null, null);
        var user = new IdentityUser { UserName = "u3", Id = Guid.NewGuid().ToString() };
        await userManager.CreateAsync(user);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var service = new ApiKeyService(factory, userManager, config);
        var (raw, meta) = await service.CreateAsync(user.Id);
        await service.SetEnabledAsync(user.Id, meta.Id, false);
        var validated = await service.ValidateAsync(raw);
        Assert.That(validated, Is.Null);
    }
}
