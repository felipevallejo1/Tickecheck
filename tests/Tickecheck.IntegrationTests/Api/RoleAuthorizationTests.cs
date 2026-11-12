using System.Net;
using Tickecheck.Api.Controllers;
using Tickecheck.Application.Categories;
using Tickecheck.Application.Comments;
using Tickecheck.Application.Tenants;
using Tickecheck.Application.Tickets;
using Tickecheck.Application.Users;
using Tickecheck.Domain.Tickets;
using Tickecheck.Domain.Users;

namespace Tickecheck.IntegrationTests.Api;

[Collection(IntegrationCollection.Name)]
public class RoleAuthorizationTests(IntegrationFixture fixture)
{
    [Theory]
    // Requesters cannot use staff or admin endpoints.
    [InlineData(DemoUsers.AcmeUser1, "GET", "/api/dashboard")]
    [InlineData(DemoUsers.AcmeUser1, "GET", "/api/users")]
    [InlineData(DemoUsers.AcmeUser1, "GET", "/api/tenants")]
    // Agents are staff, but not admins.
    [InlineData(DemoUsers.AcmeAgent1, "GET", "/api/tenants")]
    // A TenantAdmin rules one company, not the platform.
    [InlineData(DemoUsers.AcmeAdmin, "GET", "/api/tenants")]
    // The SuperAdmin manages companies and has no access to their tickets.
    [InlineData(DemoUsers.SuperAdmin, "GET", "/api/tickets")]
    [InlineData(DemoUsers.SuperAdmin, "GET", "/api/dashboard")]
    [InlineData(DemoUsers.SuperAdmin, "GET", "/api/categories")]
    public async Task Endpoint_outside_the_role_returns_403(string email, string method, string url)
    {
        using var client = await fixture.CreateClientAsync(email);

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Requester_cannot_perform_staff_actions_on_their_own_ticket()
    {
        using var requester = await fixture.CreateClientAsync(DemoUsers.AcmeUser1);
        var ticket = await requester.CreateTicketAsync();
        var url = $"/api/tickets/{ticket.Id}";

        var responses = new[]
        {
            await requester.PutAsync($"{url}/status", new ChangeStatusRequest(TicketStatus.Closed)),
            await requester.PutAsync($"{url}/priority", new ChangePriorityRequest(TicketPriority.Critical)),
            await requester.PutAsync($"{url}/assignee", new AssignTicketRequest(Guid.NewGuid())),
            await requester.DeleteAsync(url)
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    [Fact]
    public async Task Only_a_tenant_admin_can_create_users_categories_and_delete_tickets()
    {
        using var agent = await fixture.CreateClientAsync(DemoUsers.AcmeAgent1);
        using var admin = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);
        var ticket = await agent.CreateTicketAsync();
        var newUser = new CreateUserCommand($"{Guid.NewGuid():N}@acme.demo", "New Person", "Password1!", UserRole.Agent);
        var newCategory = new CreateCategoryCommand($"Category {Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsync("/api/users", newUser)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsync("/api/categories", newCategory)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.DeleteAsync($"/api/tickets/{ticket.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync("/api/users", newUser)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync("/api/categories", newCategory)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/tickets/{ticket.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_tenant_admin_cannot_create_a_super_admin()
    {
        using var admin = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);
        var command = new CreateUserCommand($"{Guid.NewGuid():N}@acme.demo", "Sneaky", "Password1!", UserRole.SuperAdmin);

        var response = await admin.PostAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Super_admin_creates_a_company_whose_admin_can_log_in_and_sees_no_data_of_others()
    {
        using var superAdmin = await fixture.CreateClientAsync(DemoUsers.SuperAdmin);
        var adminEmail = $"{Guid.NewGuid():N}@newco.demo";
        var command = new CreateTenantCommand("NewCo", adminEmail, "NewCo Admin", "Demo123!");

        var response = await superAdmin.PostAsync("/api/tenants", command);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var tenants = await superAdmin.GetAsync<List<TenantDto>>("/api/tenants");
        Assert.Contains(tenants, tenant => tenant.Name == "NewCo");

        using var newAdmin = await fixture.CreateClientAsync(adminEmail);
        Assert.Empty(await newAdmin.GetAllTicketsAsync());
        Assert.Single(await newAdmin.GetAsync<List<UserDto>>("/api/users"));
    }

    [Fact]
    public async Task Requester_sees_only_their_own_tickets()
    {
        using var user1 = await fixture.CreateClientAsync(DemoUsers.AcmeUser1);
        using var user2 = await fixture.CreateClientAsync(DemoUsers.AcmeUser2);
        using var agent = await fixture.CreateClientAsync(DemoUsers.AcmeAgent1);
        var user1Id = await agent.GetUserIdAsync(DemoUsers.AcmeUser1);
        var ticketOfUser2 = await user2.CreateTicketAsync();

        var listOfUser1 = await user1.GetAllTicketsAsync();
        var response = await user1.GetAsync($"/api/tickets/{ticketOfUser2.Id}");

        Assert.NotEmpty(listOfUser1);
        Assert.All(listOfUser1, ticket => Assert.Equal(user1Id, ticket.RequesterId));
        // Same company, but not their ticket: it "does not exist" for them.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        // Staff of the company does see it.
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync($"/api/tickets/{ticketOfUser2.Id}")).StatusCode);
    }

    [Fact]
    public async Task Requester_never_sees_internal_comments_and_cannot_write_them()
    {
        using var requester = await fixture.CreateClientAsync(DemoUsers.AcmeUser1);
        using var agent = await fixture.CreateClientAsync(DemoUsers.AcmeAgent1);
        var ticket = await requester.CreateTicketAsync();
        var url = $"/api/tickets/{ticket.Id}/comments";

        await agent.PostAsync(url, new AddCommentRequest("Public answer", false));
        await agent.PostAsync(url, new AddCommentRequest("Internal note", true));
        var forbidden = await requester.PostAsync(url, new AddCommentRequest("Trying an internal note", true));

        var seenByRequester = await requester.GetAsync<List<CommentDto>>(url);
        var seenByAgent = await agent.GetAsync<List<CommentDto>>(url);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Single(seenByRequester);
        Assert.DoesNotContain(seenByRequester, comment => comment.IsInternal);
        Assert.Equal(2, seenByAgent.Count);
    }

    [Fact]
    public async Task Requester_can_reopen_their_own_resolved_ticket()
    {
        using var requester = await fixture.CreateClientAsync(DemoUsers.AcmeUser1);
        using var agent = await fixture.CreateClientAsync(DemoUsers.AcmeAgent1);
        var ticket = await requester.CreateTicketAsync();
        var url = $"/api/tickets/{ticket.Id}";
        var agentId = await agent.GetUserIdAsync(DemoUsers.AcmeAgent1);
        await agent.PutAsync($"{url}/assignee", new AssignTicketRequest(agentId));
        await agent.PutAsync($"{url}/status", new ChangeStatusRequest(TicketStatus.InProgress));
        await agent.PutAsync($"{url}/status", new ChangeStatusRequest(TicketStatus.Resolved));

        var response = await requester.PostAsync($"{url}/reopen", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TicketStatus.Reopened, (await response.ReadAsync<TicketDto>()).Status);
    }
}
