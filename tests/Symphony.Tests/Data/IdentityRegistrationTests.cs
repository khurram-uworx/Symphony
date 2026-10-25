using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Symphony.Data;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests;

[TestFixture, Category(TestCatory.Unit)]
class IdentityRegistrationTests
{
    [Test]
    public void AddSymphonyIdentity_Default_SetsAccountCookiePaths()
    {
        var services = new ServiceCollection();
        services.AddSymphonyIdentity();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.That(options.LoginPath.Value, Is.EqualTo("/Account/Login"));
        Assert.That(options.LogoutPath.Value, Is.EqualTo("/Account/Logout"));
        Assert.That(options.AccessDeniedPath.Value, Is.EqualTo("/Account/AccessDenied"));
    }

    [Test]
    public void AddSymphonyIdentity_Default_MakesCookieHttpOnlyAndLax()
    {
        var services = new ServiceCollection();
        services.AddSymphonyIdentity();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.That(options.Cookie.HttpOnly, Is.True);
        Assert.That(options.Cookie.SameSite, Is.EqualTo(SameSiteMode.Lax));
        Assert.That(options.SlidingExpiration, Is.True);
        Assert.That(options.ExpireTimeSpan, Is.EqualTo(TimeSpan.FromDays(14)));
    }

    [Test]
    public void AddSymphonyIdentity_Production_MakesCookieSecure()
    {
        var services = new ServiceCollection();
        services.AddSymphonyIdentity(new FakeWebHostEnvironment
        {
            EnvironmentName = Environments.Production
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.That(options.Cookie.SecurePolicy, Is.EqualTo(CookieSecurePolicy.Always));
    }

    [Test]
    public void AddSymphonyIdentity_Development_DoesNotForceSecure()
    {
        var services = new ServiceCollection();
        services.AddSymphonyIdentity(new FakeWebHostEnvironment
        {
            EnvironmentName = Environments.Development
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.That(options.Cookie.SecurePolicy, Is.EqualTo(CookieSecurePolicy.SameAsRequest));
    }

    sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Symphony";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}