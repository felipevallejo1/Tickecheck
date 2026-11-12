using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;

namespace Tickecheck.Application.Tickets;

public class GetTicketHistoryHandler(ITicketRepository tickets, ICurrentUser currentUser)
{
    public async Task<List<TicketHistoryDto>> HandleAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new NotFoundException("Ticket not found.");

        AccessRules.EnsureCanAccessTicket(currentUser, ticket.RequesterId);

        return await tickets.GetHistoryAsync(ticketId, cancellationToken);
    }
}
