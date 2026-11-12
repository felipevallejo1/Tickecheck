using Tickecheck.Application.Abstractions;

namespace Tickecheck.Infrastructure.Persistence;

/// <summary>
/// Tenant provider with a fixed value, for code that runs outside an HTTP request
/// (the data seeder and the tests).
/// </summary>
public class FixedTenantProvider : ITenantProvider
{
    public FixedTenantProvider(Guid? tenantId)
    {
        TenantId = tenantId;
    }

    public Guid? TenantId { get; }
}
