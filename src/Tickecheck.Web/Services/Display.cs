using System.Security.Claims;
using MudBlazor;
using Tickecheck.Web.Models;

namespace Tickecheck.Web.Services;

/// <summary>
/// How values are shown: labels, colors and date formats, in one place.
/// </summary>
public static class Display
{
    public static string Label(TicketStatus status) => status switch
    {
        TicketStatus.InProgress => "In progress",
        TicketStatus.WaitingOnCustomer => "Waiting on customer",
        _ => status.ToString()
    };

    public static Color ColorOf(TicketStatus status) => status switch
    {
        TicketStatus.Open or TicketStatus.Reopened => Color.Info,
        TicketStatus.InProgress => Color.Primary,
        TicketStatus.WaitingOnCustomer => Color.Warning,
        TicketStatus.Resolved => Color.Success,
        _ => Color.Default
    };

    public static Color ColorOf(TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => Color.Error,
        TicketPriority.High => Color.Warning,
        TicketPriority.Medium => Color.Info,
        _ => Color.Default
    };

    public static string Label(UserRole role) => role switch
    {
        UserRole.TenantAdmin => "Company admin",
        UserRole.SuperAdmin => "Platform admin",
        _ => role.ToString()
    };

    public static string Label(TicketChangeType changeType) => changeType switch
    {
        TicketChangeType.StatusChanged => "Status",
        TicketChangeType.AssigneeChanged => "Assignee",
        _ => "Priority"
    };

    /// <summary>The API works in UTC; people read dates in their local time.</summary>
    public static string Date(DateTime utc)
    {
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd MMM yyyy HH:mm");
    }

    // Shortcuts to read the signed-in user from the claims of the token.

    public static Guid UserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirst("sub")!.Value);

    public static bool IsStaff(this ClaimsPrincipal user) =>
        user.IsInRole(nameof(UserRole.Agent)) || user.IsInRole(nameof(UserRole.TenantAdmin));
}
