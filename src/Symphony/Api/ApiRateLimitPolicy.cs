using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Symphony.Api;

public sealed class ApiRateLimitPolicy : IRateLimiterPolicy<string>
{
    public const string PolicyName = "api";

    public static readonly FixedWindowRateLimiterOptions Options = new()
    {
        PermitLimit = 120,
        Window = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0
    };

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } =
        (context, _) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.Headers.RetryAfter = ((int)Options.Window.TotalSeconds).ToString();
            return ValueTask.CompletedTask;
        };

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var key = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => Options);
    }
}
