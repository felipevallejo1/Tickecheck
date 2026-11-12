using Microsoft.EntityFrameworkCore;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Application.Dashboard;
using Tickecheck.Application.Tickets;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Infrastructure.Persistence.Repositories;

// There is no "WHERE TenantId = ..." anywhere in this class. The global query filter
// of AppDbContext adds it to every query, including each side of the joins.
public class TicketRepository(AppDbContext db) : ITicketRepository
{
    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return db.Tickets.FirstOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);
    }

    public Task<TicketDto?> GetDetailsAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return ProjectToDto(db.Tickets.Where(ticket => ticket.Id == id), utcNow)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<TicketDto>> SearchAsync(
        SearchTicketsQuery query,
        Guid? requesterId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        // AsNoTracking: these rows are only read, so EF Core does not need to watch them for changes.
        var tickets = db.Tickets.AsNoTracking();

        // Each filter is added only if the client sent it. Nothing runs yet:
        // an IQueryable is a description of a query that EF Core turns into SQL later.
        if (requesterId is not null)
        {
            tickets = tickets.Where(ticket => ticket.RequesterId == requesterId);
        }

        if (query.Status is not null)
        {
            tickets = tickets.Where(ticket => ticket.Status == query.Status);
        }

        if (query.Priority is not null)
        {
            tickets = tickets.Where(ticket => ticket.Priority == query.Priority);
        }

        if (query.AssignedAgentId is not null)
        {
            tickets = tickets.Where(ticket => ticket.AssignedAgentId == query.AssignedAgentId);
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            tickets = tickets.Where(ticket => ticket.Title.Contains(text) || ticket.Description.Contains(text));
        }

        // Two queries reach the database: one COUNT for the pager and one for the page itself.
        var totalCount = await tickets.CountAsync(cancellationToken);

        var items = await ApplySort(ProjectToDto(tickets, utcNow), query.SortBy, query.SortDescending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<List<TicketHistoryDto>> GetHistoryAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var entries = await (
            from history in db.TicketHistory.AsNoTracking()
            join user in db.Users on history.ChangedByUserId equals user.Id
            where history.TicketId == ticketId
            orderby history.OccurredAt
            select new TicketHistoryDto(
                history.Id,
                history.ChangeType,
                history.OldValue,
                history.NewValue,
                user.FullName,
                history.OccurredAt))
            .ToListAsync(cancellationToken);

        // Assignee changes store user ids. They are replaced by names here, with one
        // extra query for all of them, so the history is readable for people.
        var agentIds = entries
            .Where(entry => entry.ChangeType == TicketChangeType.AssigneeChanged)
            .SelectMany(entry => new[] { entry.OldValue, entry.NewValue })
            .Where(value => value is not null)
            .Select(value => Guid.Parse(value!))
            .Distinct()
            .ToList();

        if (agentIds.Count == 0)
        {
            return entries;
        }

        var agentNames = await db.Users
            .Where(user => agentIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id.ToString(), user => user.FullName, cancellationToken);

        return entries
            .Select(entry => entry.ChangeType == TicketChangeType.AssigneeChanged
                ? entry with
                {
                    OldValue = entry.OldValue is null ? null : agentNames.GetValueOrDefault(entry.OldValue, entry.OldValue),
                    NewValue = entry.NewValue is null ? null : agentNames.GetValueOrDefault(entry.NewValue, entry.NewValue)
                }
                : entry)
            .ToList();
    }

    public async Task<DashboardStatsDto> GetStatsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        // GROUP BY in the database: only one row per status comes back, not every ticket.
        var countsByStatus = await db.Tickets
            .GroupBy(ticket => ticket.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var countsByPriority = await db.Tickets
            .GroupBy(ticket => ticket.Priority)
            .Select(group => new { Priority = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        // Same rule as Ticket.IsOverdue, written again so it can run as SQL.
        var overdueCount = await db.Tickets.CountAsync(
            ticket => ticket.Status != TicketStatus.Resolved
                && ticket.Status != TicketStatus.Closed
                && ticket.DueAt < utcNow,
            cancellationToken);

        // The cast to double? makes the average null (instead of throwing) when nothing is resolved yet.
        var averageMinutes = await db.Tickets
            .Where(ticket => ticket.ResolvedAt != null)
            .AverageAsync(
                ticket => (double?)EF.Functions.DateDiffMinute(ticket.CreatedAt, ticket.ResolvedAt!.Value),
                cancellationToken);

        // Every status and priority is present in the result, with 0 when there are no tickets.
        var ticketsByStatus = Enum.GetValues<TicketStatus>().ToDictionary(
            status => status,
            status => countsByStatus.FirstOrDefault(row => row.Status == status)?.Count ?? 0);

        var ticketsByPriority = Enum.GetValues<TicketPriority>().ToDictionary(
            priority => priority,
            priority => countsByPriority.FirstOrDefault(row => row.Priority == priority)?.Count ?? 0);

        return new DashboardStatsDto(
            ticketsByStatus.Values.Sum(),
            overdueCount,
            averageMinutes is null ? null : Math.Round(averageMinutes.Value / 60, 1),
            ticketsByStatus,
            ticketsByPriority);
    }

    public void Add(Ticket ticket)
    {
        db.Tickets.Add(ticket);
    }

    public void Remove(Ticket ticket)
    {
        db.Tickets.Remove(ticket);
    }

    /// <summary>
    /// Turns tickets into DTOs inside the SQL query: the database returns only the
    /// columns the DTO needs, with the category and user names already joined.
    /// </summary>
    private IQueryable<TicketDto> ProjectToDto(IQueryable<Ticket> tickets, DateTime utcNow)
    {
        return
            from ticket in tickets
            join category in db.Categories on ticket.CategoryId equals category.Id
            join requester in db.Users on ticket.RequesterId equals requester.Id
            // Left join: a ticket without an assigned agent must still appear.
            join agent in db.Users on ticket.AssignedAgentId equals (Guid?)agent.Id into agents
            from agent in agents.DefaultIfEmpty()
            select new TicketDto
            {
                Id = ticket.Id,
                Title = ticket.Title,
                Description = ticket.Description,
                Priority = ticket.Priority,
                Status = ticket.Status,
                CategoryId = ticket.CategoryId,
                CategoryName = category.Name,
                RequesterId = ticket.RequesterId,
                RequesterName = requester.FullName,
                AssignedAgentId = ticket.AssignedAgentId,
                AssignedAgentName = agent != null ? agent.FullName : null,
                DueAt = ticket.DueAt,
                // Same rule as Ticket.IsOverdue, written again so it can run as SQL.
                IsOverdue = ticket.Status != TicketStatus.Resolved
                    && ticket.Status != TicketStatus.Closed
                    && ticket.DueAt < utcNow,
                CreatedAt = ticket.CreatedAt,
                ResolvedAt = ticket.ResolvedAt,
                ClosedAt = ticket.ClosedAt
            };
    }

    private static IQueryable<TicketDto> ApplySort(IQueryable<TicketDto> tickets, string sortBy, bool descending)
    {
        var sorted = sortBy switch
        {
            TicketSortFields.DueAt => descending ? tickets.OrderByDescending(t => t.DueAt) : tickets.OrderBy(t => t.DueAt),
            TicketSortFields.Priority => descending ? tickets.OrderByDescending(t => t.Priority) : tickets.OrderBy(t => t.Priority),
            TicketSortFields.Status => descending ? tickets.OrderByDescending(t => t.Status) : tickets.OrderBy(t => t.Status),
            TicketSortFields.Title => descending ? tickets.OrderByDescending(t => t.Title) : tickets.OrderBy(t => t.Title),
            _ => descending ? tickets.OrderByDescending(t => t.CreatedAt) : tickets.OrderBy(t => t.CreatedAt)
        };

        // Second sort key: without it, rows with equal values could jump between pages.
        return sorted.ThenBy(t => t.Id);
    }
}
