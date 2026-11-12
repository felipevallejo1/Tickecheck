using System.Net.Http.Json;
using Tickecheck.Web.Models;

namespace Tickecheck.Web.Services;

/// <summary>
/// Login, logout and token refresh.
/// </summary>
public class AuthService(HttpClient http, TokenStore tokenStore, JwtAuthStateProvider authStateProvider)
{
    // Only one refresh may run at a time. A page often fires several requests at once;
    // if each of them refreshed with the same token, the second would look like token
    // reuse to the API and the whole session would be revoked.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task<bool> LoginAsync(string email, string password)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        await tokenStore.SetAsync(tokens!);
        authStateProvider.NotifyChanged();
        return true;
    }

    public async Task LogoutAsync()
    {
        var tokens = await tokenStore.GetAsync();
        if (tokens is not null)
        {
            // Best effort: the local session ends even if the server cannot be reached.
            try
            {
                await http.PostAsJsonAsync("api/auth/logout", new RefreshRequest(tokens.RefreshToken));
            }
            catch (HttpRequestException)
            {
            }
        }

        await tokenStore.ClearAsync();
        authStateProvider.NotifyChanged();
    }

    /// <summary>
    /// Returns an access token that is still valid, refreshing it first if it is about to expire.
    /// Null means there is no session.
    /// </summary>
    public async Task<string?> GetValidAccessTokenAsync()
    {
        var tokens = await tokenStore.GetAsync();
        if (tokens is null)
        {
            return null;
        }

        var isAboutToExpire = tokens.AccessTokenExpiresAt <= DateTime.UtcNow.AddSeconds(30);
        if (isAboutToExpire && !await TryRefreshAsync(tokens.AccessToken))
        {
            return null;
        }

        return (await tokenStore.GetAsync())?.AccessToken;
    }

    /// <param name="staleAccessToken">The access token that was found expired or rejected.</param>
    public async Task<bool> TryRefreshAsync(string staleAccessToken)
    {
        await _refreshLock.WaitAsync();
        try
        {
            var tokens = await tokenStore.GetAsync();
            if (tokens is null)
            {
                return false;
            }

            // Another request refreshed while this one was waiting for the lock.
            if (tokens.AccessToken != staleAccessToken)
            {
                return true;
            }

            var response = await http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
            if (!response.IsSuccessStatusCode)
            {
                await tokenStore.ClearAsync();
                authStateProvider.NotifyChanged();
                return false;
            }

            var newTokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
            await tokenStore.SetAsync(newTokens!);
            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
