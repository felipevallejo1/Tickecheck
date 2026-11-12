using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Tickecheck.Api.Controllers;
using Tickecheck.Api.Hubs;

namespace Tickecheck.IntegrationTests.Api;

/// <summary>
/// Real SignalR connections against the in-memory API, to prove that events
/// do not cross companies and that internal comments do not reach requesters.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class NotificationTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task Internal_comment_reaches_staff_of_the_company_only()
    {
        using var requester = await fixture.CreateClientAsync(DemoUsers.AcmeUser1);
        using var agent = await fixture.CreateClientAsync(DemoUsers.AcmeAgent1);
        var ticket = await requester.CreateTicketAsync();

        await using var acmeStaff = await Listener.ConnectAsync(fixture, DemoUsers.AcmeAgent2);
        await using var acmeRequester = await Listener.ConnectAsync(fixture, DemoUsers.AcmeUser1);
        await using var globexStaff = await Listener.ConnectAsync(fixture, DemoUsers.GlobexAgent1);

        await agent.PostAsync($"/api/tickets/{ticket.Id}/comments", new AddCommentRequest("Internal note", true));

        var received = await acmeStaff.WaitForCommentAsync();
        Assert.Equal(ticket.Id, received.TicketId);
        Assert.True(received.IsInternal);

        // The staff member already got the event, so anyone else who was going to
        // receive it would have by now. A short extra wait keeps the check honest.
        await Task.Delay(500);
        Assert.Empty(acmeRequester.Comments);
        Assert.Empty(globexStaff.Comments);
    }

    [Fact]
    public async Task Public_comment_reaches_the_requester_but_never_another_company()
    {
        using var requester = await fixture.CreateClientAsync(DemoUsers.AcmeUser1);
        using var agent = await fixture.CreateClientAsync(DemoUsers.AcmeAgent1);
        var ticket = await requester.CreateTicketAsync();

        await using var acmeRequester = await Listener.ConnectAsync(fixture, DemoUsers.AcmeUser1);
        await using var otherAcmeRequester = await Listener.ConnectAsync(fixture, DemoUsers.AcmeUser2);
        await using var globexStaff = await Listener.ConnectAsync(fixture, DemoUsers.GlobexAgent1);
        await using var globexRequester = await Listener.ConnectAsync(fixture, DemoUsers.GlobexUser1);

        await agent.PostAsync($"/api/tickets/{ticket.Id}/comments", new AddCommentRequest("Public answer", false));

        var received = await acmeRequester.WaitForCommentAsync();
        Assert.Equal(ticket.Id, received.TicketId);

        await Task.Delay(500);
        Assert.Empty(otherAcmeRequester.Comments);
        Assert.Empty(globexStaff.Comments);
        Assert.Empty(globexRequester.Comments);
    }

    [Fact]
    public async Task Assignment_notifies_only_the_assigned_agent()
    {
        using var admin = await fixture.CreateClientAsync(DemoUsers.AcmeAdmin);
        var ticket = await admin.CreateTicketAsync();
        var agent1Id = await admin.GetUserIdAsync(DemoUsers.AcmeAgent1);

        await using var agent1 = await Listener.ConnectAsync(fixture, DemoUsers.AcmeAgent1);
        await using var agent2 = await Listener.ConnectAsync(fixture, DemoUsers.AcmeAgent2);

        await admin.PutAsync($"/api/tickets/{ticket.Id}/assignee", new AssignTicketRequest(agent1Id));

        var received = await agent1.WaitForAssignmentAsync();
        Assert.Equal(ticket.Id, received.TicketId);

        await Task.Delay(500);
        Assert.Empty(agent2.Assignments);
    }

    [Fact]
    public async Task Hub_rejects_connections_without_a_token()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, NotificationsHub.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => connection.StartAsync());
    }

    /// <summary>
    /// A connected SignalR client that records every notification it receives.
    /// </summary>
    private sealed class Listener : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly TaskCompletionSource<CommentAddedNotification> _firstComment = new();
        private readonly TaskCompletionSource<TicketAssignedNotification> _firstAssignment = new();

        public List<CommentAddedNotification> Comments { get; } = [];
        public List<TicketAssignedNotification> Assignments { get; } = [];

        private Listener(HubConnection connection)
        {
            _connection = connection;

            connection.On<CommentAddedNotification>(SignalRTicketNotifier.CommentAddedEvent, notification =>
            {
                Comments.Add(notification);
                _firstComment.TrySetResult(notification);
            });

            connection.On<TicketAssignedNotification>(SignalRTicketNotifier.TicketAssignedEvent, notification =>
            {
                Assignments.Add(notification);
                _firstAssignment.TrySetResult(notification);
            });
        }

        public static async Task<Listener> ConnectAsync(IntegrationFixture fixture, string email)
        {
            var tokens = await fixture.LoginAsync(email);

            var connection = new HubConnectionBuilder()
                .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, NotificationsHub.Path), options =>
                {
                    // Routes the SignalR traffic to the in-memory API instead of the network.
                    // The test server has no WebSockets, so long polling is used.
                    options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                    options.AccessTokenProvider = () => Task.FromResult<string?>(tokens.AccessToken);
                })
                .Build();

            var listener = new Listener(connection);
            await connection.StartAsync();
            return listener;
        }

        public Task<CommentAddedNotification> WaitForCommentAsync()
        {
            return _firstComment.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public Task<TicketAssignedNotification> WaitForAssignmentAsync()
        {
            return _firstAssignment.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
