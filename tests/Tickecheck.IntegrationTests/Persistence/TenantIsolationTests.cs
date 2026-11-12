using Microsoft.EntityFrameworkCore;
using Tickecheck.Domain.Categories;
using Tickecheck.Domain.Tenants;
using Tickecheck.Domain.Tickets;
using Tickecheck.Domain.Users;
using Tickecheck.Infrastructure.Persistence;

namespace Tickecheck.IntegrationTests.Persistence;

/// <summary>
/// Proves the isolation at the lowest level: the DbContext itself, with no API on top.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class TenantIsolationTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    private readonly IntegrationFixture _fixture;

    public TenantIsolationTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task New_entity_is_stamped_with_the_current_tenant()
    {
        var tenantA = await CreateTenantAsync();

        var ticketId = await CreateTicketAsync(tenantA);

        await using var db = _fixture.CreateDbContext(tenantA);
        var ticket = await db.Tickets.SingleAsync(t => t.Id == ticketId);
        Assert.Equal(tenantA, ticket.TenantId);
    }

    [Fact]
    public async Task Child_entities_created_by_the_domain_are_stamped_too()
    {
        var tenantA = await CreateTenantAsync();
        var ticketId = await CreateTicketAsync(tenantA);

        await using (var db = _fixture.CreateDbContext(tenantA))
        {
            var ticket = await db.Tickets.SingleAsync(t => t.Id == ticketId);
            ticket.ChangePriority(TicketPriority.Critical, ticket.RequesterId, Now);
            await db.SaveChangesAsync();
        }

        await using var readDb = _fixture.CreateDbContext(tenantA);
        var history = await readDb.TicketHistory.SingleAsync(h => h.TicketId == ticketId);
        Assert.Equal(tenantA, history.TenantId);
    }

    [Fact]
    public async Task Tenant_cannot_list_data_of_another_tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        await CreateTicketAsync(tenantA);

        await using var db = _fixture.CreateDbContext(tenantB);

        Assert.Empty(await db.Tickets.ToListAsync());
        Assert.Empty(await db.Categories.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task Tenant_cannot_read_a_row_of_another_tenant_even_knowing_its_id()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var ticketId = await CreateTicketAsync(tenantA);

        await using var db = _fixture.CreateDbContext(tenantB);

        Assert.Null(await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId));
        Assert.Null(await db.Tickets.FindAsync(ticketId));
    }

    [Fact]
    public async Task Tenant_cannot_modify_a_row_of_another_tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var ticketId = await CreateTicketAsync(tenantA);

        await using var db = _fixture.CreateDbContext(tenantB);
        // IgnoreQueryFilters simulates buggy code that got hold of a foreign row anyway.
        var foreignTicket = await db.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == ticketId);
        foreignTicket.UpdateDetails("Hacked", "Hacked", foreignTicket.CategoryId);

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Tenant_cannot_delete_a_row_of_another_tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var ticketId = await CreateTicketAsync(tenantA);

        await using var db = _fixture.CreateDbContext(tenantB);
        var foreignTicket = await db.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == ticketId);
        db.Tickets.Remove(foreignTicket);

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Tenant_cannot_insert_a_row_for_another_tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();

        await using var db = _fixture.CreateDbContext(tenantB);
        var category = new Category("Forged");
        db.Categories.Add(category);
        db.Entry(category).Property(c => c.TenantId).CurrentValue = tenantA;

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Tenant_cannot_write_a_user_of_another_tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();

        await using var db = _fixture.CreateDbContext(tenantB);
        db.Users.Add(NewUser(tenantA));

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Without_tenant_nothing_is_visible_and_tenant_data_cannot_be_saved()
    {
        var tenantA = await CreateTenantAsync();
        await CreateTicketAsync(tenantA);

        await using var db = _fixture.CreateDbContext(tenantId: null);

        Assert.Empty(await db.Tickets.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());

        db.Categories.Add(new Category("Orphan"));
        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    private async Task<Guid> CreateTenantAsync()
    {
        await using var db = _fixture.CreateDbContext(tenantId: null);
        var tenant = new Tenant($"Tenant {Guid.NewGuid()}", Now);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private async Task<Guid> CreateTicketAsync(Guid tenantId)
    {
        await using var db = _fixture.CreateDbContext(tenantId);

        var category = new Category("Hardware");
        var requester = NewUser(tenantId);
        var ticket = new Ticket("Broken screen", "It does not turn on.", TicketPriority.Medium, category.Id, requester.Id, Now);

        db.Categories.Add(category);
        db.Users.Add(requester);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        return ticket.Id;
    }

    private static User NewUser(Guid tenantId)
    {
        return new User(tenantId, $"{Guid.NewGuid()}@test.dev", "hash", "Test User", UserRole.Requester, Now);
    }
}
