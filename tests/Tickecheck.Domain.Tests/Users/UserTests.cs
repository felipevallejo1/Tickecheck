using Tickecheck.Domain.Common;
using Tickecheck.Domain.Users;

namespace Tickecheck.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SuperAdmin_cannot_belong_to_a_tenant()
    {
        Assert.Throws<DomainException>(
            () => new User(Guid.NewGuid(), "root@tickecheck.dev", "hash", "Root", UserRole.SuperAdmin, Now));
    }

    [Theory]
    [InlineData(UserRole.TenantAdmin)]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Requester)]
    public void Tenant_roles_require_a_tenant(UserRole role)
    {
        Assert.Throws<DomainException>(
            () => new User(null, "someone@acme.com", "hash", "Someone", role, Now));
    }

    [Fact]
    public void Email_is_stored_trimmed_and_in_lowercase()
    {
        var user = new User(Guid.NewGuid(), "  Ana.Perez@Acme.com ", "hash", "Ana Perez", UserRole.Agent, Now);

        Assert.Equal("ana.perez@acme.com", user.Email);
    }

    [Fact]
    public void SuperAdmin_is_created_without_tenant()
    {
        var user = new User(null, "root@tickecheck.dev", "hash", "Root", UserRole.SuperAdmin, Now);

        Assert.Null(user.TenantId);
        Assert.Equal(UserRole.SuperAdmin, user.Role);
    }
}
