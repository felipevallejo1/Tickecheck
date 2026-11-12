namespace Tickecheck.Application.Abstractions;

/// <summary>
/// Tells which tenant the current operation belongs to.
/// </summary>
public interface ITenantProvider
{
    // Null when there is no tenant: anonymous requests and the SuperAdmin.
    Guid? TenantId { get; }
}
