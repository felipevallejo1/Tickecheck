using Tickecheck.Domain.Common;

namespace Tickecheck.Domain.Tickets;

/// <summary>
/// Audit record of one change made to a ticket. It is never updated or deleted.
/// </summary>
public class TicketHistory : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public TicketChangeType ChangeType { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTime OccurredAt { get; private set; }

    // Required by EF Core to materialize the entity.
    private TicketHistory()
    {
    }

    // Internal: only Ticket can create history, so a record always matches a real change.
    internal TicketHistory(
        Guid ticketId,
        Guid changedByUserId,
        TicketChangeType changeType,
        string? oldValue,
        string? newValue,
        DateTime utcNow)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        ChangedByUserId = changedByUserId;
        ChangeType = changeType;
        OldValue = oldValue;
        NewValue = newValue;
        OccurredAt = utcNow;
    }
}
