using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;

namespace Tickecheck.Web.Services;

/// <summary>An error returned by the API, with a message that can be shown to the user.</summary>
public class ApiException(string message) : Exception(message);

/// <summary>
/// The one place that talks to the API. It adds the access token to every request,
/// refreshes it when needed and turns error responses into ApiException.
/// </summary>
public class ApiClient(HttpClient http, AuthService auth, NavigationManager navigation)
{
    // Same JSON settings as the API: camelCase names and enums as text.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<T> GetAsync<T>(string url)
    {
        var response = await SendAsync(HttpMethod.Get, url, body: null);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public async Task<T> PostAsync<T>(string url, object body)
    {
        var response = await SendAsync(HttpMethod.Post, url, body);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public async Task<T> PutAsync<T>(string url, object body)
    {
        var response = await SendAsync(HttpMethod.Put, url, body);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public Task DeleteAsync(string url)
    {
        return SendAsync(HttpMethod.Delete, url, body: null);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body)
    {
        var accessToken = await auth.GetValidAccessTokenAsync();
        var response = await SendOnceAsync(method, url, body, accessToken);

        // The token can still be rejected (for example, the clocks differ). Refresh once and retry.
        if (response.StatusCode == HttpStatusCode.Unauthorized
            && accessToken is not null
            && await auth.TryRefreshAsync(accessToken))
        {
            accessToken = await auth.GetValidAccessTokenAsync();
            response = await SendOnceAsync(method, url, body, accessToken);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await auth.LogoutAsync();
            navigation.NavigateTo("/login");
            throw new ApiException("Your session has expired. Please sign in again.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(await ReadErrorAsync(response));
        }

        return response;
    }

    private Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string url, object? body, string? accessToken)
    {
        // A request message can be sent only once, so the retry builds a new one.
        var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return http.SendAsync(request);
    }

    /// <summary>
    /// Every API error is a ProblemDetails JSON: "detail" has the message and, for
    /// validation errors, "errors" has the messages per field.
    /// </summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (problem.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(field => field.Value.EnumerateArray())
                    .Select(message => message.GetString());
                return string.Join(" ", messages);
            }

            if (problem.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // Not JSON: fall through to the generic message.
        }

        return response.StatusCode == HttpStatusCode.Forbidden
            ? "You are not allowed to do that."
            : $"The request failed ({(int)response.StatusCode}).";
    }
}
