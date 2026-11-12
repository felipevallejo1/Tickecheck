using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickecheck.Domain.Categories;
using Tickecheck.Domain.Tenants;
using Tickecheck.Domain.Tickets;
using Tickecheck.Domain.Users;

namespace Tickecheck.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.Property(ticket => ticket.Id).ValueGeneratedNever();
        builder.Property(ticket => ticket.Title).HasMaxLength(200);

        // Every list query filters by tenant and usually by status.
        builder.HasIndex(ticket => new { ticket.TenantId, ticket.Status });

        // Restrict everywhere: deleting a company, user or category must never
        // silently delete tickets. SQL Server also rejects several cascade paths to one table.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(ticket => ticket.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(ticket => ticket.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ticket => ticket.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ticket => ticket.AssignedAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // EF Core fills the private "_history" list behind the read-only History property.
        builder.HasMany(ticket => ticket.History)
            .WithOne()
            .HasForeignKey(history => history.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
