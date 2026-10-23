using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Symphony.Controllers;

public sealed class AccountController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager) : Controller
{
    static bool isLoginPath(string returnUrl)
    {
        var path = returnUrl.Split('?', 2)[0];
        return path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase);
    }

    IActionResult redirectAfterLogin(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && !isLoginPath(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return redirectAfterLogin(returnUrl);

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginInput());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input, string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(input);

        var result = await signInManager.PasswordSignInAsync(
            input.UserName!,
            input.Password!,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.Succeeded)
            return redirectAfterLogin(returnUrl);

        ModelState.AddModelError(string.Empty, "Invalid username or password.");
        return View(input);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        var roles = user is null
            ? Array.Empty<string>()
            : await userManager.GetRolesAsync(user);

        ViewData["DisplayName"] = user?.UserName ?? User.Identity?.Name;
        ViewData["Roles"] = roles;
        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
EOF