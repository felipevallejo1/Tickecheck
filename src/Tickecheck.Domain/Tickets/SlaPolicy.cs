namespace Tickecheck.Domain.Tickets;

/// <summary>
/// Maximum time to resolve a ticket, by priority.
/// Calendar hours (no business hours or holidays) and the same for every tenant.
/// </summary>
public static class SlaPolicy
{
    public static TimeSpan GetResolutionTime(TicketPriority priority)
    {
        return priority switch
        {
            TicketPriority.Critical => TimeSpan.FromHours(4),
            TicketPriority.High => TimeSpan.FromHours(8),
            TicketPriority.Medium => TimeSpan.FromHours(24),
            TicketPriority.Low => TimeSpan.FromHours(72),
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown priority.")
        };
    }

    public static DateTime CalculateDueAt(TicketPriority priority, DateTime createdAt)
    {
        return createdAt + GetResolutionTime(priority);
    }
}
