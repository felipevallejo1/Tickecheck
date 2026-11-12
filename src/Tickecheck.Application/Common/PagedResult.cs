namespace Tickecheck.Application.Common;

/// <summary>
/// One page of a list plus the total, so the client can draw the pager.
/// </summary>
public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount);
