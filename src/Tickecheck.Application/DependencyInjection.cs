using Microsoft.Extensions.DependencyInjection;
using Tickecheck.Application.Auth;
using Tickecheck.Application.Categories;
using Tickecheck.Application.Comments;
using Tickecheck.Application.Dashboard;
using Tickecheck.Application.Tenants;
using Tickecheck.Application.Tickets;
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

        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<GetCategoriesHandler>();

        services.AddScoped<CreateTicketHandler>();
        services.AddScoped<GetTicketByIdHandler>();
        services.AddScoped<SearchTicketsHandler>();
        services.AddScoped<UpdateTicketHandler>();
        services.AddScoped<DeleteTicketHandler>();
        services.AddScoped<AssignTicketHandler>();
        services.AddScoped<ChangeTicketStatusHandler>();
        services.AddScoped<ReopenTicketHandler>();
        services.AddScoped<ChangeTicketPriorityHandler>();
        services.AddScoped<GetTicketHistoryHandler>();

        services.AddScoped<AddCommentHandler>();
        services.AddScoped<GetCommentsHandler>();

        services.AddScoped<GetDashboardStatsHandler>();

        return services;
    }
}
