using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Core.ReleasePlanning.Commands;
using PoTool.Core.ReleasePlanning.Queries;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for Release Planning Board operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ReleasePlanningController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ReleasePlanningController> _logger;

    public ReleasePlanningController(
        IMediator mediator,
        ILogger<ReleasePlanningController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets the complete Release Planning Board state.
    /// </summary>
    [HttpGet("board")]
    public async Task<ActionResult<ReleasePlanningBoardDto>> GetBoard(CancellationToken cancellationToken)
    {
        try
        {
            var board = await _mediator.Send(new GetReleasePlanningBoardQuery(), cancellationToken);
            return Ok(board);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Release Planning Board");
            return StatusCode(500, "Error retrieving Release Planning Board");
        }
    }

    /// <summary>
    /// Gets all unplanned Epics (Epics not yet on the board).
    /// </summary>
    [HttpGet("unplanned-epics")]
    public async Task<ActionResult<IReadOnlyList<UnplannedEpicDto>>> GetUnplannedEpics(CancellationToken cancellationToken)
    {
        try
        {
            var epics = await _mediator.Send(new GetUnplannedEpicsQuery(), cancellationToken);
            return Ok(epics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unplanned Epics");
            return StatusCode(500, "Error retrieving unplanned Epics");
        }
    }

    /// <summary>
    /// Gets all Epics for a specific Objective.
    /// </summary>
    [HttpGet("objectives/{objectiveId:int}/epics")]
    public async Task<ActionResult<IReadOnlyList<ObjectiveEpicDto>>> GetObjectiveEpics(
        int objectiveId,
        CancellationToken cancellationToken)
    {
        try
        {
            var epics = await _mediator.Send(new GetObjectiveEpicsQuery(objectiveId), cancellationToken);
            return Ok(epics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Epics for Objective {ObjectiveId}", objectiveId);
            return StatusCode(500, "Error retrieving Objective Epics");
        }
    }

    #region Lane Operations

    /// <summary>
    /// Creates a new Lane for an Objective.
    /// </summary>
    [HttpPost("lanes")]
    public async Task<ActionResult<LaneOperationResultDto>> CreateLane(
        [FromBody] CreateLaneCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Lane");
            return StatusCode(500, "Error creating Lane");
        }
    }

    /// <summary>
    /// Deletes a Lane and all its placements.
    /// </summary>
    [HttpDelete("lanes/{laneId:int}")]
    public async Task<ActionResult<LaneOperationResultDto>> DeleteLane(
        int laneId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new DeleteLaneCommand(laneId), cancellationToken);
            
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Lane {LaneId}", laneId);
            return StatusCode(500, "Error deleting Lane");
        }
    }

    #endregion

    #region Epic Placement Operations

    /// <summary>
    /// Places an Epic on the board.
    /// </summary>
    [HttpPost("placements")]
    public async Task<ActionResult<EpicPlacementResultDto>> CreatePlacement(
        [FromBody] CreateEpicPlacementCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Epic placement");
            return StatusCode(500, "Error creating Epic placement");
        }
    }

    /// <summary>
    /// Updates an Epic placement.
    /// </summary>
    [HttpPut("placements/{placementId:int}")]
    public async Task<ActionResult<EpicPlacementResultDto>> UpdatePlacement(
        int placementId,
        [FromBody] UpdatePlacementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateEpicPlacementCommand(placementId, request.RowIndex, request.OrderInRow);
            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Epic placement {PlacementId}", placementId);
            return StatusCode(500, "Error updating Epic placement");
        }
    }

    /// <summary>
    /// Moves an Epic to a different row.
    /// </summary>
    [HttpPost("placements/{placementId:int}/move")]
    public async Task<ActionResult<EpicPlacementResultDto>> MoveEpic(
        int placementId,
        [FromBody] MoveEpicRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new MoveEpicCommand(placementId, request.NewRowIndex, request.NewOrderInRow);
            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving Epic placement {PlacementId}", placementId);
            return StatusCode(500, "Error moving Epic");
        }
    }

    /// <summary>
    /// Reorders Epics within a row.
    /// </summary>
    [HttpPost("rows/reorder")]
    public async Task<ActionResult<EpicPlacementResultDto>> ReorderEpicsInRow(
        [FromBody] ReorderEpicsInRowCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering Epics in row");
            return StatusCode(500, "Error reordering Epics");
        }
    }

    #endregion

    #region Milestone Line Operations

    /// <summary>
    /// Creates a new Milestone Line.
    /// </summary>
    [HttpPost("milestone-lines")]
    public async Task<ActionResult<LineOperationResultDto>> CreateMilestoneLine(
        [FromBody] CreateMilestoneLineCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Milestone Line");
            return StatusCode(500, "Error creating Milestone Line");
        }
    }

    /// <summary>
    /// Updates a Milestone Line.
    /// </summary>
    [HttpPut("milestone-lines/{lineId:int}")]
    public async Task<ActionResult<LineOperationResultDto>> UpdateMilestoneLine(
        int lineId,
        [FromBody] UpdateMilestoneLineRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateMilestoneLineCommand(lineId, request.Label, request.VerticalPosition, request.Type);
            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Milestone Line {LineId}", lineId);
            return StatusCode(500, "Error updating Milestone Line");
        }
    }

    /// <summary>
    /// Deletes a Milestone Line.
    /// </summary>
    [HttpDelete("milestone-lines/{lineId:int}")]
    public async Task<ActionResult<LineOperationResultDto>> DeleteMilestoneLine(
        int lineId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new DeleteMilestoneLineCommand(lineId), cancellationToken);
            
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Milestone Line {LineId}", lineId);
            return StatusCode(500, "Error deleting Milestone Line");
        }
    }

    #endregion

    #region Iteration Line Operations

    /// <summary>
    /// Creates a new Iteration Line.
    /// </summary>
    [HttpPost("iteration-lines")]
    public async Task<ActionResult<LineOperationResultDto>> CreateIterationLine(
        [FromBody] CreateIterationLineCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Iteration Line");
            return StatusCode(500, "Error creating Iteration Line");
        }
    }

    /// <summary>
    /// Updates an Iteration Line.
    /// </summary>
    [HttpPut("iteration-lines/{lineId:int}")]
    public async Task<ActionResult<LineOperationResultDto>> UpdateIterationLine(
        int lineId,
        [FromBody] UpdateIterationLineRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateIterationLineCommand(lineId, request.Label, request.VerticalPosition);
            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Iteration Line {LineId}", lineId);
            return StatusCode(500, "Error updating Iteration Line");
        }
    }

    /// <summary>
    /// Deletes an Iteration Line.
    /// </summary>
    [HttpDelete("iteration-lines/{lineId:int}")]
    public async Task<ActionResult<LineOperationResultDto>> DeleteIterationLine(
        int lineId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new DeleteIterationLineCommand(lineId), cancellationToken);
            
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Iteration Line {LineId}", lineId);
            return StatusCode(500, "Error deleting Iteration Line");
        }
    }

    #endregion

    #region Validation

    /// <summary>
    /// Refreshes the validation cache for all Epics on the board.
    /// </summary>
    [HttpPost("validation/refresh")]
    public async Task<ActionResult<ValidationCacheResultDto>> RefreshValidation(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new RefreshValidationCacheCommand(), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing validation cache");
            return StatusCode(500, "Error refreshing validation cache");
        }
    }

    #endregion
}

/// <summary>
/// Request model for updating a placement.
/// </summary>
public record UpdatePlacementRequest(int RowIndex, int OrderInRow);

/// <summary>
/// Request model for moving an Epic.
/// </summary>
public record MoveEpicRequest(int NewRowIndex, int NewOrderInRow);

/// <summary>
/// Request model for updating a Milestone Line.
/// </summary>
public record UpdateMilestoneLineRequest(string Label, double VerticalPosition, MilestoneType Type);

/// <summary>
/// Request model for updating an Iteration Line.
/// </summary>
public record UpdateIterationLineRequest(string Label, double VerticalPosition);
