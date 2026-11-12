using FluentValidation;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Tickets;

public record ChangeTicketPriorityCommand(Guid TicketId, TicketPriority Priority);

public class ChangeTicketPriorityValidator : AbstractValidator<ChangeTicketPriorityCommand>
{
    public ChangeTicketPriorityValidator()
    {
        RuleFor(command => command.Priority).IsInEnum();
    }
}

public class ChangeTicketPriorityHandler(
    ITicketRepository tickets,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<TicketDto> HandleAsync(ChangeTicketPriorityCommand command, CancellationToken cancellationToken = default)
    {
        new ChangeTicketPriorityValidator().ValidateAndThrow(command);

        var ticket = await tickets.GetByIdAsync(command.TicketId, cancellationToken)
            ?? throw new NotFoundException("Ticket not found.");

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // The entity also recalculates the SLA deadline for the new priority.
        ticket.ChangePriority(command.Priority, currentUser.UserId, utcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await tickets.GetDetailsAsync(ticket.Id, utcNow, cancellationToken))!;
    }
}
