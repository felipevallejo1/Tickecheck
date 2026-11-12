using Microsoft.Extensions.DependencyInjection;
using Tickecheck.Application.Auth;
using Tickecheck.Application.Tenants;
using Tickecheck.Application.Users;

namespace Tickecheck.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every use case. Handlers are listed one by one on purpose: it is more
    /// lines than scanning the assembly, but anyone can see what exists and nothing is magic.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<AuthTokenIssuer>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshSessionHandler>();
        services.AddScoped<LogoutHandler>();

        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<GetTenantsHandler>();

        services.AddScoped<CreateUserHandler>();
        services.AddScoped<GetUsersHandler>();

        return services;
    }
}
