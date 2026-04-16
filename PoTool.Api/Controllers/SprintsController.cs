using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for sprint management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[DataSourceMode(RouteIntent.LiveAllowed)]
public class SprintsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SprintsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all sprints for a specific team.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SprintDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<SprintDto>>> GetSprintsForTeam(
        [FromQuery] int teamId,
        CancellationToken cancellationToken = default)
    {
        if (teamId <= 0)
        {
            return BadRequest("teamId must be greater than 0");
        }

        var sprints = await _mediator.Send(new GetSprintsForTeamQuery(teamId), cancellationToken);
        return Ok(sprints);
    }

    /// <summary>
    /// Gets the current sprint for a specific team.
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(SprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SprintDto>> GetCurrentSprintForTeam(
        [FromQuery] int teamId,
        CancellationToken cancellationToken = default)
    {
        if (teamId <= 0)
        {
            return BadRequest("teamId must be greater than 0");
        }

        var sprint = await _mediator.Send(new GetCurrentSprintForTeamQuery(teamId), cancellationToken);

        if (sprint == null)
        {
            return NotFound($"No current sprint found for team {teamId}");
        }

        return Ok(sprint);
    }
}
