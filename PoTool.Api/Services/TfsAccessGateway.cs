using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PoTool.Api.Configuration;
using PoTool.Api.Exceptions;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines;
using PoTool.Core.PullRequests;
using PoTool.Core.WorkItems;
using PoTool.Shared.Contracts.TfsVerification;
using PoTool.Shared.Pipelines;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Controlled gateway for all TFS access.
/// Applies DataSourceMode guardrails before delegating to the configured TFS client implementation.
/// </summary>
public interface ITfsAccessGateway : ITfsClient
{
}

internal enum TfsAccessPurpose
{
    Read,
    Mutation,
    Verification
}

public sealed class TfsAccessGateway : ITfsAccessGateway
{
    private readonly ITfsClient _innerClient;
    private readonly TfsRuntimeMode _runtimeMode;
    private readonly IDataSourceModeProvider _modeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TfsAccessGateway> _logger;

    public TfsAccessGateway(
        ITfsClient innerClient,
        TfsRuntimeMode runtimeMode,
        IDataSourceModeProvider modeProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TfsAccessGateway> logger)
    {
        if (innerClient is ITfsAccessGateway)
        {
            throw new InvalidOperationException("TfsAccessGateway cannot wrap another gateway instance.");
        }

        _innerClient = innerClient;
        _runtimeMode = runtimeMode;
        _modeProvider = modeProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;

        TfsRuntimeModeGuard.EnsureExpectedClient(runtimeMode, innerClient, logger, nameof(TfsAccessGateway));
    }

    public bool UsesMockClient => _innerClient is MockTfsClient or MockData.BattleshipMockDataFacade;

    public string InnerClientTypeName => _innerClient.GetType().FullName ?? _innerClient.GetType().Name;

    private Task<T> ExecuteAsync<T>(Func<ITfsClient, Task<T>> operation, TfsAccessPurpose purpose, string method)
    {
        ValidateAccess(purpose, method);
        return operation(_innerClient);
    }

    private void ValidateAccess(TfsAccessPurpose purpose, string method)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var route = httpContext?.Request.Path.Value ?? "<background>";

        if (httpContext != null && purpose == TfsAccessPurpose.Read && _modeProvider.Mode == DataSourceMode.Cache)
        {
            _logger.LogError(
                "[Violation] Route={Route} Mode=CacheOnly AttemptedProvider=Live Action=Blocked Method={Method}",
                route,
                method);
            throw new InvalidDataSourceUsageException(route, "CacheOnly", "Live");
        }

        _logger.LogDebug(
            "[TfsAccess] Route={Route} Purpose={Purpose} Runtime={Runtime} Client={Client} Method={Method}",
            route,
            purpose,
            _runtimeMode.Name,
            InnerClientTypeName,
            method);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetWorkItemsAsync(areaPath, cancellationToken), TfsAccessPurpose.Read, nameof(GetWorkItemsAsync));

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsByTypeAsync(string workItemType, string areaPath, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetWorkItemsByTypeAsync(workItemType, areaPath, cancellationToken), TfsAccessPurpose.Read, nameof(GetWorkItemsByTypeAsync));

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, DateTimeOffset? since, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetWorkItemsAsync(areaPath, since, cancellationToken), TfsAccessPurpose.Read, nameof(GetWorkItemsAsync));

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsAsync(int[] rootWorkItemIds, DateTimeOffset? since = null, Action<int, int, string>? progressCallback = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetWorkItemsByRootIdsAsync(rootWorkItemIds, since, progressCallback, cancellationToken), TfsAccessPurpose.Read, nameof(GetWorkItemsByRootIdsAsync));

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsWithDetailedProgressAsync(int[] rootWorkItemIds, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetWorkItemsByRootIdsWithDetailedProgressAsync(rootWorkItemIds, since, cancellationToken), TfsAccessPurpose.Read, nameof(GetWorkItemsByRootIdsWithDetailedProgressAsync));

    public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ValidateConnectionAsync(cancellationToken), TfsAccessPurpose.Verification, nameof(ValidateConnectionAsync));

    public Task<IEnumerable<string>> GetAreaPathsAsync(int? depth = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetAreaPathsAsync(depth, cancellationToken), TfsAccessPurpose.Read, nameof(GetAreaPathsAsync));

    public Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(string? repositoryName = null, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetPullRequestsAsync(repositoryName, fromDate, toDate, cancellationToken), TfsAccessPurpose.Read, nameof(GetPullRequestsAsync));

    public Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetPullRequestIterationsAsync(pullRequestId, repositoryName, cancellationToken), TfsAccessPurpose.Read, nameof(GetPullRequestIterationsAsync));

    public Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetPullRequestCommentsAsync(pullRequestId, repositoryName, cancellationToken), TfsAccessPurpose.Read, nameof(GetPullRequestCommentsAsync));

    public Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(int pullRequestId, string repositoryName, int iterationId, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetPullRequestFileChangesAsync(pullRequestId, repositoryName, iterationId, cancellationToken), TfsAccessPurpose.Read, nameof(GetPullRequestFileChangesAsync));

    public Task<IEnumerable<int>> GetPullRequestWorkItemLinksAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetPullRequestWorkItemLinksAsync(pullRequestId, repositoryName, cancellationToken), TfsAccessPurpose.Read, nameof(GetPullRequestWorkItemLinksAsync));

    public Task<WorkItemDto?> GetWorkItemByIdAsync(int workItemId, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetWorkItemByIdAsync(workItemId, cancellationToken), TfsAccessPurpose.Read, nameof(GetWorkItemByIdAsync));

    public Task<IEnumerable<WorkItemRevisionDto>> GetWorkItemRevisionsAsync(int workItemId, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetWorkItemRevisionsAsync(workItemId, cancellationToken), TfsAccessPurpose.Read, nameof(GetWorkItemRevisionsAsync));

    public Task<IReadOnlyList<WorkItemUpdate>> GetWorkItemUpdatesAsync(int workItemId, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetWorkItemUpdatesAsync(workItemId, cancellationToken), TfsAccessPurpose.Read, nameof(GetWorkItemUpdatesAsync));

    public Task<bool> UpdateWorkItemStateAsync(int workItemId, string newState, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemStateAsync(workItemId, newState, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemStateAsync));

    public Task<bool> UpdateWorkItemEffortAsync(int workItemId, int effort, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemEffortAsync(workItemId, effort, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemEffortAsync));

    public Task<bool> UpdateWorkItemSeverityAsync(int workItemId, string severity, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemSeverityAsync(workItemId, severity, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemSeverityAsync));

    public Task<WorkItemDto?> UpdateWorkItemSeverityAndReturnAsync(int workItemId, string severity, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemSeverityAndReturnAsync(workItemId, severity, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemSeverityAndReturnAsync));

    public Task<bool> UpdateWorkItemTagsAsync(int workItemId, List<string> tags, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemTagsAsync(workItemId, tags, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemTagsAsync));

    public Task<WorkItemDto?> UpdateWorkItemTagsAndReturnAsync(int workItemId, List<string> tags, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemTagsAndReturnAsync(workItemId, tags, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemTagsAndReturnAsync));

    public Task<bool> UpdateWorkItemBacklogPriorityAsync(int workItemId, double priority, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemBacklogPriorityAsync(workItemId, priority, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemBacklogPriorityAsync));

    public Task<bool> UpdateWorkItemIterationPathAsync(int workItemId, string iterationPath, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemIterationPathAsync(workItemId, iterationPath, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemIterationPathAsync));

    public Task<WorkItemDto?> UpdateWorkItemTitleDescriptionAsync(int workItemId, string? title, string? description, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemTitleDescriptionAsync(workItemId, title, description, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemTitleDescriptionAsync));

    public Task<TfsVerificationReport> VerifyCapabilitiesAsync(bool includeWriteChecks = false, int? workItemIdForWriteCheck = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.VerifyCapabilitiesAsync(includeWriteChecks, workItemIdForWriteCheck, cancellationToken), TfsAccessPurpose.Verification, nameof(VerifyCapabilitiesAsync));

    public Task<BulkUpdateResult> UpdateWorkItemsEffortAsync(IEnumerable<WorkItemEffortUpdate> updates, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemsEffortAsync(updates, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemsEffortAsync));

    public Task<BulkUpdateResult> UpdateWorkItemsStateAsync(IEnumerable<WorkItemStateUpdate> updates, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemsStateAsync(updates, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemsStateAsync));

    public Task<IDictionary<int, IEnumerable<WorkItemRevisionDto>>> GetWorkItemRevisionsBatchAsync(IEnumerable<int> workItemIds, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetWorkItemRevisionsBatchAsync(workItemIds, cancellationToken), TfsAccessPurpose.Read, nameof(GetWorkItemRevisionsBatchAsync));

    public Task<WorkItemCreateResult> CreateWorkItemAsync(WorkItemCreateRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.CreateWorkItemAsync(request, cancellationToken), TfsAccessPurpose.Mutation, nameof(CreateWorkItemAsync));

    public Task<bool> UpdateWorkItemParentAsync(int workItemId, int newParentId, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.UpdateWorkItemParentAsync(workItemId, newParentId, cancellationToken), TfsAccessPurpose.Mutation, nameof(UpdateWorkItemParentAsync));

    public Task<IEnumerable<PipelineDto>> GetPipelinesAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetPipelinesAsync(cancellationToken), TfsAccessPurpose.Read, nameof(GetPipelinesAsync));

    public Task<PipelineDto?> GetPipelineByIdAsync(int pipelineId, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetPipelineByIdAsync(pipelineId, cancellationToken), TfsAccessPurpose.Read, nameof(GetPipelineByIdAsync));

    public Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetPipelineRunsAsync(pipelineId, top, cancellationToken), TfsAccessPurpose.Read, nameof(GetPipelineRunsAsync));

    public Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(IEnumerable<int> pipelineIds, string? branchName = null, DateTimeOffset? minStartTime = null, int top = 100, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetPipelineRunsAsync(pipelineIds, branchName, minStartTime, top, cancellationToken), TfsAccessPurpose.Read, nameof(GetPipelineRunsAsync));

    public Task<string?> GetRepositoryIdByNameAsync(string repositoryName, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetRepositoryIdByNameAsync(repositoryName, cancellationToken), TfsAccessPurpose.Read, nameof(GetRepositoryIdByNameAsync));

    public Task<IEnumerable<PipelineDefinitionDto>> GetPipelineDefinitionsForRepositoryAsync(string repositoryName, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetPipelineDefinitionsForRepositoryAsync(repositoryName, cancellationToken), TfsAccessPurpose.Read, nameof(GetPipelineDefinitionsForRepositoryAsync));

    public Task<IEnumerable<TestRunDto>> GetTestRunsByBuildIdsAsync(IEnumerable<int> buildIds, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetTestRunsByBuildIdsAsync(buildIds, cancellationToken), TfsAccessPurpose.Read, nameof(GetTestRunsByBuildIdsAsync));

    public Task<IEnumerable<CoverageDto>> GetCoverageByBuildIdsAsync(IEnumerable<int> buildIds, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetCoverageByBuildIdsAsync(buildIds, cancellationToken), TfsAccessPurpose.Read, nameof(GetCoverageByBuildIdsAsync));

    public Task<IEnumerable<TfsProjectDto>> GetTfsProjectsAsync(string organizationUrl, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetTfsProjectsAsync(organizationUrl, cancellationToken), TfsAccessPurpose.Read, nameof(GetTfsProjectsAsync));

    public Task<IEnumerable<TfsTeamDto>> GetTfsTeamsAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetTfsTeamsAsync(cancellationToken), TfsAccessPurpose.Read, nameof(GetTfsTeamsAsync));

    public Task<IEnumerable<(string Name, string Id)>> GetGitRepositoriesAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetGitRepositoriesAsync(cancellationToken), TfsAccessPurpose.Read, nameof(GetGitRepositoriesAsync));

    public Task<IEnumerable<TeamIterationDto>> GetTeamIterationsAsync(string projectName, string teamName, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetTeamIterationsAsync(projectName, teamName, cancellationToken), TfsAccessPurpose.Read, nameof(GetTeamIterationsAsync));

    public Task<IEnumerable<WorkItemTypeDefinitionDto>> GetWorkItemTypeDefinitionsAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.GetWorkItemTypeDefinitionsAsync(cancellationToken), TfsAccessPurpose.Read, nameof(GetWorkItemTypeDefinitionsAsync));
}
