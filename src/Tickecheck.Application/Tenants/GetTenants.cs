using Tickecheck.Application.Abstractions;

namespace Tickecheck.Application.Tenants;

public class GetTenantsHandler(ITenantRepository tenants)
{
    public async Task<List<TenantDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var list = await tenants.ListAsync(cancellationToken);
        return list.Select(tenant => new TenantDto(tenant.Id, tenant.Name, tenant.CreatedAt)).ToList();
    }
}
