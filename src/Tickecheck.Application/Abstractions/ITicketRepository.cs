using Tickecheck.Application.Common;
using Tickecheck.Application.Dashboard;
using Tickecheck.Application.Tickets;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Abstractions;

/// <summary>
/// Every method works only on tickets of the current tenant.
/// </summary>
public interface ITicketRepository
{
    /// <summary>Loads the entity, to change it through its domain methods.</summary>
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads the read model (with category and user names), to show it.</summary>
    Task<TicketDto?> GetDetailsAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <param name="requesterId">When set, only tickets of that requester are returned.</param>
    Task<PagedResult<TicketDto>> SearchAsync(
        SearchTicketsQuery query,
        Guid? requesterId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<List<TicketHistoryDto>> GetHistoryAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<DashboardStatsDto> GetStatsAsync(DateTime utcNow, CancellationToken cancellationToken = default);

    void Add(Ticket ticket);

    void Remove(Ticket ticket);
}
