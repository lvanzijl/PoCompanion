using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for project lookup and project-scoped product discovery.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets all projects.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects(CancellationToken cancellationToken)
    {
        var projects = await _mediator.Send(new GetAllProjectsQuery(), cancellationToken);
        return Ok(projects);
    }

    /// <summary>
    /// Gets a project by alias or internal identifier.
    /// </summary>
    [HttpGet("{alias}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> GetProject(string alias, CancellationToken cancellationToken)
    {
        var project = await _mediator.Send(new GetProjectByAliasQuery(alias), cancellationToken);
        if (project == null)
        {
            return NotFound();
        }

        return Ok(project);
    }

    /// <summary>
    /// Gets products that belong to a project resolved by alias or internal identifier.
    /// </summary>
    [HttpGet("{alias}/products")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProjectProducts(string alias, CancellationToken cancellationToken)
    {
        var products = await _mediator.Send(new GetProjectProductsQuery(alias), cancellationToken);
        return Ok(products);
    }
}
