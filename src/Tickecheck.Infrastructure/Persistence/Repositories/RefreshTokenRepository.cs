using Microsoft.EntityFrameworkCore;
using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Users;

namespace Tickecheck.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public Task<List<RefreshToken>> ListActiveByUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > utcNow)
            .ToListAsync(cancellationToken);
    }

    public void Add(RefreshToken refreshToken)
    {
        db.RefreshTokens.Add(refreshToken);
    }
}
