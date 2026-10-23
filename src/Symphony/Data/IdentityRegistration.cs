using Microsoft.AspNetCore.Identity;

namespace Symphony.Data;

public static class IdentityRegistration
{
    public static IdentityBuilder AddSymphonyIdentity(this IServiceCollection services)
    {
        var builder = services
            .AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<SymphonyDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.Zero;
        });

        return builder;
    }
}
