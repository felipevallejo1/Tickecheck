using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Abstractions;

/// <summary>
/// The authenticated user making the current request.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    UserRole Role { get; }
}
