using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.Settings;
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

    /// <summary>
    /// Gets work item type definitions from TFS.
    /// Returns the available work item types and their valid states.
    /// </summary>
    [HttpGet("workitem-type-definitions")]
    [ProducesResponseType(typeof(IEnumerable<WorkItemTypeDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<WorkItemTypeDefinitionDto>>> GetWorkItemTypeDefinitions(
        CancellationToken cancellationToken)
    {
        var definitions = await _mediator.Send(new GetWorkItemTypeDefinitionsQuery(), cancellationToken);
        return Ok(definitions);
    }

    /// <summary>
    /// Gets work item state classifications for the configured TFS project.
    /// </summary>
    [HttpGet("state-classifications")]
    [ProducesResponseType(typeof(GetStateClassificationsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetStateClassificationsResponse>> GetStateClassifications(
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetStateClassificationsQuery(), cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Saves work item state classifications for the configured TFS project.
    /// </summary>
    [HttpPost("state-classifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SaveStateClassifications(
        [FromBody] SaveStateClassificationsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SaveStateClassificationsCommand(request);
        var success = await _mediator.Send(command, cancellationToken);

        if (!success)
        {
            return BadRequest("Failed to save state classifications");
        }

        return Ok();
    }
}
