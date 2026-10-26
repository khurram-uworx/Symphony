using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Symphony.Data;

public static class IdentityRegistration
{
    public static IdentityBuilder AddSymphonyIdentity(this IServiceCollection services) =>
        AddSymphonyIdentity(services, configuration: null, environment: null);

    public static IdentityBuilder AddSymphonyIdentity(
        this IServiceCollection services,
        IWebHostEnvironment environment) =>
        AddSymphonyIdentity(services, configuration: null, environment: environment);

    public static IdentityBuilder AddSymphonyIdentity(
        this IServiceCollection services,
        IConfiguration configuration) =>
        AddSymphonyIdentity(services, configuration: configuration, environment: null);

    public static IdentityBuilder AddSymphonyIdentity(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment) =>
        AddSymphonyIdentityCore(services, configuration, environment);



    static IdentityBuilder AddSymphonyIdentityCore(
        IServiceCollection services,
        IConfiguration? configuration,
        IWebHostEnvironment? environment)
    {
        var builder = services
            .AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<SymphonyDbContext>()
            .AddDefaultTokenProviders();

        var isProduction = ResolveIsProduction(configuration, environment);

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";

            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = isProduction
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            options.Events.OnRedirectToLogin = context =>
            {
                if (IsApiOrAjax(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (IsApiOrAjax(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        static bool IsApiOrAjax(HttpRequest request) =>
            request.Path.Value?.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true ||
            string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.Zero;
        });

        return builder;
    }

    static bool ResolveIsProduction(IConfiguration? configuration, IWebHostEnvironment? environment)
    {
        if (environment is not null)
            return environment.IsProduction();

        var configured = configuration?["ASPNETCORE_ENVIRONMENT"]
            ?? configuration?["Environment"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.Equals(configured, "Production", StringComparison.OrdinalIgnoreCase);
    }
}