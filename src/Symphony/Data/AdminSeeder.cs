using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Symphony.Services;

namespace Symphony.Data;

public static class AdminSeeder
{
    const string DuplicateUserName = "DuplicateUserName";
    const string DuplicateRoleName = "DuplicateRoleName";
    const string CredentialHeader = "Symphony admin credentials (shown once)";
    const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string Lower = "abcdefghijklmnopqrstuvwxyz";
    const string Digits = "0123456789";
    const string Symbols = "!@#$%*-_=+";

    public const string AdminRole = "Admin";
    public const string UserRole = "User";
    public const string DefaultUserName = "testadmin";

    static async Task ensureRole(RoleManager<IdentityRole> roles, string name)
    {
        if (await roles.RoleExistsAsync(name))
            return;

        var created = await roles.CreateAsync(new IdentityRole(name));
        if (!created.Succeeded && created.Errors.All(error => error.Code != DuplicateRoleName))
            throw identityFailure(created);
    }

    static async Task<bool> adminExists(UserManager<IdentityUser> users) =>
        (await users.GetUsersInRoleAsync(AdminRole)).Count > 0;

    static string generatePassword(PasswordOptions options)
    {
        var length = Math.Max(options.RequiredLength, 20);
        length = Math.Max(length, options.RequiredUniqueChars);
        var alphabet = Upper + Lower + Digits + Symbols;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var required = new List<char>();
            if (options.RequireUppercase)
                required.Add(pick(Upper));
            if (options.RequireLowercase)
                required.Add(pick(Lower));
            if (options.RequireDigit)
                required.Add(pick(Digits));
            if (options.RequireNonAlphanumeric)
                required.Add(pick(Symbols));

            if (required.Count > length)
                length = required.Count;

            var chars = new char[length];
            for (var i = 0; i < required.Count; i++)
                chars[i] = required[i];
            for (var i = required.Count; i < length; i++)
                chars[i] = pick(alphabet);

            shuffle(chars);
            var password = new string(chars);
            if (options.RequiredUniqueChars <= 1 || password.Distinct().Count() >= options.RequiredUniqueChars)
                return password;
        }

        throw new InvalidOperationException("Could not generate a password that satisfies Identity password options.");
    }

    static char pick(string alphabet) =>
        alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

    static void shuffle(Span<char> chars)
    {
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }

    static bool isDuplicateUserName(IdentityResult result) =>
        result.Errors.Any(error => error.Code == DuplicateUserName);

    static InvalidOperationException identityFailure(IdentityResult result) =>
        new(string.Join("; ", result.Errors.Select(error => error.Code)));

    static InvalidOperationException usernameTaken(string userName) =>
        new($"Cannot seed admin '{userName}' because that username already exists and has no Admin role.");

    public static async Task SeedAsync(
        SymphonyDbContext db,
        UserManager<IdentityUser> users,
        RoleManager<IdentityRole> roles,
        string? configuredUserName,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        var userName = string.IsNullOrWhiteSpace(configuredUserName)
            ? DefaultUserName
            : configuredUserName.Trim();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ensureRole(roles, AdminRole);
        await ensureRole(roles, UserRole);

        if (await adminExists(users))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (await users.FindByNameAsync(userName) is not null)
            throw usernameTaken(userName);

        var password = generatePassword(users.Options.Password);
        var user = new IdentityUser { UserName = userName };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            if (isDuplicateUserName(created))
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                if (await adminExists(users))
                    return;
            }

            throw identityFailure(created);
        }

        var added = await users.AddToRoleAsync(user, AdminRole);
        if (!added.Succeeded)
            throw identityFailure(added);

        await WorkflowService.EnsureDefaultWorkflowAsync(db, user.Id, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await output.WriteLineAsync(CredentialHeader);
        await output.WriteLineAsync($"Username: {userName}");
        await output.WriteLineAsync($"Password: {password}");
    }
}
