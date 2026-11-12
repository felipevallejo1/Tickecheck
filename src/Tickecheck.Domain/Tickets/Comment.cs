using Tickecheck.Domain.Common;

namespace Tickecheck.Domain.Tickets;

public class Comment : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = null!;

    // Internal comments are notes between staff. A Requester must never see them.
    public bool IsInternal { get; private set; }

    public DateTime CreatedAt { get; private set; }

    // Required by EF Core to materialize the entity.
    private Comment()
    {
    }

    public Comment(Guid ticketId, Guid authorId, string body, bool isInternal, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainException("Comment body is required.");
        }

        Id = Guid.NewGuid();
        TicketId = ticketId;
        AuthorId = authorId;
        Body = body.Trim();
        IsInternal = isInternal;
        CreatedAt = utcNow;
    }
}
