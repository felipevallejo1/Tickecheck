using Microsoft.EntityFrameworkCore;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Comments;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Infrastructure.Persistence.Repositories;

public class CommentRepository(AppDbContext db) : ICommentRepository
{
    public Task<List<CommentDto>> ListByTicketAsync(Guid ticketId, bool includeInternal, CancellationToken cancellationToken = default)
    {
        var query =
            from comment in db.Comments.AsNoTracking()
            join author in db.Users on comment.AuthorId equals author.Id
            where comment.TicketId == ticketId && (includeInternal || !comment.IsInternal)
            orderby comment.CreatedAt
            select new CommentDto(
                comment.Id,
                comment.TicketId,
                comment.AuthorId,
                author.FullName,
                comment.Body,
                comment.IsInternal,
                comment.CreatedAt);

        return query.ToListAsync(cancellationToken);
    }

    public void Add(Comment comment)
    {
        db.Comments.Add(comment);
    }
}
