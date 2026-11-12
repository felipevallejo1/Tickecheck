using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;

namespace Tickecheck.Application.Comments;

public class GetCommentsHandler(
    ITicketRepository tickets,
    ICommentRepository comments,
    ICurrentUser currentUser)
{
    public async Task<List<CommentDto>> HandleAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new NotFoundException("Ticket not found.");

        AccessRules.EnsureCanAccessTicket(currentUser, ticket.RequesterId);

        // Internal comments are excluded in the database query, not hidden by the UI:
        // they never leave the server for a Requester.
        var includeInternal = AccessRules.CanSeeInternalComments(currentUser);

        return await comments.ListByTicketAsync(ticketId, includeInternal, cancellationToken);
    }
}
