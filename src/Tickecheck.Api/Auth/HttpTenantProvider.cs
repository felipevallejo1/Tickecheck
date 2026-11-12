using Tickecheck.Application.Abstractions;

namespace Tickecheck.Api.Auth;

/// <summary>
/// Reads the tenant from the "tenant_id" claim of the JWT of the current request.
/// The claim is signed by the API at login, so the client cannot change it.
/// </summary>
public class HttpTenantProvider : ITenantProvider
{
    public const string TenantClaim = "tenant_id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst(TenantClaim)?.Value;
            return Guid.TryParse(claim, out var tenantId) ? tenantId : null;
        }
    }
}
