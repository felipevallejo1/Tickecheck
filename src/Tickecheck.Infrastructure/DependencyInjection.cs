using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tickecheck.Application.Abstractions;
using Tickecheck.Infrastructure.Auth;
using Tickecheck.Infrastructure.Persistence;
using Tickecheck.Infrastructure.Persistence.Repositories;
using Tickecheck.Infrastructure.Persistence.Seed;

namespace Tickecheck.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        // Same DbContext instance as the repositories of the request, so one
        // SaveChanges commits what all of them changed.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();

        services.AddScoped<DbSeeder>();

        return services;
    }
}
