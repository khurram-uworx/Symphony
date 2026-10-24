using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Symphony.Api;
using Symphony.Data;
using Symphony.Processing;
using Symphony.Security;
using Symphony.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services.AddControllersWithViews();
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
        serviceProvider.GetService<WorkflowService>()));
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
        serviceProvider.GetService<JobQueue>()));

builder.Services.AddHostedService<JobProcessor>();
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapConfigurationEndpoints();
app.MapWorkflowEndpoints();
app.MapWorkEndpoints();
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
        Console.Out);
}

app.Run();