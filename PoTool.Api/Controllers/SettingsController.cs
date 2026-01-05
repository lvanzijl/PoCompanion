using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Settings;
using PoTool.Core.Settings.Commands;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for application settings.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SettingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets the current settings.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
        
        if (settings == null)
        {
            return NotFound();
        }

        return Ok(settings);
    }

    /// <summary>
    /// Gets effort estimation settings.
    /// </summary>
    [HttpGet("effort-estimation")]
    [ProducesResponseType(typeof(EffortEstimationSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EffortEstimationSettingsDto>> GetEffortEstimationSettings(CancellationToken cancellationToken)
    {
        var settings = await _mediator.Send(new GetEffortEstimationSettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Updates effort estimation settings.
    /// </summary>
    [HttpPut("effort-estimation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateEffortEstimationSettings(
        [FromBody] EffortEstimationSettingsDto settings,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEffortEstimationSettingsCommand(settings);
        await _mediator.Send(command, cancellationToken);
        return Ok();
    }
}
