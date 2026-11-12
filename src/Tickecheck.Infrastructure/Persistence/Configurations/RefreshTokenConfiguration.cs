using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickecheck.Domain.Users;

namespace Tickecheck.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash).HasMaxLength(64);
        builder.Property(token => token.ReplacedByTokenHash).HasMaxLength(64);

        // Tokens are always looked up by hash.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
