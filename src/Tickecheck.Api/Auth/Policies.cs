using Microsoft.AspNetCore.Authorization;
using Tickecheck.Domain.Users;

namespace Tickecheck.Api.Auth;

/// <summary>
/// Authorization policies by role. Endpoints declare the policy they need with
/// [Authorize(Policy = ...)], so "who can call what" is visible on the controller
/// and is not scattered as ifs inside the use cases.
/// </summary>
public static class Policies
{
    public const string SuperAdminOnly = nameof(SuperAdminOnly);
    public const string TenantAdminOnly = nameof(TenantAdminOnly);

    // The hierarchy inside a company: TenantAdmin can do everything an Agent can,
    // and an Agent everything a Requester can.
    public const string AgentOrAbove = nameof(AgentOrAbove);
    public const string TenantMember = nameof(TenantMember);

    public static void AddAppPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(SuperAdminOnly, policy => policy.RequireRole(nameof(UserRole.SuperAdmin)));

        options.AddPolicy(TenantAdminOnly, policy => policy.RequireRole(nameof(UserRole.TenantAdmin)));

        options.AddPolicy(AgentOrAbove, policy => policy.RequireRole(
            nameof(UserRole.Agent),
            nameof(UserRole.TenantAdmin)));

        // SuperAdmin is deliberately left out: it manages companies, not their tickets.
        options.AddPolicy(TenantMember, policy => policy.RequireRole(
            nameof(UserRole.Requester),
            nameof(UserRole.Agent),
            nameof(UserRole.TenantAdmin)));
    }
}
