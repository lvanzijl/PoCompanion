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

    /// <summary>
    /// Deletes an Epic placement from the board (returns it to unplanned list).
    /// </summary>
    [HttpDelete("placements/{placementId:int}")]
    public async Task<ActionResult<EpicPlacementResultDto>> DeletePlacement(
        int placementId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new DeleteEpicPlacementCommand(placementId), cancellationToken);

            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Epic placement {PlacementId}", placementId);
            return StatusCode(500, "Error deleting Epic placement");
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

    #region Epic Split

    /// <summary>
    /// Gets the Features for an Epic (for split dialog).
    /// </summary>
    [HttpGet("epics/{epicId:int}/features")]
    public async Task<ActionResult<IReadOnlyList<EpicFeatureDto>>> GetEpicFeatures(
        int epicId,
        CancellationToken cancellationToken)
    {
        try
        {
            var features = await _mediator.Send(new GetEpicFeaturesQuery(epicId), cancellationToken);
            return Ok(features);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Features for Epic {EpicId}", epicId);
            return StatusCode(500, "Error retrieving Epic Features");
        }
    }

    /// <summary>
    /// Splits an Epic into two Epics.
    /// </summary>
    [HttpPost("epics/{epicId:int}/split")]
    public async Task<ActionResult<EpicSplitResultDto>> SplitEpic(
        int epicId,
        [FromBody] SplitEpicRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new SplitEpicCommand(epicId, request.ExtractedEpicTitle, request.FeatureIdsForExtractedEpic);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting Epic {EpicId}", epicId);
            return StatusCode(500, "Error splitting Epic");
        }
    }

    #endregion

    #region Export

    /// <summary>
    /// Exports the Release Planning Board to PNG or PDF.
    /// </summary>
    [HttpPost("export")]
    public async Task<ActionResult<ExportResultDto>> ExportBoard(
        [FromBody] ExportOptionsDto options,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Exporting Release Planning Board as {Format}", options.Format);

            // Get the board state
            var board = await _mediator.Send(new GetReleasePlanningBoardQuery(), cancellationToken);

            // Generate export content
            // For now, generate a simple SVG-based export that can be converted to PNG/PDF client-side
            var svgContent = GenerateBoardSvg(board, options);

            var fileName = $"ReleasePlanningBoard_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            fileName += options.Format == ExportFormat.Png ? ".png" : ".pdf";

            // For PNG, return SVG that client can convert to PNG using canvas
            // For PDF, client can use jspdf library
            return Ok(new ExportResultDto
            {
                Success = true,
                FileContentBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svgContent)),
                FileName = fileName,
                ContentType = options.Format == ExportFormat.Png ? "image/svg+xml" : "image/svg+xml",
                PageCount = 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting Release Planning Board");
            return Ok(new ExportResultDto
            {
                Success = false,
                ErrorMessage = $"Error exporting board: {ex.Message}"
            });
        }
    }

    private static string GenerateBoardSvg(ReleasePlanningBoardDto board, ExportOptionsDto options)
    {
        var laneWidth = 200;
        var rowHeight = 80;
        var headerHeight = 50;
        var cardWidth = 180;
        var cardHeight = 60;
        var padding = 20;

        var width = (board.Lanes.Count * laneWidth) + (padding * 2);
        var height = ((board.MaxRowIndex + 1) * rowHeight) + headerHeight + (padding * 2);

        var paperWidth = options.PaperSize == PaperSize.A4 ? 297 : 420; // mm in landscape
        var paperHeight = options.PaperSize == PaperSize.A4 ? 210 : 297;

        // Scale to fit if needed
        var scale = 1.0;
        if (options.Layout == ExportLayout.FitToPage)
        {
            var scaleX = (paperWidth * 3.78) / width; // 3.78 px per mm at 96 DPI
            var scaleY = (paperHeight * 3.78) / height;
            scale = Math.Min(scaleX, scaleY);
            if (scale > 1) scale = 1;
        }

        var svg = new System.Text.StringBuilder();
        svg.AppendLine($@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""{width * scale}"" height=""{height * scale}"" viewBox=""0 0 {width} {height}"">");
        svg.AppendLine(@"<style>");
        svg.AppendLine(@"  .title { font-family: Arial, sans-serif; font-size: 16px; font-weight: bold; }");
        svg.AppendLine(@"  .lane-header { font-family: Arial, sans-serif; font-size: 12px; font-weight: bold; }");
        svg.AppendLine(@"  .epic-title { font-family: Arial, sans-serif; font-size: 10px; }");
        svg.AppendLine(@"  .effort { font-family: Arial, sans-serif; font-size: 9px; fill: #666; }");
        svg.AppendLine(@"  .milestone-line { stroke: #1976d2; stroke-width: 2; }");
        svg.AppendLine(@"  .iteration-line { stroke: #9c27b0; stroke-width: 1; stroke-dasharray: 5,5; }");
        svg.AppendLine(@"  .connector { stroke: #2196f3; stroke-width: 1.5; fill: none; }");
        svg.AppendLine(@"</style>");

        // Background
        svg.AppendLine($@"<rect x=""0"" y=""0"" width=""{width}"" height=""{height}"" fill=""#ffffff""/>");

        // Title
        svg.AppendLine($@"<text x=""{width / 2}"" y=""{padding}"" class=""title"" text-anchor=""middle"">{System.Web.HttpUtility.HtmlEncode(options.Title)}</text>");

        // Lane headers
        for (int i = 0; i < board.Lanes.Count; i++)
        {
            var lane = board.Lanes[i];
            var x = padding + (i * laneWidth) + (laneWidth / 2);
            svg.AppendLine($@"<text x=""{x}"" y=""{padding + 30}"" class=""lane-header"" text-anchor=""middle"">{System.Web.HttpUtility.HtmlEncode(lane.ObjectiveTitle)}</text>");

            // Lane divider
            if (i < board.Lanes.Count - 1)
            {
                var dividerX = padding + ((i + 1) * laneWidth);
                svg.AppendLine($@"<line x1=""{dividerX}"" y1=""{headerHeight}"" x2=""{dividerX}"" y2=""{height - padding}"" stroke=""#e0e0e0"" stroke-width=""1""/>");
            }
        }

        // Row backgrounds
        for (int row = 0; row <= board.MaxRowIndex; row++)
        {
            var y = headerHeight + (row * rowHeight);
            var bgColor = row % 2 == 0 ? "#fafafa" : "#ffffff";
            svg.AppendLine($@"<rect x=""{padding}"" y=""{y}"" width=""{width - padding * 2}"" height=""{rowHeight}"" fill=""{bgColor}""/>");
        }

        // Milestone lines
        if (options.IncludeMilestoneLines)
        {
            foreach (var line in board.MilestoneLines)
            {
                var y = headerHeight + (line.VerticalPosition * rowHeight);
                svg.AppendLine($@"<line x1=""{padding}"" y1=""{y}"" x2=""{width - padding}"" y2=""{y}"" class=""milestone-line""/>");
                svg.AppendLine($@"<text x=""{padding + 5}"" y=""{y - 3}"" class=""effort"">{System.Web.HttpUtility.HtmlEncode(line.Label)}</text>");
            }
        }

        // Iteration lines
        if (options.IncludeIterationLines)
        {
            foreach (var line in board.IterationLines)
            {
                var y = headerHeight + (line.VerticalPosition * rowHeight);
                svg.AppendLine($@"<line x1=""{padding}"" y1=""{y}"" x2=""{width - padding}"" y2=""{y}"" class=""iteration-line""/>");
                svg.AppendLine($@"<text x=""{padding + 5}"" y=""{y - 3}"" class=""effort"">{System.Web.HttpUtility.HtmlEncode(line.Label)}</text>");
            }
        }

        // Connectors
        foreach (var connector in board.Connectors)
        {
            var source = board.Placements.FirstOrDefault(p => p.Id == connector.SourcePlacementId);
            var target = board.Placements.FirstOrDefault(p => p.Id == connector.TargetPlacementId);

            if (source != null && target != null)
            {
                var sourceLaneIndex = board.Lanes.ToList().FindIndex(l => l.Id == source.LaneId);
                var targetLaneIndex = board.Lanes.ToList().FindIndex(l => l.Id == target.LaneId);

                var x1 = padding + (sourceLaneIndex * laneWidth) + (laneWidth / 2);
                var y1 = headerHeight + (source.RowIndex * rowHeight) + cardHeight;
                var x2 = padding + (targetLaneIndex * laneWidth) + (laneWidth / 2);
                var y2 = headerHeight + (target.RowIndex * rowHeight) + 10;

                if (x1 == x2)
                {
                    svg.AppendLine($@"<line x1=""{x1}"" y1=""{y1}"" x2=""{x2}"" y2=""{y2}"" class=""connector""/>");
                }
                else
                {
                    var midY = (y1 + y2) / 2;
                    svg.AppendLine($@"<path d=""M {x1} {y1} C {x1} {midY}, {x2} {midY}, {x2} {y2}"" class=""connector""/>");
                }
            }
        }

        // Epic cards
        foreach (var placement in board.Placements)
        {
            var laneIndex = board.Lanes.ToList().FindIndex(l => l.Id == placement.LaneId);
            if (laneIndex < 0) continue;

            var x = padding + (laneIndex * laneWidth) + ((laneWidth - cardWidth) / 2);
            var y = headerHeight + (placement.RowIndex * rowHeight) + 10;

            // Card background with validation indicator color
            var borderColor = placement.ValidationIndicator switch
            {
                ValidationIndicator.Error => "#f44336",
                ValidationIndicator.Warning => "#ff9800",
                _ => "#4caf50"
            };

            svg.AppendLine($@"<rect x=""{x}"" y=""{y}"" width=""{cardWidth}"" height=""{cardHeight}"" rx=""4"" fill=""white"" stroke=""{borderColor}"" stroke-width=""2""/>");

            // Epic title (truncated)
            var title = placement.EpicTitle.Length > 25 ? placement.EpicTitle[..22] + "..." : placement.EpicTitle;
            svg.AppendLine($@"<text x=""{x + 8}"" y=""{y + 20}"" class=""epic-title"">{System.Web.HttpUtility.HtmlEncode(title)}</text>");

            // Effort
            if (placement.Effort.HasValue)
            {
                svg.AppendLine($@"<text x=""{x + 8}"" y=""{y + 38}"" class=""effort"">{placement.Effort} pts</text>");
            }

            // State
            svg.AppendLine($@"<text x=""{x + cardWidth - 8}"" y=""{y + 38}"" class=""effort"" text-anchor=""end"">{System.Web.HttpUtility.HtmlEncode(placement.State)}</text>");
        }

        // Footer with export date
        svg.AppendLine($@"<text x=""{width - padding}"" y=""{height - 5}"" class=""effort"" text-anchor=""end"">Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</text>");

        svg.AppendLine("</svg>");

        return svg.ToString();
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

/// <summary>
/// Request model for splitting an Epic.
/// </summary>
public record SplitEpicRequest(string ExtractedEpicTitle, IReadOnlyList<int> FeatureIdsForExtractedEpic);
