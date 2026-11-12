using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Tickecheck.Web.Models;

namespace Tickecheck.Web.Services;

/// <summary>
/// Keeps one SignalR connection open while the user is signed in and republishes the
/// server events as C# events that pages can subscribe to.
/// </summary>
public class NotificationService(NavigationManager navigation, AuthService auth) : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<TicketAssignedNotification>? TicketAssigned;
    public event Action<CommentAddedNotification>? CommentAdded;

    public async Task StartAsync()
    {
        if (_connection is not null)
        {
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(navigation.ToAbsoluteUri("/hubs/notifications"), options =>
            {
                // Called on every (re)connection, so it always sends a fresh token.
                options.AccessTokenProvider = async () => await auth.GetValidAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<TicketAssignedNotification>("TicketAssigned", notification => TicketAssigned?.Invoke(notification));
        _connection.On<CommentAddedNotification>("CommentAdded", notification => CommentAdded?.Invoke(notification));

        try
        {
            await _connection.StartAsync();
        }
        catch (Exception)
        {
            // Real-time updates are a nice-to-have: the app keeps working without them.
            await StopAsync();
        }
    }

    public async Task StopAsync()
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.DisposeAsync();
        _connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
