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
        });

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