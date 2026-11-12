using System.Net;
using Tickecheck.Api.Controllers;
using Tickecheck.Application.Common;
using Tickecheck.Application.Tickets;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.IntegrationTests.Api;

[Collection(IntegrationCollection.Name)]
public class TicketFlowTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task Ticket_goes_through_its_life_cycle_and_every_change_is_audited()
    {
        using var requester = await fixture.CreateClientAsync(DemoUsers.AcmeUser1);
        using var agent = await fixture.CreateClientAsync(DemoUsers.AcmeAgent1);
        var agentId = await agent.GetUserIdAsync(DemoUsers.AcmeAgent1);

        var ticket = await requester.CreateTicketAsync("Life cycle ticket");
        var url = $"/api/tickets/{ticket.Id}";
        Assert.Equal(TicketStatus.Open, ticket.Status);
        // Medium priority = 24 hours of SLA.
        Assert.Equal(ticket.CreatedAt.AddHours(24), ticket.DueAt);
        Assert.False(ticket.IsOverdue);

        await agent.PutAsync($"{url}/assignee", new AssignTicketRequest(agentId));
        await agent.PutAsync($"{url}/priority", new ChangePriorityRequest(TicketPriority.High));
        await agent.PutAsync($"{url}/status", new ChangeStatusRequest(TicketStatus.InProgress));
        var resolved = await (await agent.PutAsync($"{url}/status", new ChangeStatusRequest(TicketStatus.Resolved)))
            .ReadAsync<TicketDto>();

        Assert.Equal(TicketStatus.Resolved, resolved.Status);
        Assert.Equal(TicketPriority.High, resolved.Priority);
        Assert.Equal(ticket.CreatedAt.AddHours(8), resolved.DueAt);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.Equal(agentId, resolved.AssignedAgentId);

        var history = await requester.GetAsync<List<TicketHistoryDto>>($"{url}/history");
        Assert.Equal(
            [TicketChangeType.AssigneeChanged, TicketChangeType.PriorityChanged, TicketChangeType.StatusChanged, TicketChangeType.StatusChanged],
            history.Select(entry => entry.ChangeType));
        // The assignee is shown by name, not by id.
        Assert.Equal(resolved.AssignedAgentName, history[0].NewValue);
    }

    [Fact]
    public async Task Invalid_transition_returns_409_as_problem_details()
    {
        using var agent = await fixture.CreateClientAsync(DemoUsers.AcmeAgent1);
        var ticket = await agent.CreateTicketAsync();

        // Open -> Resolved skips "InProgress", which the domain does not allow.
        var response = await agent.PutAsync($"/api/tickets/{ticket.Id}/status", new ChangeStatusRequest(TicketStatus.Resolved));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Starting_work_without_an_assigned_agent_returns_409()
    {
        using var agent = await fixture.CreateClientAsync(DemoUsers.AcmeAgent1);
        var ticket = await agent.CreateTicketAsync();

        var response = await agent.PutAsync($"/api/tickets/{ticket.Id}/status", new ChangeStatusRequest(TicketStatus.InProgress));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_changes_title_description_and_category()
    {
        using var requester = await fixture.CreateClientAsync(DemoUsers.AcmeUser1);
        var ticket = await requester.CreateTicketAsync();

        var response = await requester.PutAsync(
            $"/api/tickets/{ticket.Id}",
            new UpdateTicketRequest("Updated title", "Updated description", ticket.CategoryId));
        var updated = await response.ReadAsync<TicketDto>();

        Assert.Equal("Updated title", updated.Title);
        Assert.Equal("Updated description", updated.Description);
    }

    [Fact]
    public async Task Search_pages_the_results()
    {
        using var admin = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);

        var firstPage = await admin.GetAsync<PagedResult<TicketDto>>("/api/tickets?page=1&pageSize=3");
        var secondPage = await admin.GetAsync<PagedResult<TicketDto>>("/api/tickets?page=2&pageSize=3");

        Assert.Equal(3, firstPage.Items.Count);
        Assert.Equal(3, secondPage.Items.Count);
        Assert.True(firstPage.TotalCount >= 8);
        Assert.Empty(firstPage.Items.Select(t => t.Id).Intersect(secondPage.Items.Select(t => t.Id)));
    }

    [Fact]
    public async Task Search_filters_by_status_priority_agent_and_text()
    {
        using var admin = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);
        var agentId = await admin.GetUserIdAsync(DemoUsers.AcmeAgent1);

        var waiting = await admin.GetAsync<PagedResult<TicketDto>>("/api/tickets?status=WaitingOnCustomer&pageSize=100");
        var critical = await admin.GetAsync<PagedResult<TicketDto>>("/api/tickets?priority=Critical&pageSize=100");
        var ofAgent = await admin.GetAsync<PagedResult<TicketDto>>($"/api/tickets?assignedAgentId={agentId}&pageSize=100");
        var byText = await admin.GetAsync<PagedResult<TicketDto>>("/api/tickets?text=VPN&pageSize=100");

        Assert.NotEmpty(waiting.Items);
        Assert.All(waiting.Items, ticket => Assert.Equal(TicketStatus.WaitingOnCustomer, ticket.Status));
        Assert.NotEmpty(critical.Items);
        Assert.All(critical.Items, ticket => Assert.Equal(TicketPriority.Critical, ticket.Priority));
        Assert.NotEmpty(ofAgent.Items);
        Assert.All(ofAgent.Items, ticket => Assert.Equal(agentId, ticket.AssignedAgentId));
        Assert.NotEmpty(byText.Items);
        Assert.All(byText.Items, ticket => Assert.Contains("VPN", ticket.Title + ticket.Description));
    }

    [Fact]
    public async Task Search_sorts_by_the_requested_field()
    {
        using var admin = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);

        var byPriority = await admin.GetAsync<PagedResult<TicketDto>>("/api/tickets?sortBy=priority&sortDescending=true&pageSize=100");
        var byTitle = await admin.GetAsync<PagedResult<TicketDto>>("/api/tickets?sortBy=title&sortDescending=false&pageSize=100");

        var priorities = byPriority.Items.Select(ticket => ticket.Priority).ToList();
        var titles = byTitle.Items.Select(ticket => ticket.Title).ToList();
        Assert.Equal(priorities.OrderByDescending(priority => priority), priorities);
        Assert.Equal(titles.OrderBy(title => title, StringComparer.OrdinalIgnoreCase), titles);
    }

    [Theory]
    [InlineData("/api/tickets?pageSize=1000")]
    [InlineData("/api/tickets?page=0")]
    [InlineData("/api/tickets?sortBy=passwordHash")]
    public async Task Search_with_invalid_parameters_returns_400(string url)
    {
        using var admin = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);

        var response = await admin.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Seeded_overdue_tickets_are_flagged()
    {
        using var admin = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);

        var tickets = await admin.GetAllTicketsAsync();

        var vpn = tickets.Single(ticket => ticket.Title == "Cannot connect to the VPN");
        var closed = tickets.Single(ticket => ticket.Title == "Printer on the 2nd floor has no toner");
        Assert.True(vpn.IsOverdue);
        // A finished ticket is never overdue, no matter how old it is.
        Assert.False(closed.IsOverdue);
    }
}
