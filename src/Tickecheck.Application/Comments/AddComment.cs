using FluentValidation;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Comments;

public record CommentDto(Guid Id, Guid TicketId, Guid AuthorId, string AuthorName, string Body, bool IsInternal, DateTime CreatedAt);

public record AddCommentCommand(Guid TicketId, string Body, bool IsInternal);

public class AddCommentValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentValidator()
    {
        RuleFor(command => command.Body).NotEmpty().MaximumLength(4000);
    }
}

public class AddCommentHandler(
    ITicketRepository tickets,
    ICommentRepository comments,
    ICurrentUser currentUser,
    ITicketNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CommentDto> HandleAsync(AddCommentCommand command, CancellationToken cancellationToken = default)
    {
        new AddCommentValidator().ValidateAndThrow(command);

        var ticket = await tickets.GetByIdAsync(command.TicketId, cancellationToken)
            ?? throw new NotFoundException("Ticket not found.");

        AccessRules.EnsureCanAccessTicket(currentUser, ticket.RequesterId);

        // Whoever cannot read internal notes cannot write them either.
        if (command.IsInternal && !AccessRules.CanSeeInternalComments(currentUser))
        {
            throw new ForbiddenException("Only staff can write internal comments.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var comment = new Comment(ticket.Id, currentUser.UserId, command.Body, command.IsInternal, utcNow);

        comments.Add(comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifier.CommentAddedAsync(ticket, comment, cancellationToken);

        return new CommentDto(
            comment.Id,
            comment.TicketId,
            comment.AuthorId,
            currentUser.FullName,
            comment.Body,
            comment.IsInternal,
            comment.CreatedAt);
    }
}
