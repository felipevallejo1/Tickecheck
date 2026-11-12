using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Tickecheck.Application.Auth;
using Tickecheck.Domain.Users;

namespace Tickecheck.Api.Hubs;

/// <summary>
/// Names of the SignalR groups. A group is a named set of connections; a message
/// sent to a group reaches only the connections that were added to it.
/// </summary>
public static class HubGroups
{
    // The tenant id is part of the name, so a group can never mix two companies.
    public static string Staff(Guid tenantId) => $"tenant:{tenantId}:staff";

    public static string User(Guid userId) => $"user:{userId}";
}

/// <summary>
/// Clients only listen on this hub; they cannot call methods or choose groups.
/// The server decides the groups of each connection from the claims of its JWT,
/// which is what stops a client from subscribing to another company's events.
/// </summary>
[Authorize]
public class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;
        var userId = Guid.Parse(user.FindFirst(AppClaims.UserId)!.Value);

        // Personal group: events meant for one person (a ticket assigned to them,
        // a public comment on their own ticket).
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.User(userId));

        // Staff group of the company: receives every comment, including internal ones.
        // Requesters are never added to it.
        var isStaff = user.IsInRole(nameof(UserRole.Agent)) || user.IsInRole(nameof(UserRole.TenantAdmin));
        if (isStaff && Guid.TryParse(user.FindFirst(AppClaims.TenantId)?.Value, out var tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Staff(tenantId));
        }

        await base.OnConnectedAsync();
    }
}
