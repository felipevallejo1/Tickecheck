using FluentValidation;
using FluentValidation.Results;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Tickets;

public record AssignTicketCommand(Guid TicketId, Guid AgentId);

public class AssignTicketHandler(
    ITicketRepository tickets,
    IUserRepository users,
    ICurrentUser currentUser,
    ITicketNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<TicketDto> HandleAsync(AssignTicketCommand command, CancellationToken cancellationToken = default)
    {
        var ticket = await tickets.GetByIdAsync(command.TicketId, cancellationToken)
            ?? throw new NotFoundException("Ticket not found.");

        // The user lookup is tenant-filtered: an agent of another company is not found.
        var agent = await users.GetByIdAsync(command.AgentId, cancellationToken);
        if (agent is null || agent.Role is not (UserRole.Agent or UserRole.TenantAdmin))
        {
            throw new ValidationException([new ValidationFailure(nameof(command.AgentId), "Tickets can only be assigned to an agent or admin of the company.")]);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var previousAgentId = ticket.AssignedAgentId;

        // The entity checks its own rules (closed tickets cannot be reassigned) and writes the history.
        ticket.AssignTo(agent.Id, currentUser.UserId, utcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify only after the change is safely saved, and only if it really changed.
        if (previousAgentId != ticket.AssignedAgentId)
        {
            await notifier.TicketAssignedAsync(ticket, cancellationToken);
        }

        return (await tickets.GetDetailsAsync(ticket.Id, utcNow, cancellationToken))!;
    }
}
