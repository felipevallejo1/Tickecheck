namespace Tickecheck.Application.Auth;

/// <summary>
/// Names of the claims inside the access token. Shared by the code that writes
/// the token (Infrastructure) and the code that reads it (Api).
/// </summary>
public static class AppClaims
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string TenantId = "tenant_id";
}
