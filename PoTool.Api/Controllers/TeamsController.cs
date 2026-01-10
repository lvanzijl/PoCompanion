using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for team management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeamsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all teams.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamDto>>> GetAllTeams(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var teams = await _mediator.Send(new GetAllTeamsQuery(includeArchived), cancellationToken);
        return Ok(teams);
    }

    /// <summary>
    /// Gets a team by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> GetTeamById(int id, CancellationToken cancellationToken)
    {
        var team = await _mediator.Send(new GetTeamByIdQuery(id), cancellationToken);

        if (team == null)
        {
            return NotFound();
        }

        return Ok(team);
    }

    /// <summary>
    /// Creates a new team.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TeamDto>> CreateTeam(
        [FromBody] CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateTeamCommand(
            request.Name,
            request.TeamAreaPath,
            request.PictureType,
            request.DefaultPictureId,
            request.CustomPicturePath);

        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetTeamById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing team.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> UpdateTeam(
        int id,
        [FromBody] UpdateTeamRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateTeamCommand(
                id,
                request.Name,
                request.TeamAreaPath,
                request.PictureType,
                request.DefaultPictureId,
                request.CustomPicturePath);

            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Archives or unarchives a team.
    /// </summary>
    [HttpPost("{id}/archive")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> ArchiveTeam(
        int id,
        [FromBody] ArchiveTeamRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ArchiveTeamCommand(id, request.IsArchived);
            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}

/// <summary>
/// Request model for creating a team.
/// </summary>
public record CreateTeamRequest(
    string Name,
    string TeamAreaPath,
    TeamPictureType PictureType = TeamPictureType.Default,
    int DefaultPictureId = 0,
    string? CustomPicturePath = null
);

/// <summary>
/// Request model for updating a team.
/// </summary>
public record UpdateTeamRequest(
    string Name,
    string TeamAreaPath,
    TeamPictureType? PictureType = null,
    int? DefaultPictureId = null,
    string? CustomPicturePath = null
);

/// <summary>
/// Request model for archiving a team.
/// </summary>
public record ArchiveTeamRequest(bool IsArchived);
