using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Handlers.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Controllers;

[ApiController]
[Route("api/onboarding/lookups")]
public sealed class OnboardingLookupController : ControllerBase
{
    private readonly IOnboardingLookupHandler _handler;

    public OnboardingLookupController(IOnboardingLookupHandler handler)
    {
        _handler = handler;
    }

    [HttpGet("projects")]
    [ProducesResponseType(typeof(OnboardingSuccessEnvelope<IReadOnlyList<ProjectLookupResultDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OnboardingSuccessEnvelope<IReadOnlyList<ProjectLookupResultDto>>>> GetProjects(
        [FromQuery] string? query,
        [FromQuery] int top = 50,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        if (top <= 0 || skip < 0)
        {
            return OnboardingApiResultMapper.ToActionResult(this, OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Failure(
                new OnboardingErrorDto(OnboardingErrorCode.ValidationFailed, "Query options are invalid.", $"top={top}; skip={skip}", false)));
        }

        return OnboardingApiResultMapper.ToActionResult(this, await _handler.GetProjectsAsync(query, top, skip, cancellationToken));
    }

    [HttpGet("projects/{projectExternalId}/teams")]
    [ProducesResponseType(typeof(OnboardingSuccessEnvelope<IReadOnlyList<TeamLookupResultDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OnboardingSuccessEnvelope<IReadOnlyList<TeamLookupResultDto>>>> GetTeams(
        string projectExternalId,
        [FromQuery] string? query,
        [FromQuery] int top = 50,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        if (top <= 0 || skip < 0)
        {
            return OnboardingApiResultMapper.ToActionResult(this, OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Failure(
                new OnboardingErrorDto(OnboardingErrorCode.ValidationFailed, "Query options are invalid.", $"top={top}; skip={skip}", false)));
        }

        return OnboardingApiResultMapper.ToActionResult(this, await _handler.GetTeamsAsync(projectExternalId, query, top, skip, cancellationToken));
    }

    [HttpGet("projects/{projectExternalId}/pipelines")]
    [ProducesResponseType(typeof(OnboardingSuccessEnvelope<IReadOnlyList<PipelineLookupResultDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OnboardingSuccessEnvelope<IReadOnlyList<PipelineLookupResultDto>>>> GetPipelines(
        string projectExternalId,
        [FromQuery] string? query,
        [FromQuery] int top = 50,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        if (top <= 0 || skip < 0)
        {
            return OnboardingApiResultMapper.ToActionResult(this, OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Failure(
                new OnboardingErrorDto(OnboardingErrorCode.ValidationFailed, "Query options are invalid.", $"top={top}; skip={skip}", false)));
        }

        return OnboardingApiResultMapper.ToActionResult(this, await _handler.GetPipelinesAsync(projectExternalId, query, top, skip, cancellationToken));
    }

    [HttpGet("work-items")]
    [ProducesResponseType(typeof(OnboardingSuccessEnvelope<IReadOnlyList<WorkItemLookupResultDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OnboardingSuccessEnvelope<IReadOnlyList<WorkItemLookupResultDto>>>> SearchWorkItems(
        [FromQuery] string? query,
        [FromQuery] string? projectExternalId,
        [FromQuery(Name = "workItemTypes")] string[]? workItemTypes,
        [FromQuery] int top = 50,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        if (top <= 0 || skip < 0)
        {
            return OnboardingApiResultMapper.ToActionResult(this, OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>.Failure(
                new OnboardingErrorDto(OnboardingErrorCode.ValidationFailed, "Query options are invalid.", $"top={top}; skip={skip}", false)));
        }

        return OnboardingApiResultMapper.ToActionResult(this, await _handler.SearchWorkItemsAsync(query, projectExternalId, workItemTypes, top, skip, cancellationToken));
    }

    [HttpGet("work-items/{workItemExternalId}")]
    [ProducesResponseType(typeof(OnboardingSuccessEnvelope<WorkItemLookupResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OnboardingSuccessEnvelope<WorkItemLookupResultDto>>> GetWorkItem(
        string workItemExternalId,
        CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.GetWorkItemAsync(workItemExternalId, cancellationToken));
}
