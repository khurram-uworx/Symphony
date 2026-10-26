using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Symphony.Api;
using Symphony.Controllers;
using Symphony.Processing;
using Symphony.Security;
using Symphony.Services;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Api;

[TestFixture, Category(TestCatory.Unit)]
class ApiCookieRedirectTests
{
    [Test]
    public void MapApiEndpoints_AllApiRoutes_CarryDisableCookieRedirectMetadata()
    {
        using var app = createApp();
        app.MapApiKeyEndpoints();
        app.MapConfigurationEndpoints();
        app.MapJobEndpoints();
        app.MapWorkEndpoints();
        app.MapWorkflowEndpoints();

        var apiRoutes = routeEndpoints(app)
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api", StringComparison.Ordinal) == true)
            .ToList();

        Assert.That(apiRoutes, Is.Not.Empty);
        Assert.That(
            apiRoutes.Where(endpoint => endpoint.Metadata.GetMetadata<IDisableCookieRedirectMetadata>() is null),
            Is.Empty,
            "Every /api endpoint must answer unauthenticated calls with 401/403 instead of a login redirect.");
    }

    [Test]
    public void MapControllerRoute_ControllerEndpoints_DoNotCarryDisableCookieRedirectMetadata()
    {
        using var app = createApp(services => services
            .AddControllersWithViews()
            .AddApplicationPart(typeof(HomeController).Assembly));
        app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

        var controllerRoutes = routeEndpoints(app)
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .ToList();

        Assert.That(controllerRoutes, Is.Not.Empty);
        Assert.That(
            controllerRoutes.Where(endpoint => endpoint.Metadata.GetMetadata<IDisableCookieRedirectMetadata>() is not null),
            Is.Empty,
            "MVC screens must keep redirecting unauthenticated browsers to /Account/Login.");
    }

    static WebApplication createApp(Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<ApiKeyService>();
        builder.Services.AddScoped<ConfigurationService>();
        builder.Services.AddScoped<JobService>();
        builder.Services.AddScoped<JobTelemetryCapture>();
        builder.Services.AddScoped<JobProcessor>();
        builder.Services.AddScoped<WorkflowService>();
        builder.Services.AddSingleton<JobQueue>();
        builder.Services.AddSingleton<ISecretProtector, StubSecretProtector>();
        configure?.Invoke(builder.Services);
        return builder.Build();
    }

    static List<RouteEndpoint> routeEndpoints(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

    sealed class StubSecretProtector : ISecretProtector
    {
        public string? Protect(string? plaintext) => plaintext;

        public string? Unprotect(string? ciphertext) => ciphertext;
    }
}
