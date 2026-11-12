using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tickecheck.Api.Auth;
using Tickecheck.Application.Dashboard;

namespace Tickecheck.Api.Controllers;

[Authorize(Policy = Policies.AgentOrAbove)]
public class DashboardController(GetDashboardStatsHandler handler) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<DashboardStatsDto>> Get(CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(cancellationToken);
    }
}
