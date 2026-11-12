using Tickecheck.Domain.Common;

namespace Tickecheck.Domain.Categories;

public class Category : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;

    // Required by EF Core to materialize the entity.
    private Category()
    {
    }

    public Category(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Category name is required.");
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
    }
}
