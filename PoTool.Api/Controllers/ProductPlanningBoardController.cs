using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Filters;
using PoTool.Core.Planning;
using PoTool.Shared.Planning;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for product-scoped planning board operations backed by the planning application service.
/// </summary>
[ApiController]
[Route("api/products/{productId:int}/planning-board")]
[DataSourceMode(RouteIntent.LiveAllowed)]
public sealed class ProductPlanningBoardController : ControllerBase
{
    private readonly IProductPlanningBoardService _planningBoardService;

    public ProductPlanningBoardController(IProductPlanningBoardService planningBoardService)
    {
        _planningBoardService = planningBoardService ?? throw new ArgumentNullException(nameof(planningBoardService));
    }

    /// <summary>
    /// Gets the current planning board for a product, bootstrapping the in-memory session if needed.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProductPlanningBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductPlanningBoardDto>> GetPlanningBoard(
        int productId,
        CancellationToken cancellationToken = default)
    {
        return await MapBoardResultAsync(
            () => _planningBoardService.GetPlanningBoardAsync(productId, cancellationToken));
    }

    /// <summary>
    /// Resets the in-memory planning session for a product and returns a freshly bootstrapped board.
    /// </summary>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(ProductPlanningBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductPlanningBoardDto>> ResetPlanningBoard(
        int productId,
        CancellationToken cancellationToken = default)
    {
        return await MapBoardResultAsync(
            () => _planningBoardService.ResetPlanningBoardAsync(productId, cancellationToken));
    }

    /// <summary>
    /// Moves an epic by the specified sprint delta.
    /// </summary>
    [HttpPost("move")]
    [ProducesResponseType(typeof(ProductPlanningBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductPlanningBoardDto>> MoveEpicBySprints(
        int productId,
        [FromBody] ProductPlanningEpicDeltaRequest request,
        CancellationToken cancellationToken = default)
    {
        return await MapBoardResultAsync(
            () => _planningBoardService.ExecuteMoveEpicBySprintsAsync(productId, request.EpicId, request.DeltaSprints, cancellationToken));
    }

    /// <summary>
    /// Adjusts spacing before an epic by the specified sprint delta.
    /// </summary>
    [HttpPost("adjust-spacing")]
    [ProducesResponseType(typeof(ProductPlanningBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductPlanningBoardDto>> AdjustSpacingBefore(
        int productId,
        [FromBody] ProductPlanningEpicDeltaRequest request,
        CancellationToken cancellationToken = default)
    {
        return await MapBoardResultAsync(
            () => _planningBoardService.ExecuteAdjustSpacingBeforeAsync(productId, request.EpicId, request.DeltaSprints, cancellationToken));
    }

    /// <summary>
    /// Moves an epic onto a parallel track.
    /// </summary>
    [HttpPost("run-in-parallel")]
    [ProducesResponseType(typeof(ProductPlanningBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductPlanningBoardDto>> RunInParallel(
        int productId,
        [FromBody] ProductPlanningEpicRequest request,
        CancellationToken cancellationToken = default)
    {
        return await MapBoardResultAsync(
            () => _planningBoardService.ExecuteRunInParallelAsync(productId, request.EpicId, cancellationToken));
    }

    /// <summary>
    /// Returns an epic to the main track.
    /// </summary>
    [HttpPost("return-to-main")]
    [ProducesResponseType(typeof(ProductPlanningBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductPlanningBoardDto>> ReturnToMain(
        int productId,
        [FromBody] ProductPlanningEpicRequest request,
        CancellationToken cancellationToken = default)
    {
        return await MapBoardResultAsync(
            () => _planningBoardService.ExecuteReturnToMainAsync(productId, request.EpicId, cancellationToken));
    }

    /// <summary>
    /// Reorders an epic within the roadmap sequence.
    /// </summary>
    [HttpPost("reorder")]
    [ProducesResponseType(typeof(ProductPlanningBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductPlanningBoardDto>> ReorderEpic(
        int productId,
        [FromBody] ReorderProductPlanningEpicRequest request,
        CancellationToken cancellationToken = default)
    {
        return await MapBoardResultAsync(
            () => _planningBoardService.ExecuteReorderEpicAsync(productId, request.EpicId, request.TargetRoadmapOrder, cancellationToken));
    }

    /// <summary>
    /// Shifts the requested plan suffix by the specified sprint delta.
    /// </summary>
    [HttpPost("shift-plan")]
    [ProducesResponseType(typeof(ProductPlanningBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductPlanningBoardDto>> ShiftPlan(
        int productId,
        [FromBody] ProductPlanningEpicDeltaRequest request,
        CancellationToken cancellationToken = default)
    {
        return await MapBoardResultAsync(
            () => _planningBoardService.ExecuteShiftPlanAsync(productId, request.EpicId, request.DeltaSprints, cancellationToken));
    }

    private async Task<ActionResult<ProductPlanningBoardDto>> MapBoardResultAsync(
        Func<ValueTask<ProductPlanningBoardDto?>> action)
    {
        var board = await action();
        if (board is null)
        {
            return NotFound();
        }

        return Ok(board);
    }
}
