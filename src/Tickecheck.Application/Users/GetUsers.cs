using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Users;

public class GetUsersHandler(IUserRepository users)
{
    public async Task<List<UserDto>> HandleAsync(UserRole? role, CancellationToken cancellationToken = default)
    {
        var list = await users.ListAsync(role, cancellationToken);
        return list.Select(user => new UserDto(user.Id, user.Email, user.FullName, user.Role)).ToList();
    }
}
