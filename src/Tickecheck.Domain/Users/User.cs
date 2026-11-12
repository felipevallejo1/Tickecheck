using Tickecheck.Domain.Common;

namespace Tickecheck.Domain.Users;

public class User
{
    public Guid Id { get; private set; }

    // Null only for SuperAdmin, who belongs to the platform and not to a company.
    // That is why User does not implement ITenantEntity (its TenantId is not nullable).
    public Guid? TenantId { get; private set; }

    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Required by EF Core to materialize the entity.
    private User()
    {
    }

    public User(Guid? tenantId, string email, string passwordHash, string fullName, UserRole role, DateTime utcNow)
    {
        if (role == UserRole.SuperAdmin && tenantId is not null)
        {
            throw new DomainException("A SuperAdmin cannot belong to a tenant.");
        }

        if (role != UserRole.SuperAdmin && tenantId is null)
        {
            throw new DomainException("Only a SuperAdmin can exist without a tenant.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        // Emails are unique across the platform, so they are stored in a single canonical form.
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        Role = role;
        CreatedAt = utcNow;
    }
}
