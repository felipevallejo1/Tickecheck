using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Tickecheck.Api.Controllers;

/// <summary>
/// Base class of every controller. [Authorize] here makes endpoints private by default:
/// a new endpoint is only public if it explicitly says [AllowAnonymous].
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public abstract class ApiController : ControllerBase
{
}
