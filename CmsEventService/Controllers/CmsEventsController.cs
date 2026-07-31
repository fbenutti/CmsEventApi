using CmsEventService.Authentication;
using CmsEventService.Events;
using CmsEventService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEventService.Controllers;

[ApiController]
[Route("cms/events")]
[Authorize(AuthenticationSchemes = BasicAuthenticationDefaults.CmsScheme)]
public sealed class CmsEventsController(ICmsEventProcessor processor) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CmsEventProcessingResult>> Ingest(
        [FromBody] List<CmsEventDto> events,
        CancellationToken cancellationToken)
    {
        if (events is null)
        {
            return BadRequest("Request body must be a JSON array.");
        }

        var result = await processor.ProcessAsync(events, cancellationToken);

        return result.Failed > 0
            ? Accepted(result)
            : Ok(result);
    }
}
