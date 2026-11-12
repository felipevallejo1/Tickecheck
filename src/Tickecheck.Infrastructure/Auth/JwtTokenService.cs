using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Auth;
using Tickecheck.Domain.Users;

namespace Tickecheck.Infrastructure.Auth;

public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public AccessToken CreateAccessToken(User user)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = utcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AppClaims.UserId, user.Id.ToString()),
            new(AppClaims.Email, user.Email),
            new(AppClaims.Name, user.FullName),
            new(AppClaims.Role, user.Role.ToString())
        };

        // The SuperAdmin has no tenant, so the token carries no tenant claim
        // and every tenant-filtered query returns nothing for them.
        if (user.TenantId is not null)
        {
            claims.Add(new Claim(AppClaims.TenantId, user.TenantId.Value.ToString()));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = utcNow,
            NotBefore = utcNow,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expiresAt);
    }

    public string GenerateRefreshToken()
    {
        // 64 random bytes from the cryptographic generator: impossible to guess.
        return Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
    }

    public string HashRefreshToken(string refreshToken)
    {
        // Plain SHA-256, not the password hasher. The token is already long and random,
        // so it needs no salt or slow hashing, and a deterministic hash lets us find
        // the row with a simple indexed lookup.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(hash);
    }
}
