using Microsoft.AspNetCore.Identity;
using Symphony.Services;

namespace Symphony.Middleware;

public sealed class ApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ApiKeyService apiKeyService, SignInManager<IdentityUser> signInManager)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            await next(context);
            return;
        }

        string? key = null;
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var value = authHeader.ToString();
            if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                key = value.Substring("Bearer ".Length).Trim();
        }
        if (string.IsNullOrEmpty(key) && context.Request.Headers.TryGetValue("X-API-Key", out var xkey))
            key = xkey.ToString().Trim();

        if (!string.IsNullOrEmpty(key))
        {
            var user = await apiKeyService.ValidateAsync(key);
            if (user is not null)
            {
                var claimsPrincipal = await signInManager.CreateUserPrincipalAsync(user);
                context.User = claimsPrincipal;
                await next(context);
                return;
            }
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }
}

public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}
