using System.Net.Http.Json;
using Tickecheck.Application.Categories;
using Tickecheck.Application.Common;
using Tickecheck.Application.Tickets;
using Tickecheck.Application.Users;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.IntegrationTests.Api;

/// <summary>
/// Small shortcuts over HttpClient so the tests read as a list of steps.
/// </summary>
internal static class ApiTestHelpers
{
    public static async Task<T> GetAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(IntegrationFixture.Json))!;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<T>(IntegrationFixture.Json))!;
    }

    public static Task<HttpResponseMessage> PostAsync<T>(this HttpClient client, string url, T body)
    {
        return client.PostAsJsonAsync(url, body, IntegrationFixture.Json);
    }

    public static Task<HttpResponseMessage> PutAsync<T>(this HttpClient client, string url, T body)
    {
        return client.PutAsJsonAsync(url, body, IntegrationFixture.Json);
    }

    public static async Task<List<TicketDto>> GetAllTicketsAsync(this HttpClient client)
    {
        var page = await client.GetAsync<PagedResult<TicketDto>>("/api/tickets?pageSize=100");
        return page.Items;
    }

    /// <summary>Creates a new ticket with the given client, so a test can change it freely.</summary>
    public static async Task<TicketDto> CreateTicketAsync(this HttpClient client, string title = "Test ticket")
    {
        var categories = await client.GetAsync<List<CategoryDto>>("/api/categories");
        var command = new CreateTicketCommand(title, "Created by an integration test.", TicketPriority.Medium, categories[0].Id);

        var response = await client.PostAsync("/api/tickets", command);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<TicketDto>();
    }

    public static async Task<Guid> GetUserIdAsync(this HttpClient staffClient, string email)
    {
        var users = await staffClient.GetAsync<List<UserDto>>("/api/users");
        return users.Single(user => user.Email == email).Id;
    }
}
