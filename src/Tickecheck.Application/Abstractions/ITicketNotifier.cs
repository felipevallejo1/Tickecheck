using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Abstractions;

/// <summary>
/// Pushes real-time notifications to connected users. The use cases only say WHAT
/// happened; the implementation (SignalR, in the Api project) decides who receives it.
/// </summary>
public interface ITicketNotifier
{
    Task TicketAssignedAsync(Ticket ticket, CancellationToken cancellationToken = default);

    Task CommentAddedAsync(Ticket ticket, Comment comment, CancellationToken cancellationToken = default);
}
