using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tickecheck.Api.Auth;
using Tickecheck.Application.Tenants;

namespace Tickecheck.Api.Controllers;

[Authorize(Policy = Policies.SuperAdminOnly)]
public class TenantsController(CreateTenantHandler createHandler, GetTenantsHandler getHandler) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<List<TenantDto>>> GetAll(CancellationToken cancellationToken)
    {
        return await getHandler.HandleAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<TenantDto>> Create(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        var tenant = await createHandler.HandleAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, tenant);
    }
}
