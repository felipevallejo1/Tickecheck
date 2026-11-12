using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Dashboard;

public record DashboardStatsDto(
    int TotalTickets,
    int OverdueTickets,
    // Null when no ticket has been resolved yet: an average of nothing is not zero.
    double? AverageResolutionHours,
    Dictionary<TicketStatus, int> TicketsByStatus,
    Dictionary<TicketPriority, int> TicketsByPriority);

/// <summary>
/// Statistics of the current tenant. The repository computes them with aggregate
/// queries in the database instead of loading every ticket into memory.
/// </summary>
public class GetDashboardStatsHandler(ITicketRepository tickets, TimeProvider timeProvider)
{
    public Task<DashboardStatsDto> HandleAsync(CancellationToken cancellationToken = default)
    {
        return tickets.GetStatsAsync(timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
