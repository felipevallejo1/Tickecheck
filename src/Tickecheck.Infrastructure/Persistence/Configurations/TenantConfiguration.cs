using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickecheck.Domain.Tenants;

namespace Tickecheck.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        // Ids are created in the entity constructor. Without ValueGeneratedNever, EF Core
        // sees a new object that already has a key and assumes the row exists in the database.
        builder.Property(tenant => tenant.Id).ValueGeneratedNever();
        builder.Property(tenant => tenant.Name).HasMaxLength(200);
    }
}
