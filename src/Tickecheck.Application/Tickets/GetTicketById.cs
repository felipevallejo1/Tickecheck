using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;

namespace Tickecheck.Application.Tickets;

public class GetTicketByIdHandler(
    ITicketRepository tickets,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<TicketDto> HandleAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // A ticket of another tenant comes back as null here (query filter), so it
        // produces the same 404 as an id that does not exist at all.
        var ticket = await tickets.GetDetailsAsync(ticketId, utcNow, cancellationToken)
            ?? throw new NotFoundException("Ticket not found.");

        AccessRules.EnsureCanAccessTicket(currentUser, ticket.RequesterId);

        return ticket;
    }
}
