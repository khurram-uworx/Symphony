using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Symphony.Data;
using Symphony.Services;

namespace Symphony.Controllers;

[Authorize(Roles = AdminSeeder.AdminRole)]
public sealed class AdminController(UserManagementService users) : Controller
{
    public IActionResult Index() => View();

    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var usersList = await users.GetUsersAsync(cancellationToken);
        return View(usersList);
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
}
