using Tickecheck.Domain.Common;

namespace Tickecheck.Domain.Tickets;

public class Ticket : ITenantEntity
{
    // The whole state machine in one place: status -> statuses it can move to.
    // Closed has no entry on the right side because it is terminal.
    private static readonly Dictionary<TicketStatus, TicketStatus[]> AllowedTransitions = new()
    {
        [TicketStatus.Open] = [TicketStatus.InProgress, TicketStatus.Closed],
        [TicketStatus.InProgress] = [TicketStatus.WaitingOnCustomer, TicketStatus.Resolved],
        [TicketStatus.WaitingOnCustomer] = [TicketStatus.InProgress, TicketStatus.Resolved],
        [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.Reopened],
        [TicketStatus.Reopened] = [TicketStatus.InProgress, TicketStatus.Closed],
        [TicketStatus.Closed] = []
    };

    private readonly List<TicketHistory> _history = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid? AssignedAgentId { get; private set; }
    public DateTime DueAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    // Exposed as read-only so history can only be added through the methods below.
    public IReadOnlyCollection<TicketHistory> History => _history;

    // Required by EF Core to materialize the entity.
    private Ticket()
    {
    }

    public Ticket(
        string title,
        string description,
        TicketPriority priority,
        Guid categoryId,
        Guid requesterId,
        DateTime utcNow)
    {
        EnsureTitle(title);

        Id = Guid.NewGuid();
        Title = title.Trim();
        Description = description;
        Priority = priority;
        Status = TicketStatus.Open;
        CategoryId = categoryId;
        RequesterId = requesterId;
        CreatedAt = utcNow;
        DueAt = SlaPolicy.CalculateDueAt(priority, utcNow);
    }

    public void UpdateDetails(string title, string description, Guid categoryId)
    {
        EnsureNotClosed();
        EnsureTitle(title);

        Title = title.Trim();
        Description = description;
        CategoryId = categoryId;
    }

    public void ChangeStatus(TicketStatus newStatus, Guid changedByUserId, DateTime utcNow)
    {
        if (!AllowedTransitions[Status].Contains(newStatus))
        {
            throw new DomainException($"A ticket cannot go from {Status} to {newStatus}.");
        }

        if (newStatus == TicketStatus.InProgress && AssignedAgentId is null)
        {
            throw new DomainException("A ticket needs an assigned agent before work can start.");
        }

        var oldStatus = Status;
        Status = newStatus;

        if (newStatus == TicketStatus.Resolved)
        {
            ResolvedAt = utcNow;
        }
        else if (newStatus == TicketStatus.Reopened)
        {
            // The earlier resolution did not hold, so it must not count as resolution time.
            ResolvedAt = null;
        }
        else if (newStatus == TicketStatus.Closed)
        {
            ClosedAt = utcNow;
        }

        AddHistory(TicketChangeType.StatusChanged, oldStatus.ToString(), newStatus.ToString(), changedByUserId, utcNow);
    }

    public void AssignTo(Guid agentId, Guid changedByUserId, DateTime utcNow)
    {
        EnsureNotClosed();

        if (AssignedAgentId == agentId)
        {
            // Nothing changed, so there is nothing to audit.
            return;
        }

        var oldAgentId = AssignedAgentId;
        AssignedAgentId = agentId;

        AddHistory(TicketChangeType.AssigneeChanged, oldAgentId?.ToString(), agentId.ToString(), changedByUserId, utcNow);
    }

    public void ChangePriority(TicketPriority newPriority, Guid changedByUserId, DateTime utcNow)
    {
        EnsureNotClosed();

        if (Priority == newPriority)
        {
            return;
        }

        var oldPriority = Priority;
        Priority = newPriority;
        // The deadline always counts from creation: raising the priority late
        // must not give the ticket a fresh full SLA window.
        DueAt = SlaPolicy.CalculateDueAt(newPriority, CreatedAt);

        AddHistory(TicketChangeType.PriorityChanged, oldPriority.ToString(), newPriority.ToString(), changedByUserId, utcNow);
    }

    public bool IsOverdue(DateTime utcNow)
    {
        var isFinished = Status is TicketStatus.Resolved or TicketStatus.Closed;
        return !isFinished && utcNow > DueAt;
    }

    private void AddHistory(
        TicketChangeType changeType,
        string? oldValue,
        string? newValue,
        Guid changedByUserId,
        DateTime utcNow)
    {
        _history.Add(new TicketHistory(Id, changedByUserId, changeType, oldValue, newValue, utcNow));
    }

    private void EnsureNotClosed()
    {
        if (Status == TicketStatus.Closed)
        {
            throw new DomainException("A closed ticket cannot be modified.");
        }
    }

    private static void EnsureTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Ticket title is required.");
        }
    }
}
