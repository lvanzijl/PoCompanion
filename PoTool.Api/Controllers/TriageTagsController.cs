using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Services;
using PoTool.Shared.BugTriage;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for triage tag catalog management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[DataSourceMode(RouteIntent.LiveAllowed)]
public class TriageTagsController : ControllerBase
{
    private readonly TriageTagService _triageTagService;
    private readonly ILogger<TriageTagsController> _logger;

    public TriageTagsController(
        TriageTagService triageTagService,
        ILogger<TriageTagsController> logger)
    {
        _triageTagService = triageTagService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all triage tags.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TriageTagDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TriageTagDto>>> GetAllTags(CancellationToken cancellationToken)
    {
        var tags = await _triageTagService.GetAllTagsAsync(cancellationToken);
        return Ok(tags);
    }

    /// <summary>
    /// Gets only enabled triage tags.
    /// </summary>
    [HttpGet("enabled")]
    [ProducesResponseType(typeof(List<TriageTagDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TriageTagDto>>> GetEnabledTags(CancellationToken cancellationToken)
    {
        var tags = await _triageTagService.GetEnabledTagsAsync(cancellationToken);
        return Ok(tags);
    }

    /// <summary>
    /// Creates a new triage tag.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TriageTagOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TriageTagOperationResponse>> CreateTag(
        [FromBody] CreateTriageTagRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _triageTagService.CreateTagAsync(request, cancellationToken);

        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Updates a triage tag.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TriageTagOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TriageTagOperationResponse>> UpdateTag(
        int id,
        [FromBody] UpdateTriageTagRequest request,
        CancellationToken cancellationToken)
    {
        // Ensure ID in path matches request
        if (request.Id != id)
        {
            return BadRequest("ID mismatch");
        }

        var response = await _triageTagService.UpdateTagAsync(request, cancellationToken);

        if (!response.Success)
        {
            if (response.Message?.Contains("not found") == true)
            {
                return NotFound(response);
            }
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Deletes a triage tag.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(TriageTagOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TriageTagOperationResponse>> DeleteTag(
        int id,
        CancellationToken cancellationToken)
    {
        var response = await _triageTagService.DeleteTagAsync(id, cancellationToken);

        if (!response.Success)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Reorders triage tags.
    /// </summary>
    [HttpPost("reorder")]
    [ProducesResponseType(typeof(TriageTagOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TriageTagOperationResponse>> ReorderTags(
        [FromBody] List<int> tagIds,
        CancellationToken cancellationToken)
    {
        var response = await _triageTagService.ReorderTagsAsync(tagIds, cancellationToken);

        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
