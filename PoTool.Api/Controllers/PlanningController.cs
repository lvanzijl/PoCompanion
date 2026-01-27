using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Planning.Commands;
using PoTool.Core.Planning.Queries;
using PoTool.Shared.Planning;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for new Planning Board operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PlanningController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PlanningController> _logger;

    public PlanningController(
        IMediator mediator,
        ILogger<PlanningController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets the complete Planning Board state for a Product Owner.
    /// </summary>
    [HttpGet("board/{productOwnerId:int}")]
    public async Task<ActionResult<PlanningBoardDto>> GetBoard(
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var board = await _mediator.Send(new GetPlanningBoardQuery(productOwnerId), cancellationToken);
            return Ok(board);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Planning Board for Product Owner {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving Planning Board");
        }
    }

    /// <summary>
    /// Gets unplanned Epics for a Product Owner.
    /// </summary>
    [HttpGet("unplanned-epics/{productOwnerId:int}")]
    public async Task<ActionResult<IReadOnlyList<UnplannedEpicDto>>> GetUnplannedEpics(
        int productOwnerId,
        [FromQuery] int? productId,
        CancellationToken cancellationToken)
    {
        try
        {
            var epics = await _mediator.Send(new GetUnplannedEpicsQuery(productOwnerId, productId), cancellationToken);
            return Ok(epics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unplanned Epics for Product Owner {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving unplanned Epics");
        }
    }

    /// <summary>
    /// Initializes the default board layout.
    /// </summary>
    [HttpPost("board/{productOwnerId:int}/initialize")]
    public async Task<ActionResult<BoardOperationResultDto>> InitializeBoard(
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new InitializeDefaultBoardCommand(productOwnerId), cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Planning Board for Product Owner {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error initializing Planning Board");
        }
    }

    #region Row Operations

    /// <summary>
    /// Creates a new row on the board.
    /// </summary>
    [HttpPost("rows")]
    public async Task<ActionResult<RowOperationResultDto>> CreateRow(
        [FromBody] CreateRowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new CreateBoardRowCommand(request.InsertBeforeRowId, request.InsertBelow), 
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating board row");
            return StatusCode(500, new RowOperationResultDto 
            { 
                Success = false, 
                ErrorMessage = "Error creating row" 
            });
        }
    }

    /// <summary>
    /// Creates a marker row (Iteration or Release line).
    /// </summary>
    [HttpPost("rows/marker")]
    public async Task<ActionResult<RowOperationResultDto>> CreateMarkerRow(
        [FromBody] CreateMarkerRowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new CreateMarkerRowCommand(request.MarkerType, request.Label, request.InsertBeforeRowId, request.InsertBelow), 
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating marker row");
            return StatusCode(500, new RowOperationResultDto 
            { 
                Success = false, 
                ErrorMessage = "Error creating marker row" 
            });
        }
    }

    /// <summary>
    /// Deletes a row from the board.
    /// </summary>
    [HttpDelete("rows/{rowId:int}")]
    public async Task<ActionResult<RowOperationResultDto>> DeleteRow(
        int rowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new DeleteBoardRowCommand(rowId), cancellationToken);

            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting row {RowId}", rowId);
            return StatusCode(500, new RowOperationResultDto 
            { 
                Success = false, 
                ErrorMessage = "Error deleting row" 
            });
        }
    }

    /// <summary>
    /// Updates a marker row's label.
    /// </summary>
    [HttpPut("rows/marker/{rowId:int}")]
    public async Task<ActionResult<RowOperationResultDto>> UpdateMarkerRow(
        int rowId,
        [FromBody] UpdateMarkerRowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new UpdateMarkerRowCommand(rowId, request.Label), 
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating marker row {RowId}", rowId);
            return StatusCode(500, new RowOperationResultDto 
            { 
                Success = false, 
                ErrorMessage = "Error updating marker row" 
            });
        }
    }

    /// <summary>
    /// Moves a row to a new position.
    /// </summary>
    [HttpPut("rows/{rowId:int}/move")]
    public async Task<ActionResult<RowOperationResultDto>> MoveRow(
        int rowId,
        [FromBody] MoveRowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new MoveRowCommand(rowId, request.TargetRowId, request.InsertBelow), 
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving row {RowId}", rowId);
            return StatusCode(500, new RowOperationResultDto 
            { 
                Success = false, 
                ErrorMessage = "Error moving row" 
            });
        }
    }

    #endregion

    #region Epic Placement Operations

    /// <summary>
    /// Places an Epic on the board.
    /// </summary>
    [HttpPost("placements")]
    public async Task<ActionResult<PlacementOperationResultDto>> CreatePlacement(
        [FromBody] CreateEpicPlacementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new CreatePlanningEpicPlacementCommand(request.EpicId, request.RowId, request.OrderInCell), 
                cancellationToken);

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
    /// Moves an Epic placement to a different row.
    /// </summary>
    [HttpPut("placements/{placementId:int}/move")]
    public async Task<ActionResult<PlacementOperationResultDto>> MovePlacement(
        int placementId,
        [FromBody] MovePlanningEpicRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new MovePlanningEpicCommand(placementId, request.NewRowId, request.NewOrderInCell), 
                cancellationToken);

            if (!result.Success)
            {
                return NotFound(result);
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
    /// Deletes Epic placements from the board.
    /// </summary>
    [HttpPost("placements/delete")]
    public async Task<ActionResult<PlacementOperationResultDto>> DeletePlacements(
        [FromBody] DeletePlacementsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new DeletePlanningEpicPlacementsCommand(request.PlacementIds), 
                cancellationToken);

            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Epic placements");
            return StatusCode(500, "Error deleting Epic placements");
        }
    }

    #endregion

    #region Board Settings

    /// <summary>
    /// Updates the board scope settings.
    /// </summary>
    [HttpPut("board/{productOwnerId:int}/scope")]
    public async Task<ActionResult<BoardOperationResultDto>> UpdateScope(
        int productOwnerId,
        [FromBody] UpdateBoardScopeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new UpdateBoardScopeCommand(productOwnerId, request.Scope, request.SelectedProductId), 
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating board scope for Product Owner {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error updating board scope");
        }
    }

    /// <summary>
    /// Updates product column visibility.
    /// </summary>
    [HttpPut("board/{productOwnerId:int}/visibility")]
    public async Task<ActionResult<BoardOperationResultDto>> UpdateProductVisibility(
        int productOwnerId,
        [FromBody] UpdateProductVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(
                new UpdateProductVisibilityCommand(productOwnerId, request.ProductId, request.IsVisible), 
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product visibility for Product Owner {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error updating product visibility");
        }
    }

    #endregion
}

/// <summary>
/// Request model for deleting placements.
/// </summary>
public record DeletePlacementsRequest
{
    /// <summary>
    /// List of placement IDs to delete.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(1)]
    public required IReadOnlyList<int> PlacementIds { get; init; }
}
