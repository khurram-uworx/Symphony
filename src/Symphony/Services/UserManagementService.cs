using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Symphony.Data;

namespace Symphony.Services;

public sealed class UserManagementService(
    UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles,
    SignInManager<IdentityUser>? signInManager = null,
    IDbContextFactory<SymphonyDbContext>? dbContextFactory = null,
    WorkflowService? workflowService = null,
    ApiKeyService? apiKeyService = null)
{
    const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string Lower = "abcdefghijklmnopqrstuvwxyz";
    const string Digits = "0123456789";
    const string Symbols = "!@#$%*-_=+";

    readonly UserManager<IdentityUser> users = users;
    readonly RoleManager<IdentityRole> roles = roles;
    readonly SignInManager<IdentityUser>? signInManager = signInManager;
    readonly IDbContextFactory<SymphonyDbContext>? dbContextFactory = dbContextFactory;
    readonly WorkflowService? workflowService = workflowService;
    readonly ApiKeyService? apiKeyService = apiKeyService;

    public Task<IReadOnlyList<IdentityUser>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        GetAllUsersAsync(users, cancellationToken);

    public Task<IReadOnlyList<IdentityUser>> GetAllUsersAsync(CancellationToken cancellationToken = default) =>
        GetAllUsersAsync(users, cancellationToken);

    public Task<IReadOnlyList<IdentityUser>> ListUsersAsync(CancellationToken cancellationToken = default) =>
        GetAllUsersAsync(users, cancellationToken);

    public static async Task<IReadOnlyList<IdentityUser>> GetAllUsersAsync(
        UserManager<IdentityUser> users,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await users.Users.OrderBy(user => user.UserName).ToListAsync<IdentityUser>(cancellationToken);
    }

    public Task<IdentityUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default) =>
        FindUserAsync(users, userId, cancellationToken);

    public Task<IdentityUser?> FindUserAsync(string userId, CancellationToken cancellationToken = default) =>
        FindUserAsync(users, userId, cancellationToken);

    public Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        FindUserAsync(users, userId, cancellationToken);

    public Task<IdentityUser?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        FindUserAsync(users, userId, cancellationToken);

    public Task<IdentityUser?> GetUserByNameAsync(string userName, CancellationToken cancellationToken = default) =>
        FindByNameAsync(users, userName, cancellationToken);

    public static async Task<IdentityUser?> FindUserAsync(
        UserManager<IdentityUser> users,
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await users.FindByIdAsync(userId);
    }

    public static async Task<IdentityUser?> FindByNameAsync(
        UserManager<IdentityUser> users,
        string userName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await users.FindByNameAsync(userName);
    }

    public async Task<IdentityUser> CreateUserAsync(
        string userName,
        string password,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        var user = await CreateUserAsync(
            users,
            roles,
            userName,
            password,
            role,
            cancellationToken,
            dbContextFactory);
        if (workflowService is not null)
            await workflowService.EnsureDefaultWorkflowAsync(user.Id, cancellationToken);
        else if (dbContextFactory is not null)
            await WorkflowService.EnsureDefaultWorkflowAsync(dbContextFactory, user.Id, cancellationToken);
        return user;
    }

    public static async Task<IdentityUser> CreateUserAsync(
        UserManager<IdentityUser> users,
        RoleManager<IdentityRole> roles,
        string userName,
        string password,
        string? role = null,
        CancellationToken cancellationToken = default,
        IDbContextFactory<SymphonyDbContext>? dbContextFactory = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("User name is required.", nameof(userName));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password is required.", nameof(password));

        role ??= AdminSeeder.UserRole;
        await EnsureRoleAsync(roles, role, cancellationToken);

        var user = new IdentityUser { UserName = userName.Trim() };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            throw IdentityFailure(created);

        var added = await users.AddToRoleAsync(user, role);
        if (!added.Succeeded)
        {
            await users.DeleteAsync(user);
            throw IdentityFailure(added);
        }

        if (dbContextFactory is not null)
            await WorkflowService.EnsureDefaultWorkflowAsync(dbContextFactory, user.Id, cancellationToken);

        return user;
    }

    public static class AdminSeeder
    {
        public const string AdminRole = "Admin";
        public const string UserRole = "User";
    }

    public Task<(IdentityUser User, string Password)> CreateMemberAsync(
        string userName,
        CancellationToken cancellationToken = default) =>
        CreateMemberAsync(users, roles, userName, cancellationToken, dbContextFactory);

    public static async Task<(IdentityUser User, string Password)> CreateMemberAsync(
        UserManager<IdentityUser> users,
        RoleManager<IdentityRole> roles,
        string userName,
        CancellationToken cancellationToken = default,
        IDbContextFactory<SymphonyDbContext>? dbContextFactory = null)
    {
        var password = GeneratePassword(users.Options.Password);
        var user = await CreateUserAsync(users, roles, userName, password, AdminSeeder.UserRole, cancellationToken, dbContextFactory);
        return (user, password);
    }

    public Task<(IdentityUser User, string Password)> CreateAdminAsync(
        string userName,
        CancellationToken cancellationToken = default) =>
        CreateAdminAsync(users, roles, userName, cancellationToken, dbContextFactory);

    public static async Task<(IdentityUser User, string Password)> CreateAdminAsync(
        UserManager<IdentityUser> users,
        RoleManager<IdentityRole> roles,
        string userName,
        CancellationToken cancellationToken = default,
        IDbContextFactory<SymphonyDbContext>? dbContextFactory = null)
    {
        var password = GeneratePassword(users.Options.Password);
        var user = await CreateUserAsync(users, roles, userName, password, AdminSeeder.AdminRole, cancellationToken, dbContextFactory);
        return (user, password);
    }

    public async Task DisableUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        await DisableUserAsync(users, userId, cancellationToken);
        if (apiKeyService is not null) await apiKeyService.InvalidateAllForUserAsync(userId, cancellationToken);
    }

    public async Task DisableUserAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        await DisableUserAsync(users, user, cancellationToken);
        if (apiKeyService is not null) await apiKeyService.InvalidateAllForUserAsync(user.Id, cancellationToken);
    }

    public static async Task DisableUserAsync(
        UserManager<IdentityUser> users,
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");
        await DisableUserAsync(users, user, cancellationToken);
    }

    public static async Task DisableUserAsync(
        UserManager<IdentityUser> users,
        IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await users.UpdateSecurityStampAsync(user);
    }

    public Task EnableUserAsync(string userId, CancellationToken cancellationToken = default) =>
        EnableUserAsync(users, userId, cancellationToken);

    public Task EnableUserAsync(IdentityUser user, CancellationToken cancellationToken = default) =>
        EnableUserAsync(users, user, cancellationToken);

    public static async Task EnableUserAsync(
        UserManager<IdentityUser> users,
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");
        await EnableUserAsync(users, user, cancellationToken);
    }

    public static async Task EnableUserAsync(
        UserManager<IdentityUser> users,
        IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await users.SetLockoutEndDateAsync(user, null);
    }

    public Task SetUserEnabledAsync(string userId, bool enabled, CancellationToken cancellationToken = default) =>
        SetUserEnabledAsync(users, userId, enabled, cancellationToken);

    public Task SetUserEnabledAsync(IdentityUser user, bool enabled, CancellationToken cancellationToken = default) =>
        SetUserEnabledAsync(users, user, enabled, cancellationToken);

    public static async Task SetUserEnabledAsync(
        UserManager<IdentityUser> users,
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");
        await SetUserEnabledAsync(users, user, enabled, cancellationToken);
    }

    public static Task SetUserEnabledAsync(
        UserManager<IdentityUser> users,
        IdentityUser user,
        bool enabled,
        CancellationToken cancellationToken = default) =>
        enabled ? EnableUserAsync(users, user, cancellationToken) : DisableUserAsync(users, user, cancellationToken);

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (apiKeyService is not null) await apiKeyService.InvalidateAllForUserAsync(userId, cancellationToken);
        await DeleteUserAsync(users, userId, cancellationToken);
    }

    public async Task DeleteUserAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        if (apiKeyService is not null) await apiKeyService.InvalidateAllForUserAsync(user.Id, cancellationToken);
        await DeleteUserAsync(users, user, cancellationToken);
    }

    public Task RemoveUserAsync(string userId, CancellationToken cancellationToken = default) =>
        DeleteUserAsync(userId, cancellationToken);

    public Task RemoveUserAsync(IdentityUser user, CancellationToken cancellationToken = default) =>
        DeleteUserAsync(user, cancellationToken);

    public static async Task DeleteUserAsync(
        UserManager<IdentityUser> users,
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");
        await DeleteUserAsync(users, user, cancellationToken);
    }

    public static async Task DeleteUserAsync(
        UserManager<IdentityUser> users,
        IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded)
            throw IdentityFailure(deleted);
    }

    public Task SetRoleAsync(string userId, string role, CancellationToken cancellationToken = default) =>
        SetRoleAsync(users, roles, userId, role, cancellationToken);

    public Task SetRoleAsync(IdentityUser user, string role, CancellationToken cancellationToken = default) =>
        SetRoleAsync(users, roles, user, role, cancellationToken);

    public Task AssignRoleAsync(string userId, string role, CancellationToken cancellationToken = default) =>
        SetRoleAsync(users, roles, userId, role, cancellationToken);

    public Task AssignRoleAsync(IdentityUser user, string role, CancellationToken cancellationToken = default) =>
        SetRoleAsync(users, roles, user, role, cancellationToken);

    public Task ChangeRoleAsync(string userId, string role, CancellationToken cancellationToken = default) =>
        SetRoleAsync(users, roles, userId, role, cancellationToken);

    public Task ChangeRoleAsync(IdentityUser user, string role, CancellationToken cancellationToken = default) =>
        SetRoleAsync(users, roles, user, role, cancellationToken);

    public static async Task SetRoleAsync(
        UserManager<IdentityUser> users,
        RoleManager<IdentityRole> roles,
        string userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");
        await SetRoleAsync(users, roles, user, role, cancellationToken);
    }

    public static async Task SetRoleAsync(
        UserManager<IdentityUser> users,
        RoleManager<IdentityRole> roles,
        IdentityUser user,
        string role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureRoleAsync(roles, role, cancellationToken);

        var currentRoles = await users.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removed = await users.RemoveFromRolesAsync(user, currentRoles);
            if (!removed.Succeeded)
                throw IdentityFailure(removed);
        }

        var added = await users.AddToRoleAsync(user, role);
        if (!added.Succeeded)
            throw IdentityFailure(added);

        await users.UpdateSecurityStampAsync(user);
    }

    public Task<string> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default) =>
        ResetPasswordAsync(users, userId, cancellationToken);

    public Task<string> ResetPasswordAsync(IdentityUser user, CancellationToken cancellationToken = default) =>
        ResetPasswordAsync(users, user, cancellationToken);

    public static async Task<string> ResetPasswordAsync(
        UserManager<IdentityUser> users,
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");
        return await ResetPasswordAsync(users, user, cancellationToken);
    }

    public static async Task<string> ResetPasswordAsync(
        UserManager<IdentityUser> users,
        IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var password = GeneratePassword(users.Options.Password);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var reset = await users.ResetPasswordAsync(user, token, password);
        if (!reset.Succeeded)
            throw IdentityFailure(reset);

        await users.UpdateSecurityStampAsync(user);
        return password;
    }

    public Task SetPasswordAsync(string userId, string password, CancellationToken cancellationToken = default) =>
        SetPasswordAsync(users, userId, password, cancellationToken);

    public Task SetPasswordAsync(IdentityUser user, string password, CancellationToken cancellationToken = default) =>
        SetPasswordAsync(users, user, password, cancellationToken);

    public static async Task SetPasswordAsync(
        UserManager<IdentityUser> users,
        string userId,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");
        await SetPasswordAsync(users, user, password, cancellationToken);
    }

    public static async Task SetPasswordAsync(
        UserManager<IdentityUser> users,
        IdentityUser user,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await users.HasPasswordAsync(user))
        {
            var removed = await users.RemovePasswordAsync(user);
            if (!removed.Succeeded)
                throw IdentityFailure(removed);
        }

        var added = await users.AddPasswordAsync(user, password);
        if (!added.Succeeded)
            throw IdentityFailure(added);

        await users.UpdateSecurityStampAsync(user);
    }

    public Task InvalidateSessionsAsync(string userId, CancellationToken cancellationToken = default) =>
        InvalidateSessionsAsync(users, userId, cancellationToken);

    public Task InvalidateSessionsAsync(IdentityUser user, CancellationToken cancellationToken = default) =>
        InvalidateSessionsAsync(users, user, cancellationToken);

    public Task InvalidateUserSessionsAsync(string userId, CancellationToken cancellationToken = default) =>
        InvalidateSessionsAsync(users, userId, cancellationToken);

    public Task InvalidateUserSessionsAsync(IdentityUser user, CancellationToken cancellationToken = default) =>
        InvalidateSessionsAsync(users, user, cancellationToken);

    public static async Task InvalidateSessionsAsync(
        UserManager<IdentityUser> users,
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");
        await InvalidateSessionsAsync(users, user, cancellationToken);
    }

    public static async Task InvalidateSessionsAsync(
        UserManager<IdentityUser> users,
        IdentityUser user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await users.UpdateSecurityStampAsync(user);
    }

    static async Task EnsureRoleAsync(RoleManager<IdentityRole> roles, string role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await roles.RoleExistsAsync(role))
            return;

        var created = await roles.CreateAsync(new IdentityRole(role));
        if (!created.Succeeded)
            throw IdentityFailure(created);
    }

    static string GeneratePassword(PasswordOptions options)
    {
        var length = Math.Max(options.RequiredLength, 20);
        length = Math.Max(length, options.RequiredUniqueChars);
        var alphabet = Upper + Lower + Digits + Symbols;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var required = new List<char>();
            if (options.RequireUppercase)
                required.Add(Pick(Upper));
            if (options.RequireLowercase)
                required.Add(Pick(Lower));
            if (options.RequireDigit)
                required.Add(Pick(Digits));
            if (options.RequireNonAlphanumeric)
                required.Add(Pick(Symbols));

            if (required.Count > length)
                length = required.Count;

            var chars = new char[length];
            for (var i = 0; i < required.Count; i++)
                chars[i] = required[i];
            for (var i = required.Count; i < length; i++)
                chars[i] = Pick(alphabet);

            Shuffle(chars);
            var password = new string(chars);
            if (options.RequiredUniqueChars <= 1 || password.Distinct().Count() >= options.RequiredUniqueChars)
                return password;
        }

        throw new InvalidOperationException("Could not generate a password that satisfies Identity password options.");
    }

    static char Pick(string alphabet) =>
        alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

    static void Shuffle(Span<char> chars)
    {
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }

    static InvalidOperationException IdentityFailure(IdentityResult result) =>
        new(string.Join("; ", result.Errors.Select(error => error.Code)));


}
