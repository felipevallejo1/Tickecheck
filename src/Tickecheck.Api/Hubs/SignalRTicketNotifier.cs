using Microsoft.AspNetCore.SignalR;
using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Api.Hubs;

// Payloads sent to the browser. They carry only what is needed to show a notification
// and a link; the client asks the API for the rest, which applies all the access rules again.
public record TicketAssignedNotification(Guid TicketId, string Title);
public record CommentAddedNotification(Guid TicketId, string TicketTitle, Guid AuthorId, bool IsInternal);

public class SignalRTicketNotifier(IHubContext<NotificationsHub> hubContext) : ITicketNotifier
{
    public const string TicketAssignedEvent = "TicketAssigned";
    public const string CommentAddedEvent = "CommentAdded";

    public Task TicketAssignedAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        if (ticket.AssignedAgentId is null)
        {
            return Task.CompletedTask;
        }

        return hubContext.Clients
            .Group(HubGroups.User(ticket.AssignedAgentId.Value))
            .SendAsync(TicketAssignedEvent, new TicketAssignedNotification(ticket.Id, ticket.Title), cancellationToken);
    }

    public Task CommentAddedAsync(Ticket ticket, Comment comment, CancellationToken cancellationToken = default)
    {
        var notification = new CommentAddedNotification(ticket.Id, ticket.Title, comment.AuthorId, comment.IsInternal);

        // Staff of the company always hears about a new comment.
        var groups = new List<string> { HubGroups.Staff(ticket.TenantId) };

        // The requester only hears about public comments. An internal one is never
        // sent to them, not even as a "something happened" signal.
        if (!comment.IsInternal)
        {
            groups.Add(HubGroups.User(ticket.RequesterId));
        }

        return hubContext.Clients.Groups(groups).SendAsync(CommentAddedEvent, notification, cancellationToken);
    }
}
