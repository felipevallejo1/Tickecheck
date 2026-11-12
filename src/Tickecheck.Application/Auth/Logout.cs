using Tickecheck.Application.Abstractions;

namespace Tickecheck.Application.Auth;

public record LogoutCommand(string RefreshToken);

/// <summary>
/// Revokes the refresh token. The access token cannot be revoked (the API does not
/// store it), which is why it has a short life: it simply expires a few minutes later.
/// </summary>
public class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return;
        }

        var tokenHash = tokenService.HashRefreshToken(command.RefreshToken);
        var storedToken = await refreshTokens.FindByHashAsync(tokenHash, cancellationToken);

        // An unknown token is not an error: the goal (no valid session) is already met.
        if (storedToken is null)
        {
            return;
        }

        storedToken.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
