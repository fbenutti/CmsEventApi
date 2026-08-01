using CmsEventService.Authentication;
using CmsEventService.Events;
using CmsEventService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEventService.Controllers;

[ApiController]
[Route("entities")]
[Authorize(AuthenticationSchemes = BasicAuthenticationDefaults.ApiScheme)]
public sealed class EntitiesController(
    IEntityQueryService queryService,
    IEntityAdministrationService administrationService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<EntityResponse>>> List(
        [FromQuery] EntityQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var includeDisabled = User.IsInRole("Admin");
        var entities = await queryService.ListAsync(includeDisabled, parameters, cancellationToken);
        return Ok(entities);
    }

    [HttpPatch("{id}/disabled")]
    [Authorize(AuthenticationSchemes = BasicAuthenticationDefaults.ApiScheme, Roles = "Admin")]
    public async Task<IActionResult> SetLocalDisabled(
        string id,
        [FromBody] DisableEntityRequest request,
        CancellationToken cancellationToken)
    {
        var changed = await administrationService.SetLocalDisabledAsync(id, request.Disabled, cancellationToken);
        return changed ? NoContent() : NotFound();
    }
}
