namespace Tickecheck.Domain.Common;

/// <summary>
/// Marks an entity whose rows belong to a single tenant.
/// The persistence layer uses this marker to filter queries and to stamp new rows.
/// </summary>
public interface ITenantEntity
{
    // No public setter on purpose: application code must never choose the tenant of a row.
    Guid TenantId { get; }
}
