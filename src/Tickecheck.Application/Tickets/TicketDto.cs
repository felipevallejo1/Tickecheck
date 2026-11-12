using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Tickets;

/// <summary>
/// What the API returns for a ticket. It is a separate class from the entity so the
/// JSON contract does not change every time the domain model does.
/// </summary>
public class TicketDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public TicketPriority Priority { get; init; }
    public TicketStatus Status { get; init; }
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public Guid RequesterId { get; init; }
    public string RequesterName { get; init; } = string.Empty;
    public Guid? AssignedAgentId { get; init; }
    public string? AssignedAgentName { get; init; }
    public DateTime DueAt { get; init; }
    public bool IsOverdue { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
}

public record TicketHistoryDto(
    Guid Id,
    TicketChangeType ChangeType,
    string? OldValue,
    string? NewValue,
    string ChangedByName,
    DateTime OccurredAt);
