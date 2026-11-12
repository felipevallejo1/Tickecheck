using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Tokens of the user that are neither revoked nor expired.</summary>
    Task<List<RefreshToken>> ListActiveByUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);
}
