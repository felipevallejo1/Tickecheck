using FluentValidation;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Tickets;

public record ChangeTicketStatusCommand(Guid TicketId, TicketStatus Status);

public class ChangeTicketStatusValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusValidator()
    {
        RuleFor(command => command.Status).IsInEnum();
    }
}

/// <summary>
/// Used by staff. The handler does not know which transitions are valid:
/// it asks the entity, which throws a DomainException if the change is not allowed.
/// </summary>
public class ChangeTicketStatusHandler(
    ITicketRepository tickets,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<TicketDto> HandleAsync(ChangeTicketStatusCommand command, CancellationToken cancellationToken = default)
    {
        new ChangeTicketStatusValidator().ValidateAndThrow(command);

        var ticket = await tickets.GetByIdAsync(command.TicketId, cancellationToken)
            ?? throw new NotFoundException("Ticket not found.");

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        ticket.ChangeStatus(command.Status, currentUser.UserId, utcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await tickets.GetDetailsAsync(ticket.Id, utcNow, cancellationToken))!;
    }
}
