using FluentValidation;
using FluentValidation.Results;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;

namespace Tickecheck.Application.Tickets;

public record UpdateTicketCommand(Guid TicketId, string Title, string Description, Guid CategoryId);

public class UpdateTicketValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(4000);
        RuleFor(command => command.CategoryId).NotEmpty();
    }
}

public class UpdateTicketHandler(
    ITicketRepository tickets,
    ICategoryRepository categories,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<TicketDto> HandleAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default)
    {
        new UpdateTicketValidator().ValidateAndThrow(command);

        var ticket = await tickets.GetByIdAsync(command.TicketId, cancellationToken)
            ?? throw new NotFoundException("Ticket not found.");

        AccessRules.EnsureCanAccessTicket(currentUser, ticket.RequesterId);

        if (!await categories.ExistsAsync(command.CategoryId, cancellationToken))
        {
            throw new ValidationException([new ValidationFailure(nameof(command.CategoryId), "The category does not exist.")]);
        }

        ticket.UpdateDetails(command.Title, command.Description, command.CategoryId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        return (await tickets.GetDetailsAsync(ticket.Id, utcNow, cancellationToken))!;
    }
}
