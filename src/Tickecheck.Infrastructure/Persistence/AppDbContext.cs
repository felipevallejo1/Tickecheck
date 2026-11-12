using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Categories;
using Tickecheck.Domain.Common;
using Tickecheck.Domain.Tenants;
using Tickecheck.Domain.Tickets;
using Tickecheck.Domain.Users;

namespace Tickecheck.Infrastructure.Persistence;

// IUnitOfWork is implemented by the DbContext itself: it already tracks every change
// of the request and saves them in one transaction.
public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantProvider _tenantProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<TicketHistory> TicketHistory => Set<TicketHistory>();

    // The query filters below read this property. EF Core evaluates it again on every
    // query, so each request filters by the tenant of its own user.
    private Guid? CurrentTenantId => _tenantProvider.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Reflection is used so every ITenantEntity gets the filter automatically.
        // Listing the entities by hand would be simpler to read, but forgetting one
        // when adding a new entity would silently leak data between companies.
        var applyFilterMethod = typeof(AppDbContext)
            .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                applyFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }

        // User cannot implement ITenantEntity (its TenantId is nullable because of the
        // SuperAdmin), so it gets the same filter explicitly.
        modelBuilder.Entity<User>()
            .HasQueryFilter(user => CurrentTenantId != null && user.TenantId == CurrentTenantId);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        // "CurrentTenantId != null" makes the filter fail closed: with no tenant in
        // context the query returns nothing instead of returning everything.
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity => CurrentTenantId != null && entity.TenantId == CurrentTenantId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantOnSave();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceTenantOnSave();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Second line of defense, for writes. Query filters only protect reads: without
    /// this check, code could still attach or create a row for another tenant and save it.
    /// </summary>
    private void EnforceTenantOnSave()
    {
        var tenantId = CurrentTenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            if (tenantId is null)
            {
                throw new TenantViolationException(
                    $"Cannot save {entry.Entity.GetType().Name} without a tenant in context.");
            }

            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                // New entities never choose their tenant: it is stamped here.
                // TenantId has a private setter, so it is set through EF's property API.
                entry.Property(nameof(ITenantEntity.TenantId)).CurrentValue = tenantId.Value;
                continue;
            }

            // OriginalValue is checked too, so an existing row cannot be moved to another tenant.
            var originalTenantId = (Guid)entry.Property(nameof(ITenantEntity.TenantId)).OriginalValue!;
            if (entry.Entity.TenantId != tenantId || (entry.State != EntityState.Added && originalTenantId != tenantId))
            {
                throw new TenantViolationException(
                    $"Cannot save {entry.Entity.GetType().Name} that belongs to another tenant.");
            }
        }

        // Users have a nullable tenant, so the rule is: a request that has a tenant can
        // only write users of that tenant. Requests without tenant (SuperAdmin creating
        // the first admin of a company, the seeder) choose the tenant explicitly.
        foreach (var entry in ChangeTracker.Entries<User>())
        {
            var isWrite = entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
            if (isWrite && tenantId is not null && entry.Entity.TenantId != tenantId)
            {
                throw new TenantViolationException("Cannot save a user that belongs to another tenant.");
            }
        }
    }
}
