using Moq;
using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Tickets;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Tests;

/// <summary>
/// Shared building blocks for the handler tests.
/// </summary>
internal static class TestSupport
{
    public static readonly DateTime Now = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);
    public static readonly Guid TenantId = Guid.NewGuid();

    /// <summary>A clock stopped at <see cref="Now"/>, so results do not depend on when the test runs.</summary>
    public static TimeProvider Clock()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(provider => provider.GetUtcNow()).Returns(new DateTimeOffset(Now));
        return clock.Object;
    }

    public static ICurrentUser CurrentUser(UserRole role, Guid? userId = null)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(user => user.UserId).Returns(userId ?? Guid.NewGuid());
        currentUser.Setup(user => user.Role).Returns(role);
        currentUser.Setup(user => user.FullName).Returns("Test User");
        return currentUser.Object;
    }

    public static User NewUser(UserRole role, string email = "someone@acme.demo")
    {
        Guid? tenantId = role == UserRole.SuperAdmin ? null : TenantId;
        return new User(tenantId, email, "stored-hash", "Someone", role, Now);
    }

    public static Ticket NewTicket(Guid? requesterId = null)
    {
        return new Ticket("Printer is broken", "It does not print.", TicketPriority.Medium, Guid.NewGuid(), requesterId ?? Guid.NewGuid(), Now);
    }
}
