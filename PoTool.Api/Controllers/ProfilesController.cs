using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for profile management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProfilesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all profiles.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProfileDto>>> GetAllProfiles(CancellationToken cancellationToken)
    {
        var profiles = await _mediator.Send(new GetAllProfilesQuery(), cancellationToken);
        return Ok(profiles);
    }

    /// <summary>
    /// Gets a profile by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileDto>> GetProfileById(int id, CancellationToken cancellationToken)
    {
        var profile = await _mediator.Send(new GetProfileByIdQuery(id), cancellationToken);

        if (profile == null)
        {
            return NotFound();
        }

        return Ok(profile);
    }

    /// <summary>
    /// Gets the currently active profile.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileDto>> GetActiveProfile(CancellationToken cancellationToken)
    {
        var profile = await _mediator.Send(new GetActiveProfileQuery(), cancellationToken);

        if (profile == null)
        {
            return NotFound();
        }

        return Ok(profile);
    }

    /// <summary>
    /// Creates a new profile.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileDto>> CreateProfile(
        [FromBody] CreateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProfileCommand(
            request.Name,
            request.AreaPaths,
            request.TeamName,
            request.GoalIds,
            request.PictureType,
            request.DefaultPictureId,
            request.CustomPicturePath);

        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetProfileById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing profile.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileDto>> UpdateProfile(
        int id,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateProfileCommand(
                id,
                request.Name,
                request.AreaPaths,
                request.TeamName,
                request.GoalIds,
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
    /// Deletes a profile.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProfile(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteProfileCommand(id), cancellationToken);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Sets the active profile.
    /// </summary>
    [HttpPost("active")]
    [ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SettingsDto>> SetActiveProfile(
        [FromBody] SetActiveProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetActiveProfileCommand(request.ProfileId);
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }
}

/// <summary>
/// Request model for creating a profile.
/// </summary>
public record CreateProfileRequest(
    string Name,
    List<string> AreaPaths,
    string TeamName,
    List<int> GoalIds,
    ProfilePictureType PictureType = ProfilePictureType.Default,
    int DefaultPictureId = 0,
    string? CustomPicturePath = null
);

/// <summary>
/// Request model for updating a profile.
/// </summary>
public record UpdateProfileRequest(
    string Name,
    List<string> AreaPaths,
    string TeamName,
    List<int> GoalIds,
    ProfilePictureType? PictureType = null,
    int? DefaultPictureId = null,
    string? CustomPicturePath = null
);

/// <summary>
/// Request model for setting the active profile.
/// </summary>
public record SetActiveProfileRequest(int? ProfileId);
