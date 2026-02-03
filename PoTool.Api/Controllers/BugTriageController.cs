using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Services;
using PoTool.Shared.BugTriage;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for bug triage state management.
/// Tracks which bugs have been triaged locally (not persisted to TFS).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BugTriageController : ControllerBase
{
    private readonly BugTriageStateService _triageStateService;
    private readonly ILogger<BugTriageController> _logger;

    public BugTriageController(
        BugTriageStateService triageStateService,
        ILogger<BugTriageController> logger)
    {
        _triageStateService = triageStateService;
        _logger = logger;
    }

    /// <summary>
    /// Gets triage state for a specific bug.
    /// </summary>
    [HttpGet("{bugId}")]
    [ProducesResponseType(typeof(BugTriageStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BugTriageStateDto>> GetTriageState(
        int bugId,
        CancellationToken cancellationToken)
    {
        var state = await _triageStateService.GetTriageStateAsync(bugId, cancellationToken);

        if (state == null)
        {
            return NotFound();
        }

        return Ok(state);
    }

    /// <summary>
    /// Gets triage states for multiple bugs.
    /// </summary>
    [HttpPost("states")]
    [ProducesResponseType(typeof(List<BugTriageStateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BugTriageStateDto>>> GetTriageStates(
        [FromBody] List<int> bugIds,
        CancellationToken cancellationToken)
    {
        var states = await _triageStateService.GetTriageStatesAsync(bugIds, cancellationToken);
        return Ok(states);
    }

    /// <summary>
    /// Gets IDs of all untriaged bugs from a given list.
    /// </summary>
    [HttpPost("untriaged")]
    [ProducesResponseType(typeof(HashSet<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<HashSet<int>>> GetUntriagedBugIds(
        [FromBody] List<int> bugIds,
        CancellationToken cancellationToken)
    {
        var untriagedIds = await _triageStateService.GetUntriagedBugIdsAsync(bugIds, cancellationToken);
        return Ok(untriagedIds);
    }

    /// <summary>
    /// Records that a bug was first seen in the triage UI.
    /// </summary>
    [HttpPost("first-seen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordFirstSeen(
        [FromQuery] int bugId,
        [FromQuery] string currentSeverity,
        CancellationToken cancellationToken)
    {
        await _triageStateService.RecordFirstSeenAsync(bugId, currentSeverity, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Marks a bug as triaged due to a user action.
    /// Logs what would be saved to TFS (severity and/or tags).
    /// </summary>
    [HttpPost("mark-triaged")]
    [ProducesResponseType(typeof(UpdateBugTriageStateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UpdateBugTriageStateResponse>> MarkAsTriaged(
        [FromBody] UpdateBugTriageStateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _triageStateService.MarkAsTriagedAsync(request, cancellationToken);
        return Ok(response);
    }
}
