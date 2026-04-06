using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Handlers.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingCrudController : ControllerBase
{
    private readonly IOnboardingCrudHandler _handler;

    public OnboardingCrudController(IOnboardingCrudHandler handler)
    {
        _handler = handler;
    }

    [HttpGet("connections")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<IReadOnlyList<OnboardingTfsConnectionDto>>>> ListConnections([FromQuery] OnboardingConfigurationStatus? status, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.ListConnectionsAsync(status, cancellationToken));

    [HttpGet("connections/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingTfsConnectionDto>>> GetConnection(int id, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.GetConnectionAsync(id, cancellationToken));

    [HttpPost("connections")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingTfsConnectionDto>>> CreateConnection([FromBody] CreateTfsConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _handler.CreateConnectionAsync(request, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetConnection), new { id = result.Data!.Id }, new OnboardingSuccessEnvelope<OnboardingTfsConnectionDto>(result.Data!, DateTime.UtcNow))
            : OnboardingApiResultMapper.ToActionResult(this, result);
    }

    [HttpPut("connections/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingTfsConnectionDto>>> UpdateConnection(int id, [FromBody] UpdateTfsConnectionRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.UpdateConnectionAsync(id, request, cancellationToken));

    [HttpDelete("connections/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingSoftDeleteResultDto>>> DeleteConnection(int id, [FromBody] OnboardingSoftDeleteRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.DeleteConnectionAsync(id, request, cancellationToken));

    [HttpGet("projects")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<IReadOnlyList<OnboardingProjectSourceDto>>>> ListProjects([FromQuery] int? connectionId, [FromQuery] OnboardingConfigurationStatus? status, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.ListProjectsAsync(connectionId, status, cancellationToken));

    [HttpGet("projects/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingProjectSourceDto>>> GetProject(int id, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.GetProjectAsync(id, cancellationToken));

    [HttpPost("projects")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingProjectSourceDto>>> CreateProject([FromBody] CreateProjectSourceRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _handler.CreateProjectAsync(request, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetProject), new { id = result.Data!.Id }, new OnboardingSuccessEnvelope<OnboardingProjectSourceDto>(result.Data!, DateTime.UtcNow))
            : OnboardingApiResultMapper.ToActionResult(this, result);
    }

    [HttpPut("projects/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingProjectSourceDto>>> UpdateProject(int id, [FromBody] UpdateProjectSourceRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.UpdateProjectAsync(id, request, cancellationToken));

    [HttpDelete("projects/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingSoftDeleteResultDto>>> DeleteProject(int id, [FromBody] OnboardingSoftDeleteRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.DeleteProjectAsync(id, request, cancellationToken));

    [HttpGet("teams")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<IReadOnlyList<OnboardingTeamSourceDto>>>> ListTeams([FromQuery] int? connectionId, [FromQuery] int? projectId, [FromQuery] OnboardingConfigurationStatus? status, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.ListTeamsAsync(connectionId, projectId, status, cancellationToken));

    [HttpGet("teams/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingTeamSourceDto>>> GetTeam(int id, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.GetTeamAsync(id, cancellationToken));

    [HttpPost("teams")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingTeamSourceDto>>> CreateTeam([FromBody] CreateTeamSourceRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _handler.CreateTeamAsync(request, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetTeam), new { id = result.Data!.Id }, new OnboardingSuccessEnvelope<OnboardingTeamSourceDto>(result.Data!, DateTime.UtcNow))
            : OnboardingApiResultMapper.ToActionResult(this, result);
    }

    [HttpPut("teams/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingTeamSourceDto>>> UpdateTeam(int id, [FromBody] UpdateTeamSourceRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.UpdateTeamAsync(id, request, cancellationToken));

    [HttpDelete("teams/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingSoftDeleteResultDto>>> DeleteTeam(int id, [FromBody] OnboardingSoftDeleteRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.DeleteTeamAsync(id, request, cancellationToken));

    [HttpGet("pipelines")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<IReadOnlyList<OnboardingPipelineSourceDto>>>> ListPipelines([FromQuery] int? connectionId, [FromQuery] int? projectId, [FromQuery] OnboardingConfigurationStatus? status, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.ListPipelinesAsync(connectionId, projectId, status, cancellationToken));

    [HttpGet("pipelines/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingPipelineSourceDto>>> GetPipeline(int id, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.GetPipelineAsync(id, cancellationToken));

    [HttpPost("pipelines")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingPipelineSourceDto>>> CreatePipeline([FromBody] CreatePipelineSourceRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _handler.CreatePipelineAsync(request, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetPipeline), new { id = result.Data!.Id }, new OnboardingSuccessEnvelope<OnboardingPipelineSourceDto>(result.Data!, DateTime.UtcNow))
            : OnboardingApiResultMapper.ToActionResult(this, result);
    }

    [HttpPut("pipelines/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingPipelineSourceDto>>> UpdatePipeline(int id, [FromBody] UpdatePipelineSourceRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.UpdatePipelineAsync(id, request, cancellationToken));

    [HttpDelete("pipelines/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingSoftDeleteResultDto>>> DeletePipeline(int id, [FromBody] OnboardingSoftDeleteRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.DeletePipelineAsync(id, request, cancellationToken));

    [HttpGet("roots")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<IReadOnlyList<OnboardingProductRootDto>>>> ListRoots([FromQuery] int? connectionId, [FromQuery] int? projectId, [FromQuery] OnboardingConfigurationStatus? status, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.ListRootsAsync(connectionId, projectId, status, cancellationToken));

    [HttpGet("roots/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingProductRootDto>>> GetRoot(int id, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.GetRootAsync(id, cancellationToken));

    [HttpPost("roots")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingProductRootDto>>> CreateRoot([FromBody] CreateProductRootRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _handler.CreateRootAsync(request, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetRoot), new { id = result.Data!.Id }, new OnboardingSuccessEnvelope<OnboardingProductRootDto>(result.Data!, DateTime.UtcNow))
            : OnboardingApiResultMapper.ToActionResult(this, result);
    }

    [HttpPut("roots/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingProductRootDto>>> UpdateRoot(int id, [FromBody] UpdateProductRootRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.UpdateRootAsync(id, request, cancellationToken));

    [HttpDelete("roots/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingSoftDeleteResultDto>>> DeleteRoot(int id, [FromBody] OnboardingSoftDeleteRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.DeleteRootAsync(id, request, cancellationToken));

    [HttpGet("bindings")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<IReadOnlyList<OnboardingProductSourceBindingDto>>>> ListBindings([FromQuery] int? connectionId, [FromQuery] int? projectId, [FromQuery] int? productRootId, [FromQuery] OnboardingConfigurationStatus? status, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.ListBindingsAsync(connectionId, projectId, productRootId, status, cancellationToken));

    [HttpGet("bindings/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingProductSourceBindingDto>>> GetBinding(int id, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.GetBindingAsync(id, cancellationToken));

    [HttpPost("bindings")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingProductSourceBindingDto>>> CreateBinding([FromBody] CreateProductSourceBindingRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _handler.CreateBindingAsync(request, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(nameof(GetBinding), new { id = result.Data!.Id }, new OnboardingSuccessEnvelope<OnboardingProductSourceBindingDto>(result.Data!, DateTime.UtcNow))
            : OnboardingApiResultMapper.ToActionResult(this, result);
    }

    [HttpPut("bindings/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingProductSourceBindingDto>>> UpdateBinding(int id, [FromBody] UpdateProductSourceBindingRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.UpdateBindingAsync(id, request, cancellationToken));

    [HttpDelete("bindings/{id:int}")]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingSoftDeleteResultDto>>> DeleteBinding(int id, [FromBody] OnboardingSoftDeleteRequest request, CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.DeleteBindingAsync(id, request, cancellationToken));
}
