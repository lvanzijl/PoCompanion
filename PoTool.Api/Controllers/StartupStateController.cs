using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Controllers;

[ApiController]
[Route("api/startup-state")]
[DataSourceMode(RouteIntent.LiveAllowed)]
public sealed class StartupStateController : ControllerBase
{
    private readonly IMediator _mediator;

    public StartupStateController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(StartupStateResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StartupStateResponseDto>> Get(
        [FromQuery] string? returnUrl,
        [FromQuery] int? profileHintId,
        CancellationToken cancellationToken)
    {
        var startupState = await _mediator.Send(new GetStartupStateQuery(returnUrl, profileHintId), cancellationToken);
        return Ok(startupState);
    }
}
