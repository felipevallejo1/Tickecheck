using System.Text.Json;
using Microsoft.JSInterop;
using Tickecheck.Web.Models;

namespace Tickecheck.Web.Services;

/// <summary>
/// Keeps the tokens in the browser's localStorage so the session survives a page reload.
/// Trade-off: any script running on the page could read them (XSS). The safer option is
/// an HttpOnly cookie for the refresh token; see "Limitaciones" in the README.
/// </summary>
public class TokenStore(IJSRuntime js)
{
    private const string StorageKey = "tickecheck.tokens";

    // Cached copy so the tokens are not read from JavaScript on every request.
    private AuthResponse? _cached;
    private bool _loaded;

    public async Task<AuthResponse?> GetAsync()
    {
        if (!_loaded)
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            _cached = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<AuthResponse>(json);
            _loaded = true;
        }

        return _cached;
    }

    public async Task SetAsync(AuthResponse tokens)
    {
        _cached = tokens;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(tokens));
    }

    public async Task ClearAsync()
    {
        _cached = null;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }
}
