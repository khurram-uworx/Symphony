using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
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
}
