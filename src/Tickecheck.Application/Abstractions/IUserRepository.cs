using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>Finds a user of the current tenant.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists the users of the current tenant, optionally by role.</summary>
    Task<List<User>> ListAsync(UserRole? role, CancellationToken cancellationToken = default);

    void Add(User user);

    // The three methods below skip the tenant filter. They exist because at login and
    // refresh there is no JWT yet (so no tenant to filter by), and because emails are
    // unique across the whole platform. Nothing else may bypass the filter.

    Task<User?> FindByEmailIgnoringTenantAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByIdIgnoringTenantAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsIgnoringTenantAsync(string email, CancellationToken cancellationToken = default);
}
