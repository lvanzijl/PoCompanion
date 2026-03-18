using Mediator;
using PoTool.Api.Configuration;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Shared.Settings;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Api.Services;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetGoalsFromTfsQuery.
/// Fetches goals from the configured TFS client, bypassing the cache.
/// Used specifically for the Add Profile flow where cache is not yet populated.
/// </summary>
    public sealed class GetGoalsFromTfsQueryHandler : IQueryHandler<GetGoalsFromTfsQuery, IEnumerable<WorkItemDto>>
    {
        private readonly ITfsClient _tfsClient;
        private readonly TfsConfigurationService _configService;
        private readonly TfsRuntimeMode _runtimeMode;
        private readonly ILogger<GetGoalsFromTfsQueryHandler> _logger;

        public GetGoalsFromTfsQueryHandler(
            ITfsClient tfsClient,
            TfsConfigurationService configService,
            TfsRuntimeMode runtimeMode,
            ILogger<GetGoalsFromTfsQueryHandler> logger)
        {
            _tfsClient = tfsClient;
            _configService = configService;
            _runtimeMode = runtimeMode;
            _logger = logger;
        }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetGoalsFromTfsQuery query,
        CancellationToken cancellationToken)
        {
            _logger.LogDebug("Fetching goals from the configured TFS client (cache bypass for Add Profile flow)");
            TfsRuntimeModeGuard.EnsureExpectedClient(_runtimeMode, _tfsClient, _logger, nameof(GetGoalsFromTfsQueryHandler));

            var config = await _configService.GetConfigEntityAsync(cancellationToken);

        if (config == null || string.IsNullOrWhiteSpace(config.DefaultAreaPath))
        {
            _logger.LogWarning("No TFS configuration or default area path found");
            return Enumerable.Empty<WorkItemDto>();
        }

        if (_runtimeMode.UseMockClient && _tfsClient is not MockTfsClient)
        {
            throw new InvalidOperationException(
                "Mock mode is enabled but the resolved TFS client is not the mock implementation. " +
                "Goal bootstrap requires MockTfsClient when TfsIntegration:UseMockClient=true.");
        }

        try
        {
            var goals = (await _tfsClient.GetWorkItemsByTypeAsync(
                    WorkItemType.Goal,
                    config.DefaultAreaPath,
                    cancellationToken))
                .ToList();

            if (goals.Count == 0)
            {
                _logger.LogInformation("No Goal work items found in TFS for area path: {AreaPath}", config.DefaultAreaPath);
                return Enumerable.Empty<WorkItemDto>();
            }

            _logger.LogInformation("Successfully fetched {Count} Goals from TFS", goals.Count);

            return goals;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Goal fetch operation was cancelled");
            return Enumerable.Empty<WorkItemDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching goals from TFS");
            return Enumerable.Empty<WorkItemDto>();
        }
    }
}
