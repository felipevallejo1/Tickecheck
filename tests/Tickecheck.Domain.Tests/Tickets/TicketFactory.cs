using Tickecheck.Domain.Tickets;

namespace Tickecheck.Domain.Tests.Tickets;

/// <summary>
/// Builds tickets for tests. A ticket can only reach a status through
/// ChangeStatus, so the factory walks a valid path to get there.
/// </summary>
internal static class TicketFactory
{
    public static readonly DateTime CreatedAt = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);
    public static readonly Guid RequesterId = Guid.NewGuid();
    public static readonly Guid AgentId = Guid.NewGuid();

    private static readonly Dictionary<TicketStatus, TicketStatus[]> PathFromOpen = new()
    {
        [TicketStatus.Open] = [],
        [TicketStatus.InProgress] = [TicketStatus.InProgress],
        [TicketStatus.WaitingOnCustomer] = [TicketStatus.InProgress, TicketStatus.WaitingOnCustomer],
        [TicketStatus.Resolved] = [TicketStatus.InProgress, TicketStatus.Resolved],
        [TicketStatus.Reopened] = [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Reopened],
        [TicketStatus.Closed] = [TicketStatus.Closed]
    };

    public static Ticket Create(TicketPriority priority = TicketPriority.Medium)
    {
        return new Ticket("Printer is not working", "It shows error E-05.", priority, Guid.NewGuid(), RequesterId, CreatedAt);
    }

    public static Ticket CreateInStatus(TicketStatus status)
    {
        var ticket = Create();
        ticket.AssignTo(AgentId, AgentId, CreatedAt);

        foreach (var step in PathFromOpen[status])
        {
            ticket.ChangeStatus(step, AgentId, CreatedAt);
        }

        return ticket;
    }
}
