using PoTool.Api.Services.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Handlers.Onboarding;

public interface IOnboardingLookupHandler
{
    Task<OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>> GetProjectsAsync(string? query, int top, int skip, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> GetTeamsAsync(string projectExternalId, string? query, int top, int skip, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> GetPipelinesAsync(string projectExternalId, string? query, int top, int skip, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>> SearchWorkItemsAsync(string? query, string? projectExternalId, IReadOnlyCollection<string>? workItemTypes, int top, int skip, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<WorkItemLookupResultDto>> GetWorkItemAsync(string workItemExternalId, CancellationToken cancellationToken);
}

public sealed class OnboardingLookupHandler : IOnboardingLookupHandler
{
    private readonly IOnboardingLookupService _lookupService;

    public OnboardingLookupHandler(IOnboardingLookupService lookupService)
    {
        _lookupService = lookupService;
    }

    public Task<OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>> GetProjectsAsync(string? query, int top, int skip, CancellationToken cancellationToken)
        => _lookupService.GetProjectsAsync(query, top, skip, cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> GetTeamsAsync(string projectExternalId, string? query, int top, int skip, CancellationToken cancellationToken)
        => _lookupService.GetTeamsAsync(projectExternalId, query, top, skip, cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> GetPipelinesAsync(string projectExternalId, string? query, int top, int skip, CancellationToken cancellationToken)
        => _lookupService.GetPipelinesAsync(projectExternalId, query, top, skip, cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>> SearchWorkItemsAsync(string? query, string? projectExternalId, IReadOnlyCollection<string>? workItemTypes, int top, int skip, CancellationToken cancellationToken)
        => _lookupService.SearchWorkItemsAsync(query, projectExternalId, workItemTypes, top, skip, cancellationToken);

    public Task<OnboardingOperationResult<WorkItemLookupResultDto>> GetWorkItemAsync(string workItemExternalId, CancellationToken cancellationToken)
        => _lookupService.GetWorkItemAsync(workItemExternalId, cancellationToken);
}
