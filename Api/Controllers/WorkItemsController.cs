using Core.Contracts;
using Core.WorkItems;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// API controller for work item operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class WorkItemsController : ControllerBase
{
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<WorkItemsController> _logger;

    public WorkItemsController(
        IWorkItemRepository repository,
        ILogger<WorkItemsController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all cached work items.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<WorkItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<WorkItemDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting all work items");
        var workItems = await _repository.GetAllAsync(cancellationToken);
        return Ok(workItems);
    }

    /// <summary>
    /// Gets a work item by its TFS ID.
    /// </summary>
    [HttpGet("{tfsId:int}")]
    [ProducesResponseType(typeof(WorkItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkItemDto>> GetById(
        int tfsId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting work item with TFS ID {TfsId}", tfsId);
        var workItem = await _repository.GetByTfsIdAsync(tfsId, cancellationToken);

        if (workItem is null)
        {
            _logger.LogWarning("Work item with TFS ID {TfsId} not found", tfsId);
            return NotFound();
        }

        return Ok(workItem);
    }

    /// <summary>
    /// Gets work items by area path.
    /// </summary>
    [HttpGet("by-area/{*areaPath}")]
    [ProducesResponseType(typeof(IReadOnlyCollection<WorkItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<WorkItemDto>>> GetByAreaPath(
        string areaPath,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting work items for area path {AreaPath}", areaPath);
        var workItems = await _repository.GetByAreaPathAsync(areaPath, cancellationToken);
        return Ok(workItems);
    }

    /// <summary>
    /// Gets the timestamp of the last cache update.
    /// </summary>
    [HttpGet("last-update")]
    [ProducesResponseType(typeof(DateTimeOffset?), StatusCodes.Status200OK)]
    public async Task<ActionResult<DateTimeOffset?>> GetLastUpdate(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting last update timestamp");
        var lastUpdate = await _repository.GetLastUpdateTimestampAsync(cancellationToken);
        return Ok(lastUpdate);
    }
}
