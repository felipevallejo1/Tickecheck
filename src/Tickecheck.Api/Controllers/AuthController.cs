using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tickecheck.Application.Auth;

namespace Tickecheck.Api.Controllers;

[AllowAnonymous]
public class AuthController(
    LoginHandler loginHandler,
    RefreshSessionHandler refreshHandler,
    LogoutHandler logoutHandler) : ApiController
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        return await loginHandler.HandleAsync(command, cancellationToken);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        return await refreshHandler.HandleAsync(command, cancellationToken);
    }

    // Anonymous on purpose: a client whose access token already expired must still be able to log out.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken cancellationToken)
    {
        await logoutHandler.HandleAsync(command, cancellationToken);
        return NoContent();
    }
}
