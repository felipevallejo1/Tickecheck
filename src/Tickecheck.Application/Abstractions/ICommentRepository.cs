using Tickecheck.Application.Comments;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Abstractions;

public interface ICommentRepository
{
    /// <param name="includeInternal">False for requesters: internal notes are filtered out in the query itself.</param>
    Task<List<CommentDto>> ListByTicketAsync(Guid ticketId, bool includeInternal, CancellationToken cancellationToken = default);

    void Add(Comment comment);
}
