using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for startup readiness and orchestration.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StartupController : ControllerBase
{
    private readonly IMediator _mediator;

    public StartupController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets the startup readiness state.
    /// Used by the Startup Orchestrator to determine where to route the user.
    /// </summary>
    [HttpGet("readiness")]
    [ProducesResponseType(typeof(StartupReadinessDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StartupReadinessDto>> GetStartupReadiness(CancellationToken cancellationToken)
    {
        var readiness = await _mediator.Send(new GetStartupReadinessQuery(), cancellationToken);
        return Ok(readiness);
    }
}
