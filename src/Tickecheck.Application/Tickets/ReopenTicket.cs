using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Tickets;

/// <summary>
/// The one status change a Requester can make: reopening their own resolved ticket.
/// It is a separate use case (and endpoint) so the general "change status" endpoint
/// can stay limited to staff with a simple role policy.
/// </summary>
public class ReopenTicketHandler(
    ITicketRepository tickets,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<TicketDto> HandleAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new NotFoundException("Ticket not found.");

        AccessRules.EnsureCanAccessTicket(currentUser, ticket.RequesterId);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        ticket.ChangeStatus(TicketStatus.Reopened, currentUser.UserId, utcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await tickets.GetDetailsAsync(ticket.Id, utcNow, cancellationToken))!;
    }
}
