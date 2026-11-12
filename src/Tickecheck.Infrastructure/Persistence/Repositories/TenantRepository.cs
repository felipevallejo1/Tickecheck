using Microsoft.EntityFrameworkCore;
using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Tenants;

namespace Tickecheck.Infrastructure.Persistence.Repositories;

public class TenantRepository(AppDbContext db) : ITenantRepository
{
    public Task<List<Tenant>> ListAsync(CancellationToken cancellationToken = default)
    {
        return db.Tenants.AsNoTracking().OrderBy(tenant => tenant.Name).ToListAsync(cancellationToken);
    }

    public void Add(Tenant tenant)
    {
        db.Tenants.Add(tenant);
    }
}
