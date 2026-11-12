using FluentValidation;
using FluentValidation.Results;
using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Tickets;

public record CreateTicketCommand(string Title, string Description, TicketPriority Priority, Guid CategoryId);

public class CreateTicketValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(4000);
        RuleFor(command => command.Priority).IsInEnum();
        RuleFor(command => command.CategoryId).NotEmpty();
    }
}

public class CreateTicketHandler(
    ITicketRepository tickets,
    ICategoryRepository categories,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<TicketDto> HandleAsync(CreateTicketCommand command, CancellationToken cancellationToken = default)
    {
        new CreateTicketValidator().ValidateAndThrow(command);

        // The lookup is tenant-filtered, so a category of another company "does not exist".
        if (!await categories.ExistsAsync(command.CategoryId, cancellationToken))
        {
            throw new ValidationException([new ValidationFailure(nameof(command.CategoryId), "The category does not exist.")]);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // The requester is always the logged-in user; it is never taken from the request body.
        var ticket = new Ticket(
            command.Title,
            command.Description,
            command.Priority,
            command.CategoryId,
            currentUser.UserId,
            utcNow);

        tickets.Add(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await tickets.GetDetailsAsync(ticket.Id, utcNow, cancellationToken))!;
    }
}
