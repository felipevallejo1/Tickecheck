using Tickecheck.Domain.Tenants;

namespace Tickecheck.Application.Abstractions;

public interface ITenantRepository
{
    Task<List<Tenant>> ListAsync(CancellationToken cancellationToken = default);

    void Add(Tenant tenant);
}
