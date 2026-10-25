using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symphony.Data;
using Symphony.Services;

namespace Symphony.Controllers;

[Authorize(Roles = AdminSeeder.AdminRole)]
public sealed class AdminController(
    UserManagementService users,
    JobService? jobService = null,
    UserManager<IdentityUser>? userManager = null,
    Microsoft.EntityFrameworkCore.IDbContextFactory<Symphony.Data.SymphonyDbContext>? dbContextFactory = null,
    Symphony.Services.SystemWorkspaceRootProvider? workspaceRootProvider = null) : Controller
{
    readonly UserManagementService users = users;
    readonly JobService? configuredJobService = jobService;
    readonly UserManager<IdentityUser>? configuredUserManager = userManager;
    readonly Microsoft.EntityFrameworkCore.IDbContextFactory<Symphony.Data.SymphonyDbContext>? configuredDbFactory = dbContextFactory;
    readonly Symphony.Services.SystemWorkspaceRootProvider? configuredWorkspaceRootProvider = workspaceRootProvider;

    JobService JobService =>
        configuredJobService
        ?? HttpContext?.RequestServices?.GetService<JobService>()
        ?? throw new InvalidOperationException("JobService is not available.");

    UserManager<IdentityUser> UserManager =>
        configuredUserManager
        ?? HttpContext?.RequestServices?.GetRequiredService<UserManager<IdentityUser>>();

    Microsoft.EntityFrameworkCore.IDbContextFactory<Symphony.Data.SymphonyDbContext> DbFactory =>
        configuredDbFactory
        ?? HttpContext?.RequestServices?.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Symphony.Data.SymphonyDbContext>>();

    Symphony.Services.SystemWorkspaceRootProvider WorkspaceRootProvider =>
        configuredWorkspaceRootProvider
        ?? HttpContext?.RequestServices?.GetRequiredService<Symphony.Services.SystemWorkspaceRootProvider>();

    public IActionResult Index() => View();

    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var usersList = await users.GetUsersAsync(cancellationToken);
        return View(usersList);
    }

    [HttpGet]
    public async Task<IActionResult> Jobs(
        string? state = null,
        string? status = null,
        string? owner = null,
        string? ownerId = null,
        string? ownerUserId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? startDate = null,
        DateTimeOffset? to = null,
        DateTimeOffset? endDate = null,
        int? page = null,
        int? pageNumber = null,
        int? pageSize = null,
        int? size = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedState = FirstNonEmpty(state, status);
        var resolvedOwner = FirstNonEmpty(owner, ownerId, ownerUserId);
        var resolvedFrom = from ?? startDate;
        var resolvedTo = to ?? endDate;
        var resolvedPage = page ?? pageNumber ?? 1;
        if (resolvedPage < 1)
            resolvedPage = 1;
        var resolvedPageSize = pageSize ?? size ?? 20;
        if (resolvedPageSize < 1)
            resolvedPageSize = 20;
        if (resolvedPageSize > 100)
            resolvedPageSize = 100;

        var jobs = await JobService.QueryJobsAsync(
            resolvedOwner,
            resolvedState,
            resolvedFrom,
            resolvedTo,
            resolvedPage,
            resolvedPageSize,
            cancellationToken);
        var total = await JobService.CountJobsAsync(
            resolvedOwner,
            resolvedState,
            resolvedFrom,
            resolvedTo,
            cancellationToken);

        var userList = await users.GetUsersAsync(cancellationToken);
        ViewBag.Users = userList;
        ViewBag.StateFilter = resolvedState;
        ViewBag.OwnerFilter = resolvedOwner;
        ViewBag.FromFilter = resolvedFrom;
        ViewBag.ToFilter = resolvedTo;
        ViewBag.Page = resolvedPage;
        ViewBag.PageSize = resolvedPageSize;
        ViewBag.Total = total;
        ViewBag.TotalPages = total == 0 ? 1 : (total + resolvedPageSize - 1) / resolvedPageSize;

        return View("Jobs", jobs);
    }

    [HttpGet]
    [Route("/admin/jobs/{id:guid}")]
    public async Task<IActionResult> JobDetail(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job = await JobService.GetJobAsync(id, cancellationToken);
        if (job is null)
            return NotFound();

        var issues = await JobService.GetIssuesAsync(id, cancellationToken);
        var events = await JobService.GetEventsAsync(id, 100, cancellationToken);

        ViewData["Title"] = $"Job {job.Id}";
        ViewBag.Issues = issues;
        ViewBag.Events = events;

        return View("JobDetail", job);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/admin/jobs/{id:guid}/cancel")]
    public async Task<IActionResult> CancelJob(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cancelled = await JobService.CancelAsync(id, cancellationToken);
        TempData[cancelled ? "Message" : "Error"] = cancelled ? "Job cancelled." : "Job could not be cancelled.";
        return RedirectToAction(nameof(JobDetail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string userName, string password, string role, CancellationToken cancellationToken)
    {
        try
        {
            var user = await users.CreateUserAsync(userName, password, role, cancellationToken);
            TempData["Message"] = $"Created user '{user.UserName}'.";
        }
        catch (Exception exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableUser(string userId, CancellationToken cancellationToken)
    {
        try
        {
            await users.DisableUserAsync(userId, cancellationToken);
            TempData["Message"] = "User disabled.";
        }
        catch (Exception exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableUser(string userId, CancellationToken cancellationToken)
    {
        try
        {
            await users.EnableUserAsync(userId, cancellationToken);
            TempData["Message"] = "User enabled.";
        }
        catch (Exception exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId, CancellationToken cancellationToken)
    {
        try
        {
            await users.DeleteUserAsync(userId, cancellationToken);
            TempData["Message"] = "User deleted.";
        }
        catch (Exception exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUserPassword(string userId, string password, CancellationToken cancellationToken)
    {
        try
        {
            await users.SetPasswordAsync(userId, password, cancellationToken);
            TempData["Message"] = "Password changed.";
        }
        catch (Exception exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var password = await users.ResetPasswordAsync(userId, cancellationToken);
            TempData["Message"] = $"Reset password: {password}";
        }
        catch (Exception exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string userId, string role, CancellationToken cancellationToken)
    {
        try
        {
            await users.SetRoleAsync(userId, role, cancellationToken);
            TempData["Message"] = "Role updated.";
        }
        catch (Exception exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpGet("/admin/system")]
    public async Task<IActionResult> System(CancellationToken cancellationToken = default)
    {
        var configuredValue = (await DbFactory.CreateDbContextAsync(cancellationToken)).AppSettings
            .AsNoTracking()
            .FirstOrDefault(setting => setting.Key == Services.SystemWorkspaceRootProvider.SettingKey)?.Value
            ?? Services.SystemWorkspaceRootProvider.DefaultTemplate;

        ViewBag.SystemWorkspaceRootConfigured = configuredValue;
        ViewBag.SystemWorkspaceRootResolved = await WorkspaceRootProvider.GetAsync(cancellationToken);
        return View();
    }

    [HttpPost("/admin/system")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSystemConfiguration(string workspaceRoot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            TempData["Error"] = "Workspace root is required.";
            return RedirectToAction(nameof(System));
        }

        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        var existing = db.AppSettings.FirstOrDefault(setting => setting.Key == Services.SystemWorkspaceRootProvider.SettingKey);
        if (existing is null)
        {
            db.AppSettings.Add(new Symphony.Data.AppSetting
            {
                Key = Services.SystemWorkspaceRootProvider.SettingKey,
                Value = workspaceRoot.Trim()
            });
        }
        else
        {
            existing.Value = workspaceRoot.Trim();
        }

        await db.SaveChangesAsync(cancellationToken);
        TempData["Message"] = "System configuration saved.";
        return RedirectToAction(nameof(System));
    }

    [HttpGet("/admin/settings")]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        var settings = db.AppSettings.ToList();
        ViewBag.Settings = settings.OrderBy(s => s.Key).ToList();
        return View();
    }

    [HttpPost("/admin/settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(IFormCollection form, CancellationToken cancellationToken)
    {
        await using var db = await DbFactory.CreateDbContextAsync(cancellationToken);
        var keys = form.Keys.Where(k => !string.IsNullOrWhiteSpace(k) && k.ToString() != "__RequestVerificationToken").ToList();
        foreach (var keyObj in keys)
        {
            var key = keyObj.ToString();
            var value = form[key].ToString();
            var existing = db.AppSettings.FirstOrDefault(s => s.Key == key);
            if (existing is null)
            {
                db.AppSettings.Add(new Symphony.Data.AppSetting { Key = key, Value = value ?? string.Empty });
            }
            else
            {
                existing.Value = value ?? string.Empty;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        TempData["Message"] = "Settings saved.";
        return RedirectToAction("Settings");
    }

    static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return null;
    }
}