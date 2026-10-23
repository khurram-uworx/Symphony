using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Symphony.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services.AddControllersWithViews();
builder.Services.AddSymphonyDatabase(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddSymphonyIdentity();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await DatabaseInitializer.InitializeAsync(
    app.Services.GetRequiredService<IDbContextFactory<SymphonyDbContext>>());

app.Run();
