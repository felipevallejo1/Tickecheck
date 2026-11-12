using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;

namespace Tickecheck.Application.Tickets;

public class DeleteTicketHandler(ITicketRepository tickets, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new NotFoundException("Ticket not found.");

        // Comments and history are removed by the database (cascade delete).
        tickets.Remove(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
