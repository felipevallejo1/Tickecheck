using FluentValidation;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;

namespace Tickecheck.Application.Auth;

public record RefreshSessionCommand(string RefreshToken);

public class RefreshSessionValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}

/// <summary>
/// Exchanges a refresh token for a new pair of tokens. The old refresh token is
/// revoked in the same operation (rotation), so each one can be used only once.
/// </summary>
public class RefreshSessionHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    ITokenService tokenService,
    AuthTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private const string InvalidTokenMessage = "Invalid refresh token.";

    public async Task<AuthResponse> HandleAsync(RefreshSessionCommand command, CancellationToken cancellationToken = default)
    {
        new RefreshSessionValidator().ValidateAndThrow(command);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = tokenService.HashRefreshToken(command.RefreshToken);
        var storedToken = await refreshTokens.FindByHashAsync(tokenHash, cancellationToken);

        if (storedToken is null)
        {
            throw new UnauthorizedException(InvalidTokenMessage);
        }

        if (storedToken.IsRevoked)
        {
            // A rotated token is being used again. The legitimate client always uses the
            // newest one, so this token was probably stolen: close every session of the user.
            var activeTokens = await refreshTokens.ListActiveByUserAsync(storedToken.UserId, utcNow, cancellationToken);
            foreach (var activeToken in activeTokens)
            {
                activeToken.Revoke(utcNow);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException(InvalidTokenMessage);
        }

        if (storedToken.IsExpired(utcNow))
        {
            throw new UnauthorizedException(InvalidTokenMessage);
        }

        var user = await users.GetByIdIgnoringTenantAsync(storedToken.UserId, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedException(InvalidTokenMessage);
        }

        var (response, newTokenHash) = tokenIssuer.Issue(user);
        storedToken.Revoke(utcNow, newTokenHash);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}
