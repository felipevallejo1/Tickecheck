using Microsoft.EntityFrameworkCore;
using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Users;

namespace Tickecheck.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return db.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<List<User>> ListAsync(UserRole? role, CancellationToken cancellationToken = default)
    {
        var query = db.Users.AsNoTracking();

        if (role is not null)
        {
            query = query.Where(user => user.Role == role);
        }

        return query.OrderBy(user => user.FullName).ToListAsync(cancellationToken);
    }

    public void Add(User user)
    {
        db.Users.Add(user);
    }

    // IgnoreQueryFilters is used ONLY in the three methods below.
    // Login and refresh are anonymous requests: there is no tenant yet to filter by.
    // The email check must look at every tenant because emails are unique platform-wide.

    public Task<User?> FindByEmailIgnoringTenantAsync(string email, CancellationToken cancellationToken = default)
    {
        return db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<User?> GetByIdIgnoringTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<bool> EmailExistsIgnoringTenantAsync(string email, CancellationToken cancellationToken = default)
    {
        return db.Users.IgnoreQueryFilters().AnyAsync(user => user.Email == email, cancellationToken);
    }
}
