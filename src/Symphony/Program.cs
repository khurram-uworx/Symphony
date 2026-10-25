using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Symphony.Api;
using Symphony.Data;
using Symphony.Processing;
using Symphony.Security;
using Symphony.Services;
using Symphony.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddAntiforgery();
builder.Services.AddSymphonyDatabase(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddSymphonyDataProtection(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddSymphonySecretProtection(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddSymphonyIdentity(builder.Configuration, builder.Environment);
builder.Services.AddScoped<UserManagementService>(serviceProvider =>
    new UserManagementService(
        serviceProvider.GetRequiredService<UserManager<IdentityUser>>(),
        serviceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        serviceProvider.GetService<SignInManager<IdentityUser>>(),
        serviceProvider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>(),
        serviceProvider.GetService<WorkflowService>(),
        serviceProvider.GetService<ApiKeyService>()));
builder.Services.AddScoped<ConfigurationService>(serviceProvider =>
    new ConfigurationService(
        serviceProvider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>(),
        serviceProvider.GetRequiredService<ISecretProtector>()));

builder.Services.AddScoped<WorkflowService>(serviceProvider =>
    new WorkflowService(
        serviceProvider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>()));

builder.Services.AddSingleton(JobQueue.Default);
builder.Services.AddSingleton<IJobQueue>(JobQueue.Default);
builder.Services.AddScoped<JobService>(serviceProvider =>
    new JobService(
        serviceProvider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>(),
        serviceProvider.GetRequiredService<ISecretProtector>(),
        serviceProvider.GetService<JobQueue>(),
        serviceProvider.GetRequiredService<ILogger<JobService>>()));

builder.Services.AddSingleton<JobProcessor>(serviceProvider =>
    new JobProcessor(
        serviceProvider.GetRequiredService<IServiceScopeFactory>(),
        serviceProvider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>(),
        serviceProvider.GetRequiredService<IJobQueue>(),
        serviceProvider.GetRequiredService<ILogger<JobProcessor>>()));
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<JobProcessor>());

builder.Services.AddScoped<ApiKeyService>(serviceProvider =>
    new ApiKeyService(
        serviceProvider.GetRequiredService<IDbContextFactory<SymphonyDbContext>>(),
        serviceProvider.GetRequiredService<UserManager<IdentityUser>>(),
        serviceProvider.GetRequiredService<IConfiguration>(),
        serviceProvider.GetService<IWebHostEnvironment>()));

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy<string, ApiRateLimitPolicy>(ApiRateLimitPolicy.PolicyName);
});
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapConfigurationEndpoints();
app.MapWorkflowEndpoints();
app.MapWorkEndpoints();
app.MapJobEndpoints();
app.MapApiKeyEndpoints();
app.MapHub<Symphony.Hubs.JobUpdateHub>("/hubs/jobs");
await DatabaseInitializer.InitializeAsync(
    app.Services.GetRequiredService<IDbContextFactory<SymphonyDbContext>>());

await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    await AdminSeeder.SeedAsync(
        services.GetRequiredService<SymphonyDbContext>(),
        services.GetRequiredService<UserManager<IdentityUser>>(),
        services.GetRequiredService<RoleManager<IdentityRole>>(),
        app.Configuration["SeedAdmin:UserName"],
        Console.Out,
        app.Configuration["SeedAdmin:Password"]);
}

app.Run();