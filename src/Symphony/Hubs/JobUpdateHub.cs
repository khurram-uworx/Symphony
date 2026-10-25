using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Symphony.Hubs;

[Authorize]
public sealed class JobUpdateHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId(Context.User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}", Context.ConnectionAborted);
        }

        if (IsAdmin(Context.User))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins", Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId(Context.User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}", Context.ConnectionAborted);
        }

        if (IsAdmin(Context.User))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins", Context.ConnectionAborted);
        }

        await base.OnDisconnectedAsync(exception);
    }

    static string? GetUserId(ClaimsPrincipal? user) =>
        user?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user?.FindFirstValue("sub")
        ?? user?.Identity?.Name;

    static bool IsAdmin(ClaimsPrincipal? user) =>
        user?.IsInRole("Admin") == true;
}
