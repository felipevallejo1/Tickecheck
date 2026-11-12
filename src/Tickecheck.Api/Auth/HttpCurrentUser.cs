using System.Security.Claims;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Auth;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Users;

namespace Tickecheck.Api.Auth;

/// <summary>
/// Reads the authenticated user from the claims of the JWT.
/// </summary>
public class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId => Guid.Parse(GetClaim(AppClaims.UserId));

    public UserRole Role => Enum.Parse<UserRole>(GetClaim(AppClaims.Role));

    private string GetClaim(string claimType)
    {
        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        return user?.FindFirst(claimType)?.Value
            ?? throw new UnauthorizedException("The request is not authenticated.");
    }
}
