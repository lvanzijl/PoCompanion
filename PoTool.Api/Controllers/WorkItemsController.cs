using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for work item operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkItemsController : ControllerBase
{
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<WorkItemsController> _logger;

    public WorkItemsController(
        IWorkItemRepository repository,
        ILogger<WorkItemsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all cached work items.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var workItems = await _repository.GetAllAsync(cancellationToken);
            return Ok(workItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work items");
            return StatusCode(500, "Error retrieving work items");
        }
    }

    /// <summary>
    /// Gets work items matching a filter.
    /// </summary>
    [HttpGet("filter/{filter}")]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetFiltered(
        string filter,
        CancellationToken cancellationToken)
    {
        try
        {
            var workItems = await _repository.GetFilteredAsync(filter, cancellationToken);
            return Ok(workItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering work items with filter: {Filter}", filter);
            return StatusCode(500, "Error filtering work items");
        }
    }

    /// <summary>
    /// Gets a specific work item by its TFS ID.
    /// </summary>
    [HttpGet("{tfsId:int}")]
    public async Task<ActionResult<WorkItemDto>> GetByTfsId(
        int tfsId,
        CancellationToken cancellationToken)
    {
        try
        {
            var workItem = await _repository.GetByTfsIdAsync(tfsId, cancellationToken);
            
            if (workItem == null)
            {
                return NotFound();
            }

            return Ok(workItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work item with TFS ID: {TfsId}", tfsId);
            return StatusCode(500, "Error retrieving work item");
        }
    }
}
