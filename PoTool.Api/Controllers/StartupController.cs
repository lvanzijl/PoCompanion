using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for startup readiness and orchestration.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StartupController : ControllerBase
{
    private readonly ITfsClient _tfsClient;

    public StartupController(ITfsClient tfsClient)
    {
        _tfsClient = tfsClient;
    }

    /// <summary>
    /// Gets all TFS projects accessible to the current user (live from TFS).
    /// Used during onboarding and TFS configuration to allow project selection.
    /// </summary>
    /// <param name="organizationUrl">The TFS/Azure DevOps organization URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("tfs-projects")]
    [ProducesResponseType(typeof(IEnumerable<TfsProjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<TfsProjectDto>>> GetTfsProjects(
        [FromQuery] string organizationUrl, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl))
        {
            return BadRequest("Organization URL is required");
        }

        var projects = await _tfsClient.GetTfsProjectsAsync(organizationUrl, cancellationToken);
        return Ok(projects);
    }

    /// <summary>
    /// Gets all TFS teams for the configured project (live from TFS).
    /// </summary>
    [HttpGet("tfs-teams")]
    [ProducesResponseType(typeof(IEnumerable<TfsTeamDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<TfsTeamDto>>> GetTfsTeams(CancellationToken cancellationToken)
    {
        var teams = await _tfsClient.GetTfsTeamsAsync(cancellationToken);
        return Ok(teams);
    }

    /// <summary>
    /// Gets all Git repositories for the configured project (live from TFS).
    /// </summary>
    [HttpGet("git-repositories")]
    [ProducesResponseType(typeof(IEnumerable<GitRepositoryInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<GitRepositoryInfoDto>>> GetGitRepositories(CancellationToken cancellationToken)
    {
        var repositories = await _tfsClient.GetGitRepositoriesAsync(cancellationToken);
        // Convert tuple to GitRepositoryInfoDto
        var result = repositories.Select(r => new GitRepositoryInfoDto(r.Name, r.Id));
        return Ok(result);
    }
}
