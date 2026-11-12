using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Auth;

namespace Tickecheck.Api.Auth;

/// <summary>
/// Reads the tenant from the "tenant_id" claim of the JWT of the current request.
/// The token is signed by the API at login, so the client cannot change the claim.
/// </summary>
public class HttpTenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public Guid? TenantId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst(AppClaims.TenantId)?.Value;
            return Guid.TryParse(claim, out var tenantId) ? tenantId : null;
        }
    }
}
