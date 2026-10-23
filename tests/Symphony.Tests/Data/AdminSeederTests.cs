using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Symphony.Data;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests;

[TestFixture, Category(TestCatory.Unit)]
class AdminSeederTests
{
    string directory = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "symphony-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    [Test]
    public async Task SeedAsync_NoAdmin_CreatesRolesAdminAndWritesCredentials()
    {
        await using var provider = await createInitializedProvider();
        var output = new StringWriter();

        await seed(provider, output, configuredUserName: null);

        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<SymphonyDbContext>();
        var admin = await users.FindByNameAsync(AdminSeeder.DefaultUserName);
        var password = passwordFrom(output.ToString());

        Assert.That(await roles.RoleExistsAsync(AdminSeeder.AdminRole), Is.True);
        Assert.That(await roles.RoleExistsAsync(AdminSeeder.UserRole), Is.True);
        Assert.That(await db.Users.CountAsync(), Is.EqualTo(1));
        Assert.That(admin, Is.Not.Null);
        Assert.That(await users.IsInRoleAsync(admin!, AdminSeeder.AdminRole), Is.True);
        Assert.That(await users.IsInRoleAsync(admin!, AdminSeeder.UserRole), Is.False);
        Assert.That(admin!.PasswordHash, Is.Not.EqualTo(password));
        Assert.That(await users.CheckPasswordAsync(admin, password), Is.True);
        Assert.That(output.ToString(), Does.Contain("Username: " + AdminSeeder.DefaultUserName));
        Assert.That(output.ToString(), Does.Contain("Password: " + password));
        Assert.That(output.ToString().Split("Password:").Length - 1, Is.EqualTo(1));
    }

    [Test]
    public async Task SeedAsync_SecondCall_PreservesAdminAndWritesNothing()
    {
        await using var provider = await createInitializedProvider();
        var first = new StringWriter();
        await seed(provider, first, configuredUserName: null);
        var password = passwordFrom(first.ToString());

        string? id;
        string? hash;
        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = await users.FindByNameAsync(AdminSeeder.DefaultUserName);
            id = admin!.Id;
            hash = admin.PasswordHash;
        }

        var second = new StringWriter();
        await seed(provider, second, configuredUserName: null);

        await using var check = provider.CreateAsyncScope();
        var saved = await check.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>()
            .FindByNameAsync(AdminSeeder.DefaultUserName);

        Assert.That(second.ToString(), Is.Empty);
        Assert.That(saved!.Id, Is.EqualTo(id));
        Assert.That(saved.PasswordHash, Is.EqualTo(hash));
        Assert.That(await check.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>()
            .CheckPasswordAsync(saved, password), Is.True);
        Assert.That(await check.ServiceProvider.GetRequiredService<SymphonyDbContext>().Users.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task SeedAsync_ExistingAdmin_DoesNotPrintOrOverwrite()
    {
        await using var provider = await createInitializedProvider();
        const string existingName = "existing-admin";
        const string existingPassword = "Existing1!password";
        string? hash;

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
            var users = services.GetRequiredService<UserManager<IdentityUser>>();
            Assert.That((await roles.CreateAsync(new IdentityRole(AdminSeeder.AdminRole))).Succeeded, Is.True);
            var existing = new IdentityUser { UserName = existingName };
            Assert.That((await users.CreateAsync(existing, existingPassword)).Succeeded, Is.True);
            Assert.That((await users.AddToRoleAsync(existing, AdminSeeder.AdminRole)).Succeeded, Is.True);
            hash = existing.PasswordHash;
        }

        var output = new StringWriter();
        await seed(provider, output, configuredUserName: null);

        await using var check = provider.CreateAsyncScope();
        var checkServices = check.ServiceProvider;
        var saved = await checkServices.GetRequiredService<UserManager<IdentityUser>>().FindByNameAsync(existingName);

        Assert.That(output.ToString(), Is.Empty);
        Assert.That(await checkServices.GetRequiredService<SymphonyDbContext>().Users.CountAsync(), Is.EqualTo(1));
        Assert.That(saved!.PasswordHash, Is.EqualTo(hash));
        Assert.That(await checkServices.GetRequiredService<RoleManager<IdentityRole>>()
            .RoleExistsAsync(AdminSeeder.UserRole), Is.True);
    }

    [Test]
    public async Task SeedAsync_ConfiguredUserName_CreatesThatUser()
    {
        await using var provider = await createInitializedProvider();
        var output = new StringWriter();

        await seed(provider, output, configuredUserName: "  custom-admin  ");

        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var admin = await users.FindByNameAsync("custom-admin");

        Assert.That(admin, Is.Not.Null);
        Assert.That(await users.IsInRoleAsync(admin!, AdminSeeder.AdminRole), Is.True);
        Assert.That(await users.FindByNameAsync(AdminSeeder.DefaultUserName), Is.Null);
        Assert.That(output.ToString(), Does.Contain("Username: custom-admin"));
        Assert.That(await users.CheckPasswordAsync(admin!, passwordFrom(output.ToString())), Is.True);
    }

    [Test]
    public async Task SeedAsync_BlankUserName_UsesTestAdmin()
    {
        await using var provider = await createInitializedProvider();
        var output = new StringWriter();

        await seed(provider, output, configuredUserName: "   ");

        await using var scope = provider.CreateAsyncScope();
        var admin = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>()
            .FindByNameAsync(AdminSeeder.DefaultUserName);

        Assert.That(admin, Is.Not.Null);
        Assert.That(output.ToString(), Does.Contain("Username: " + AdminSeeder.DefaultUserName));
    }

    [Test]
    public async Task SeedAsync_UserNameTakenByNonAdmin_ThrowsWithoutPrinting()
    {
        await using var provider = await createInitializedProvider();
        const string takenName = "taken";
        const string takenPassword = "Taken1!password";
        string? hash;

        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var taken = new IdentityUser { UserName = takenName };
            Assert.That((await users.CreateAsync(taken, takenPassword)).Succeeded, Is.True);
            hash = taken.PasswordHash;
        }

        var output = new StringWriter();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seed(provider, output, configuredUserName: takenName));

        await using var check = provider.CreateAsyncScope();
        var saved = await check.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>()
            .FindByNameAsync(takenName);

        var roles = check.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        Assert.That(exception!.Message, Does.Contain(takenName));
        Assert.That(exception.Message, Does.Not.Contain(takenPassword));
        Assert.That(output.ToString(), Is.Empty);
        Assert.That(saved!.PasswordHash, Is.EqualTo(hash));
        Assert.That(await check.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>()
            .IsInRoleAsync(saved, AdminSeeder.AdminRole), Is.False);
        Assert.That(await check.ServiceProvider.GetRequiredService<SymphonyDbContext>().Users.CountAsync(), Is.EqualTo(1));
        Assert.That(await roles.RoleExistsAsync(AdminSeeder.AdminRole), Is.False);
        Assert.That(await roles.RoleExistsAsync(AdminSeeder.UserRole), Is.False);
    }

    async Task<ServiceProvider> createInitializedProvider()
    {
        var database = Path.Combine(directory, "symphony.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = database }.ToString();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "sqlite",
                ["ConnectionStrings:Sqlite"] = connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSymphonyDatabase(configuration, directory);
        services.AddSymphonyIdentity();
        var provider = services.BuildServiceProvider();
        await DatabaseInitializer.InitializeAsync(
            provider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>());
        return provider;
    }

    static async Task seed(ServiceProvider provider, TextWriter output, string? configuredUserName)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await AdminSeeder.SeedAsync(
            services.GetRequiredService<SymphonyDbContext>(),
            services.GetRequiredService<UserManager<IdentityUser>>(),
            services.GetRequiredService<RoleManager<IdentityRole>>(),
            configuredUserName,
            output);
    }

    static string passwordFrom(string output)
    {
        const string marker = "Password: ";
        var start = output.IndexOf(marker, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        var value = output[(start + marker.Length)..];
        var end = value.IndexOfAny(['\r', '\n']);
        return end < 0 ? value : value[..end];
    }
}
