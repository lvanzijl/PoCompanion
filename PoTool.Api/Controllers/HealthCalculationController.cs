using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Health;
using PoTool.Shared.Health;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for backlog health calculation operations.
/// Exposes health calculation business logic from Core layer via HTTP endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthCalculationController : ControllerBase
{
    private readonly BacklogHealthCalculator _calculator;
    private readonly ILogger<HealthCalculationController> _logger;

    public HealthCalculationController(
        BacklogHealthCalculator calculator,
        ILogger<HealthCalculationController> logger)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Calculates the health score for an iteration based on validation issues.
    /// </summary>
    /// <param name="request">Health calculation request containing issue counts.</param>
    /// <returns>Health score from 0 to 100.</returns>
    [HttpPost("calculate-score")]
    public ActionResult<CalculateHealthScoreResponse> CalculateHealthScore(
        [FromBody] CalculateHealthScoreRequest request)
    {
        try
        {
            var totalIssues = request.WorkItemsWithoutEffort
                + request.WorkItemsInProgressWithoutEffort
                + request.ParentProgressIssues
                + request.BlockedItems;

            var score = _calculator.CalculateHealthScore(
                request.TotalWorkItems,
                request.WorkItemsWithoutEffort,
                request.WorkItemsInProgressWithoutEffort,
                request.ParentProgressIssues,
                request.BlockedItems
            );

            return Ok(new CalculateHealthScoreResponse
            {
                HealthScore = score,
                TotalIssues = totalIssues
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating health score");
            return StatusCode(500, "Error calculating health score");
        }
    }
}
