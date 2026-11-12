using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Tickecheck.Web.Services;

/// <summary>
/// Tells Blazor who the user is, by reading the claims inside the stored access token.
/// This only drives the UI (which menus and buttons to show). It is NOT security:
/// the API validates the token signature and the role on every request.
/// </summary>
public class JwtAuthStateProvider(TokenStore tokenStore) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var tokens = await tokenStore.GetAsync();
        if (tokens is null)
        {
            return Anonymous;
        }

        var claims = ReadClaims(tokens.AccessToken);
        // The third and fourth arguments say which claims hold the name and the role,
        // so AuthorizeView Roles="..." and User.Identity.Name work.
        var identity = new ClaimsIdentity(claims, "jwt", "name", "role");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>Called after login, refresh or logout so the UI re-renders for the new user.</summary>
    public void NotifyChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static List<Claim> ReadClaims(string jwt)
    {
        // A JWT is three Base64Url parts separated by dots: header.payload.signature.
        // The payload is plain JSON with the claims. The browser cannot verify the
        // signature (it does not have the key), and it does not need to.
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        var json = Convert.FromBase64String(payload);
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];

        return values
            .Where(pair => pair.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            .Select(pair => new Claim(pair.Key, pair.Value.ToString()))
            .ToList();
    }
}
