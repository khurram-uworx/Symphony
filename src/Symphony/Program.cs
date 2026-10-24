using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Symphony.Data;
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
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<ConfigurationService>();

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