using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Services;
using PoTool.Shared.Planning;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for roadmap snapshot management.
/// Snapshots are stored in the application database and never modify TFS.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoadmapSnapshotsController : ControllerBase
{
    private readonly RoadmapSnapshotService _snapshotService;

    public RoadmapSnapshotsController(RoadmapSnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
    }

    /// <summary>
    /// Lists all roadmap snapshots, ordered newest first.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<RoadmapSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RoadmapSnapshotDto>>> ListSnapshots(
        CancellationToken cancellationToken)
    {
        var snapshots = await _snapshotService.ListSnapshotsAsync(cancellationToken);
        return Ok(snapshots);
    }

    /// <summary>
    /// Gets a single snapshot with full detail (products and epics).
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoadmapSnapshotDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoadmapSnapshotDetailDto>> GetSnapshot(
        int id,
        CancellationToken cancellationToken)
    {
        var detail = await _snapshotService.GetSnapshotDetailAsync(id, cancellationToken);
        if (detail == null) return NotFound();
        return Ok(detail);
    }

    /// <summary>
    /// Creates a new roadmap snapshot from the provided roadmap state.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoadmapSnapshotDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoadmapSnapshotDto>> CreateSnapshot(
        [FromBody] CreateRoadmapSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _snapshotService.CreateSnapshotAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSnapshot), new { id = snapshot.Id }, snapshot);
    }

    /// <summary>
    /// Deletes a roadmap snapshot.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSnapshot(
        int id,
        CancellationToken cancellationToken)
    {
        var deleted = await _snapshotService.DeleteSnapshotAsync(id, cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
