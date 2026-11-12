using Tickecheck.Domain.Common;

namespace Tickecheck.Domain.Tenants;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    // Required by EF Core to materialize the entity.
    private Tenant()
    {
    }

    public Tenant(string name, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Tenant name is required.");
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        CreatedAt = utcNow;
    }
}
