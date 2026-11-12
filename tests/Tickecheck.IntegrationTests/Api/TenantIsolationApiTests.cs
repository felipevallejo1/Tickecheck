using System.Net;
using Tickecheck.Api.Controllers;
using Tickecheck.Application.Categories;
using Tickecheck.Application.Dashboard;
using Tickecheck.Application.Tickets;
using Tickecheck.Application.Users;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.IntegrationTests.Api;

/// <summary>
/// The central promise of the project, tested from the outside through HTTP:
/// a user of Acme cannot read, list or change anything of Globex, even with the exact ids.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class TenantIsolationApiTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task Listing_tickets_returns_only_tickets_of_the_own_tenant()
    {
        using var acme = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);
        using var globex = await fixture.CreateClientAsync(DemoUsers.GlobexAdmin);

        var acmeIds = (await acme.GetAllTicketsAsync()).Select(ticket => ticket.Id).ToList();
        var globexIds = (await globex.GetAllTicketsAsync()).Select(ticket => ticket.Id).ToList();

        Assert.NotEmpty(acmeIds);
        Assert.NotEmpty(globexIds);
        Assert.Empty(acmeIds.Intersect(globexIds));
    }

    [Fact]
    public async Task Reading_a_ticket_of_another_tenant_by_id_returns_404()
    {
        using var acme = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);
        var globexTicket = await CreateGlobexTicketAsync();

        var ticketResponse = await acme.GetAsync($"/api/tickets/{globexTicket.Id}");
        var commentsResponse = await acme.GetAsync($"/api/tickets/{globexTicket.Id}/comments");
        var historyResponse = await acme.GetAsync($"/api/tickets/{globexTicket.Id}/history");

        // 404 and not 403: Acme must not even learn that the id exists.
        Assert.Equal(HttpStatusCode.NotFound, ticketResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, commentsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, historyResponse.StatusCode);
    }

    [Fact]
    public async Task Modifying_a_ticket_of_another_tenant_returns_404_and_changes_nothing()
    {
        using var acme = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);
        using var globex = await fixture.CreateClientAsync(DemoUsers.GlobexAdmin);
        var globexTicket = await CreateGlobexTicketAsync();
        var acmeAgentId = await acme.GetUserIdAsync(DemoUsers.AcmeAgent1);
        var url = $"/api/tickets/{globexTicket.Id}";

        var responses = new[]
        {
            await acme.PutAsync(url, new UpdateTicketRequest("Hacked", "Hacked", globexTicket.CategoryId)),
            await acme.PutAsync($"{url}/assignee", new AssignTicketRequest(acmeAgentId)),
            await acme.PutAsync($"{url}/status", new ChangeStatusRequest(TicketStatus.Closed)),
            await acme.PutAsync($"{url}/priority", new ChangePriorityRequest(TicketPriority.Critical)),
            await acme.PostAsync($"{url}/comments", new AddCommentRequest("Hello from Acme", false)),
            await acme.PostAsync($"{url}/reopen", new { }),
            await acme.DeleteAsync(url)
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));

        // Seen from Globex, the ticket is exactly as it was created.
        var unchanged = await globex.GetAsync<TicketDto>(url);
        Assert.Equal(globexTicket.Title, unchanged.Title);
        Assert.Equal(TicketStatus.Open, unchanged.Status);
        Assert.Equal(TicketPriority.Medium, unchanged.Priority);
        Assert.Null(unchanged.AssignedAgentId);
        Assert.Empty(await globex.GetAsync<List<object>>($"{url}/comments"));
    }

    [Fact]
    public async Task A_ticket_cannot_be_assigned_to_an_agent_of_another_tenant()
    {
        using var acme = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);
        using var globex = await fixture.CreateClientAsync(DemoUsers.GlobexAdmin);
        var acmeTicket = await acme.CreateTicketAsync();
        var globexAgentId = await globex.GetUserIdAsync(DemoUsers.GlobexAgent1);

        var response = await acme.PutAsync($"/api/tickets/{acmeTicket.Id}/assignee", new AssignTicketRequest(globexAgentId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_ticket_cannot_be_created_with_a_category_of_another_tenant()
    {
        using var acme = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);
        using var globex = await fixture.CreateClientAsync(DemoUsers.GlobexAdmin);
        var globexCategory = (await globex.GetAsync<List<CategoryDto>>("/api/categories"))[0];

        var response = await acme.PostAsync(
            "/api/tickets",
            new CreateTicketCommand("Forged", "Uses a foreign category.", TicketPriority.Low, globexCategory.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Users_and_categories_lists_contain_only_the_own_tenant()
    {
        using var acme = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);
        using var globex = await fixture.CreateClientAsync(DemoUsers.GlobexAdmin);

        var acmeUsers = await acme.GetAsync<List<UserDto>>("/api/users");
        var acmeCategoryIds = (await acme.GetAsync<List<CategoryDto>>("/api/categories")).Select(c => c.Id);
        var globexCategoryIds = (await globex.GetAsync<List<CategoryDto>>("/api/categories")).Select(c => c.Id);

        Assert.All(acmeUsers, user => Assert.EndsWith("@acme.demo", user.Email));
        Assert.Empty(acmeCategoryIds.Intersect(globexCategoryIds));
    }

    [Fact]
    public async Task Dashboard_counts_only_tickets_of_the_own_tenant()
    {
        using var acme = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);

        var visibleTickets = await acme.GetAllTicketsAsync();
        var stats = await acme.GetAsync<DashboardStatsDto>("/api/dashboard");

        // Other tests may add Acme tickets in between, so the total can only be equal or higher.
        Assert.True(stats.TotalTickets >= visibleTickets.Count);
        Assert.Equal(stats.TotalTickets, stats.TicketsByStatus.Values.Sum());
        Assert.Equal(stats.TotalTickets, stats.TicketsByPriority.Values.Sum());
    }

    private async Task<TicketDto> CreateGlobexTicketAsync()
    {
        using var globex = await fixture.CreateClientAsync(DemoUsers.GlobexAdmin);
        return await globex.CreateTicketAsync("Globex confidential ticket");
    }
}
