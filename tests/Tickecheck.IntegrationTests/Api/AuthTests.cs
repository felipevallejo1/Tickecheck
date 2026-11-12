using System.Net;
using Microsoft.EntityFrameworkCore;
using Tickecheck.Application.Auth;

namespace Tickecheck.IntegrationTests.Api;

[Collection(IntegrationCollection.Name)]
public class AuthTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task Login_with_valid_credentials_returns_both_tokens()
    {
        var tokens = await fixture.LoginAsync(DemoUsers.AcmeAgent1);

        Assert.NotEmpty(tokens.AccessToken);
        Assert.NotEmpty(tokens.RefreshToken);
        Assert.True(tokens.AccessTokenExpiresAt > DateTime.UtcNow);
    }

    [Theory]
    [InlineData(DemoUsers.AcmeAgent1, "wrong-password")]
    [InlineData("nobody@acme.demo", "Demo123!")]
    public async Task Login_with_invalid_credentials_returns_401(string email, string password)
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsync("/api/auth/login", new LoginCommand(email, password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_empty_fields_returns_400_as_problem_details()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsync("/api/auth/login", new LoginCommand("", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Request_without_token_returns_401()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_returns_new_tokens_and_the_old_refresh_token_stops_working()
    {
        using var client = fixture.Factory.CreateClient();
        var first = await fixture.LoginAsync(DemoUsers.AcmeUser2);

        var refreshResponse = await client.PostAsync("/api/auth/refresh", new RefreshSessionCommand(first.RefreshToken));
        var second = await refreshResponse.ReadAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        var reuseResponse = await client.PostAsync("/api/auth/refresh", new RefreshSessionCommand(first.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_newest_one_too()
    {
        using var client = fixture.Factory.CreateClient();
        var first = await fixture.LoginAsync(DemoUsers.GlobexUser1);
        var second = await (await client.PostAsync("/api/auth/refresh", new RefreshSessionCommand(first.RefreshToken)))
            .ReadAsync<AuthResponse>();

        // Someone presents the old token again: it is treated as stolen.
        await client.PostAsync("/api/auth/refresh", new RefreshSessionCommand(first.RefreshToken));

        var response = await client.PostAsync("/api/auth/refresh", new RefreshSessionCommand(second.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        using var client = fixture.Factory.CreateClient();
        var tokens = await fixture.LoginAsync(DemoUsers.AcmeAgent2);

        var logoutResponse = await client.PostAsync("/api/auth/logout", new LogoutCommand(tokens.RefreshToken));
        var refreshResponse = await client.PostAsync("/api/auth/refresh", new RefreshSessionCommand(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_tokens_are_not_stored_in_plain_text()
    {
        var tokens = await fixture.LoginAsync(DemoUsers.AcmeAdmin);

        await using var db = fixture.CreateDbContext(tenantId: null);
        var storedInPlainText = await db.RefreshTokens.AnyAsync(token => token.TokenHash == tokens.RefreshToken);

        Assert.False(storedInPlainText);
    }
}
