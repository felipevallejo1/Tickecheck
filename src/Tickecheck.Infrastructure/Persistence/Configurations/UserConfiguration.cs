using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickecheck.Domain.Tenants;
using Tickecheck.Domain.Users;

namespace Tickecheck.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.FullName).HasMaxLength(200);

        // Unique across the whole platform, so login needs only email and password.
        builder.HasIndex(user => user.Email).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(user => user.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
