using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Auth;

public record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken);

/// <summary>
/// Creates the pair of tokens for a user. Shared by login and refresh so both
/// issue tokens in exactly the same way.
/// </summary>
public class AuthTokenIssuer(
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokens,
    TimeProvider timeProvider)
{
    /// <summary>
    /// The new refresh token is added to the repository but not saved:
    /// the calling handler commits it together with its own changes.
    /// </summary>
    public (AuthResponse Response, string RefreshTokenHash) Issue(User user)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var accessToken = tokenService.CreateAccessToken(user);

        // The client receives the raw token; the database only ever sees its hash.
        var rawRefreshToken = tokenService.GenerateRefreshToken();
        var refreshTokenHash = tokenService.HashRefreshToken(rawRefreshToken);
        refreshTokens.Add(new RefreshToken(user.Id, refreshTokenHash, utcNow + tokenService.RefreshTokenLifetime, utcNow));

        var response = new AuthResponse(accessToken.Value, accessToken.ExpiresAt, rawRefreshToken);
        return (response, refreshTokenHash);
    }
}
