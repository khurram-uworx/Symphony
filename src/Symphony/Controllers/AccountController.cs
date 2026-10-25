using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Symphony.Data;
using Symphony.Dto;
using Symphony.Security;
using Symphony.Services;
using System.Security.Claims;

namespace Symphony.Controllers;

public sealed class AccountController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    ConfigurationService? configurationService = null,
    IWebHostEnvironment? environment = null) : Controller
{
    readonly SignInManager<IdentityUser> signInManager = signInManager;
    readonly UserManager<IdentityUser> userManager = userManager;
    readonly ConfigurationService? configuredConfigurationService = configurationService;
    readonly IWebHostEnvironment? configuredEnvironment = environment;

    ConfigurationService ConfigurationService =>
        configuredConfigurationService
        ?? HttpContext?.RequestServices?.GetService<ConfigurationService>()
        ?? throw new InvalidOperationException("ConfigurationService is not available.");

    IWebHostEnvironment? WebHostEnvironment =>
        configuredEnvironment
        ?? HttpContext?.RequestServices?.GetService<IWebHostEnvironment>();

    static bool isLoginPath(string returnUrl)
    {
        var path = returnUrl.Split('?', 2)[0];
        return path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase);
    }

    IActionResult redirectAfterLogin(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && !isLoginPath(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index), "Account");
    }

    async Task<string> GetUserIdAsync()
    {
        var user = await userManager.GetUserAsync(User);
        return user?.Id
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.Identity?.Name
            ?? string.Empty;
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
    [Authorize]
    public IActionResult Manage()
    {
        ViewData["DisplayName"] = User.Identity?.Name;
        return View(new ChangePasswordInput());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Manage(ChangePasswordInput input) => ChangePassword(input);

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
    {
        if (!ModelState.IsValid)
            return View("Manage", input);

        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var result = await userManager.ChangePasswordAsync(user, input.CurrentPassword!, input.NewPassword!);
        if (result.Succeeded)
        {
            await signInManager.RefreshSignInAsync(user);
            TempData["Message"] = "Password changed.";
            return RedirectToAction(nameof(Manage));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return View("Manage", input);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Configuration(CancellationToken cancellationToken = default)
    {
        var userId = await GetUserIdAsync();
        var configuration = await ConfigurationService.GetAsync(userId, cancellationToken)
            ?? new UserConfiguration { UserId = userId };
        return View("Configuration", ConfigurationDto.From(configuration));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configuration(ConfigurationDto? model, CancellationToken cancellationToken = default)
    {
        model ??= new ConfigurationDto();

        var userId = await GetUserIdAsync();

        string? validationError = null;
        if (!ModelState.IsValid || !model.IsValid(out validationError))
        {
            if (!string.IsNullOrWhiteSpace(validationError))
                ModelState.AddModelError(string.Empty, validationError);
            return View("Configuration", model);
        }

        var existing = await ConfigurationService.GetAsync(userId, cancellationToken);

        if (string.IsNullOrWhiteSpace(model.TrackerApiKey) ||
            string.Equals(model.TrackerApiKey, Redaction.Redacted, StringComparison.Ordinal))
        {
            model.TrackerApiKey = existing?.TrackerApiKey ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(model.CopilotApiKey) ||
            string.Equals(model.CopilotApiKey, Redaction.Redacted, StringComparison.Ordinal))
        {
            model.CopilotApiKey = existing?.CopilotApiKey;
        }

        model.UserId = userId;
        var configuration = model.ToUserConfiguration();
        configuration.UserId = userId;

        await ConfigurationService.SaveAsync(userId, configuration, cancellationToken);

        TempData["Message"] = "Configuration saved.";
        return RedirectToAction(nameof(Configuration));
    }

    [HttpGet]
    [Authorize]
    public Task<IActionResult> Workflows(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var template = string.Empty;
        var environment = WebHostEnvironment;
        if (environment is not null)
        {
            var path = Path.Combine(environment.ContentRootPath, "WORKFLOW.md");
            if (System.IO.File.Exists(path))
                template = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
        }

        ViewData["Title"] = "Workflows";
        ViewData["WorkflowTemplate"] = template;
        return Task.FromResult<IActionResult>(View("Workflows"));
    }

    [HttpGet]
    [Route("Account/Workflows/Edit")]
    [Authorize]
    public Task<IActionResult> WorkflowEdit(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var template = string.Empty;
        var environment = WebHostEnvironment;
        if (environment is not null)
        {
            var path = Path.Combine(environment.ContentRootPath, "WORKFLOW.md");
            if (System.IO.File.Exists(path))
                template = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
        }

        ViewData["Title"] = "Workflow editor";
        ViewData["WorkflowTemplate"] = template;
        return Task.FromResult<IActionResult>(View("WorkflowEdit"));
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Run(CancellationToken cancellationToken = default)
    {
        var userId = await GetUserIdAsync();
        var configuration = await ConfigurationService.GetAsync(userId, cancellationToken)
            ?? new UserConfiguration { UserId = userId };

        ViewData["Title"] = "Run launcher";
        ViewData["DefaultWorkflowName"] = configuration.DefaultWorkflowName ?? string.Empty;
        return View("Run");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ApiKeys([FromServices] ApiKeyService service)
    {
        var userId = await GetUserIdAsync();
        ViewData["Title"] = "API keys";
        return View(await service.ListAsync(userId));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateApiKey(string? name, [FromServices] ApiKeyService service)
    {
        var userId = await GetUserIdAsync();
        var trimmedName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var (rawKey, metadata) = await service.CreateAsync(userId, trimmedName);
        TempData["NewApiKey"] = rawKey;
        TempData["NewApiKeyName"] = trimmedName;
        return RedirectToAction(nameof(ApiKeys));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateApiKey(string id, [FromServices] ApiKeyService service)
    {
        var userId = await GetUserIdAsync();
        var result = await service.RotateAsync(userId, id);
        if (result is not null)
        {
            var (rawKey, metadata) = result.Value;
            TempData["NewApiKey"] = rawKey;
            TempData["NewApiKeyName"] = string.IsNullOrEmpty(metadata.Name) ? "Rotated API key" : metadata.Name;
        }
        return RedirectToAction(nameof(ApiKeys));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetApiKeyEnabled(string id, bool enabled, [FromServices] ApiKeyService service)
    {
        var userId = await GetUserIdAsync();
        await service.SetEnabledAsync(userId, id, enabled);
        return RedirectToAction(nameof(ApiKeys));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeApiKey(string id, [FromServices] ApiKeyService service)
    {
        var userId = await GetUserIdAsync();
        await service.RevokeAsync(userId, id);
        return RedirectToAction(nameof(ApiKeys));
    }
}
