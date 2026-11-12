using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tickecheck.Api.Auth;
using Tickecheck.Application.Categories;

namespace Tickecheck.Api.Controllers;

[Authorize(Policy = Policies.TenantMember)]
public class CategoriesController(CreateCategoryHandler createHandler, GetCategoriesHandler getHandler) : ApiController
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll(CancellationToken cancellationToken)
    {
        return await getHandler.HandleAsync(cancellationToken);
    }

    [HttpPost]
    [Authorize(Policy = Policies.TenantAdminOnly)]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await createHandler.HandleAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, category);
    }
}
