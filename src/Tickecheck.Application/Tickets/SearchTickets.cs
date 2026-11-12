using FluentValidation;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Application.Tickets;

/// <summary>
/// Fields the list can be sorted by. A fixed list (and not "any property name")
/// so clients cannot sort by columns we did not intend to expose.
/// </summary>
public static class TicketSortFields
{
    public const string CreatedAt = "createdAt";
    public const string DueAt = "dueAt";
    public const string Priority = "priority";
    public const string Status = "status";
    public const string Title = "title";

    public static readonly string[] All = [CreatedAt, DueAt, Priority, Status, Title];
}

public class SearchTicketsQuery
{
    public TicketStatus? Status { get; init; }
    public TicketPriority? Priority { get; init; }
    public Guid? AssignedAgentId { get; init; }

    /// <summary>Searched in title and description.</summary>
    public string? Text { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string SortBy { get; init; } = TicketSortFields.CreatedAt;
    public bool SortDescending { get; init; } = true;
}

public class SearchTicketsValidator : AbstractValidator<SearchTicketsQuery>
{
    public SearchTicketsValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        // The upper limit stops a client from asking for the whole table in one request.
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Text).MaximumLength(200);
        RuleFor(query => query.SortBy)
            .Must(sortBy => TicketSortFields.All.Contains(sortBy))
            .WithMessage($"SortBy must be one of: {string.Join(", ", TicketSortFields.All)}.");
    }
}

public class SearchTicketsHandler(
    ITicketRepository tickets,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public Task<PagedResult<TicketDto>> HandleAsync(SearchTicketsQuery query, CancellationToken cancellationToken = default)
    {
        new SearchTicketsValidator().ValidateAndThrow(query);

        var requesterScope = AccessRules.GetRequesterScope(currentUser);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        return tickets.SearchAsync(query, requesterScope, utcNow, cancellationToken);
    }
}
