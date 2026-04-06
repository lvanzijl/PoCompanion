using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingLookupService
{
    Task<OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>> GetProjectsAsync(string? query, int top, int skip, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> GetTeamsAsync(string projectExternalId, string? query, int top, int skip, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> GetPipelinesAsync(string projectExternalId, string? query, int top, int skip, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>> SearchWorkItemsAsync(string? query, string? projectExternalId, IReadOnlyCollection<string>? workItemTypes, int top, int skip, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<WorkItemLookupResultDto>> GetWorkItemAsync(string workItemExternalId, CancellationToken cancellationToken);
}

public sealed class OnboardingLookupService : IOnboardingLookupService
{
    private readonly PoToolDbContext _dbContext;
    private readonly IOnboardingLiveLookupClient _liveLookupClient;

    public OnboardingLookupService(PoToolDbContext dbContext, IOnboardingLiveLookupClient liveLookupClient)
    {
        _dbContext = dbContext;
        _liveLookupClient = liveLookupClient;
    }

    public Task<OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>> GetProjectsAsync(string? query, int top, int skip, CancellationToken cancellationToken)
        => ExecuteWithConnectionAsync(connection => _liveLookupClient.GetProjectsAsync(connection, query, top, skip, cancellationToken), cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> GetTeamsAsync(string projectExternalId, string? query, int top, int skip, CancellationToken cancellationToken)
        => ExecuteWithConnectionAsync(connection => _liveLookupClient.GetTeamsAsync(connection, projectExternalId, query, top, skip, cancellationToken), cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> GetPipelinesAsync(string projectExternalId, string? query, int top, int skip, CancellationToken cancellationToken)
        => ExecuteWithConnectionAsync(connection => _liveLookupClient.GetPipelinesAsync(connection, projectExternalId, query, top, skip, cancellationToken), cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>> SearchWorkItemsAsync(string? query, string? projectExternalId, IReadOnlyCollection<string>? workItemTypes, int top, int skip, CancellationToken cancellationToken)
        => ExecuteWithConnectionAsync(connection => _liveLookupClient.SearchWorkItemsAsync(connection, query, projectExternalId, workItemTypes, top, skip, cancellationToken), cancellationToken);

    public Task<OnboardingOperationResult<WorkItemLookupResultDto>> GetWorkItemAsync(string workItemExternalId, CancellationToken cancellationToken)
        => ExecuteWithConnectionAsync(connection => _liveLookupClient.GetWorkItemAsync(connection, workItemExternalId, cancellationToken), cancellationToken);

    private async Task<OnboardingOperationResult<T>> ExecuteWithConnectionAsync<T>(
        Func<PoTool.Api.Persistence.Entities.Onboarding.TfsConnection, Task<OnboardingOperationResult<T>>> action,
        CancellationToken cancellationToken)
    {
        var connection = await OnboardingReadQueries.ActiveConnections(_dbContext)
            .OrderBy(item => item.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (connection is null)
        {
            return OnboardingOperationResult<T>.Failure(new OnboardingErrorDto(
                OnboardingErrorCode.NotFound,
                "No onboarding connection is configured.",
                "Onboarding connection",
                false));
        }

        return await action(connection);
    }
}
