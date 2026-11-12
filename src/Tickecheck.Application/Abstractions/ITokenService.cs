using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Abstractions;

public record AccessToken(string Value, DateTime ExpiresAt);

public interface ITokenService
{
    TimeSpan RefreshTokenLifetime { get; }

    /// <summary>Creates the signed JWT that identifies the user on every request.</summary>
    AccessToken CreateAccessToken(User user);

    /// <summary>Creates a random, unguessable refresh token.</summary>
    string GenerateRefreshToken();

    /// <summary>Hash used to store and look up a refresh token.</summary>
    string HashRefreshToken(string refreshToken);
}
