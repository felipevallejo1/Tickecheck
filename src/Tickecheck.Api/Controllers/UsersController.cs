using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tickecheck.Api.Auth;
using Tickecheck.Application.Users;
using Tickecheck.Domain.Users;

namespace Tickecheck.Api.Controllers;

public class UsersController(CreateUserHandler createHandler, GetUsersHandler getHandler) : ApiController
{
    // Agents can list users too: they need the list of agents to assign tickets.
    [HttpGet]
    [Authorize(Policy = Policies.AgentOrAbove)]
    public async Task<ActionResult<List<UserDto>>> GetAll([FromQuery] UserRole? role, CancellationToken cancellationToken)
    {
        return await getHandler.HandleAsync(role, cancellationToken);
    }

    [HttpPost]
    [Authorize(Policy = Policies.TenantAdminOnly)]
    public async Task<ActionResult<UserDto>> Create(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await createHandler.HandleAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, user);
    }
}
