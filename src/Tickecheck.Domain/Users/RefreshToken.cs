namespace Tickecheck.Domain.Users;

/// <summary>
/// A long-lived token that lets a client get a new access token without logging in again.
/// Only its hash is stored: if the database leaks, the tokens cannot be used.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    // Hash of the token that replaced this one when it was rotated. Kept for traceability.
    public string? ReplacedByTokenHash { get; private set; }

    // Required by EF Core to materialize the entity.
    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt, DateTime utcNow)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = utcNow;
    }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTime utcNow)
    {
        return utcNow >= ExpiresAt;
    }

    public void Revoke(DateTime utcNow, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = utcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
