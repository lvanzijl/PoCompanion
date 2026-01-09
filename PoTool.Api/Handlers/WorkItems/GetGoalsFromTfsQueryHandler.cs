using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Api.Services;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetGoalsFromTfsQuery.
/// Fetches goals directly from TFS, bypassing the cache.
/// Used specifically for the Add Profile flow where cache is not yet populated.
/// </summary>
public sealed class GetGoalsFromTfsQueryHandler : IQueryHandler<GetGoalsFromTfsQuery, IEnumerable<WorkItemDto>>
{
    private readonly ITfsClient _tfsClient;
    private readonly TfsConfigurationService _configService;
    private readonly ILogger<GetGoalsFromTfsQueryHandler> _logger;

    public GetGoalsFromTfsQueryHandler(
        ITfsClient tfsClient,
        TfsConfigurationService configService,
        ILogger<GetGoalsFromTfsQueryHandler> logger)
    {
        _tfsClient = tfsClient;
        _configService = configService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetGoalsFromTfsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching goals directly from TFS (cache bypass for Add Profile flow)");

        // Get the configured default area path from TFS config
        var config = await _configService.GetConfigEntityAsync(cancellationToken);
        
        if (config == null || string.IsNullOrWhiteSpace(config.DefaultAreaPath))
        {
            _logger.LogWarning("No TFS configuration or default area path found");
            return Enumerable.Empty<WorkItemDto>();
        }

        try
        {
            // Fetch work items from TFS using the default area path
            // Cache is intentionally bypassed here because it's not yet populated in the Add Profile flow
            var workItems = await _tfsClient.GetWorkItemsAsync(config.DefaultAreaPath, cancellationToken);
            
            // Filter to only Goal work items
            var goals = workItems
                .Where(wi => wi.Type == WorkItemType.Goal)
                .OrderBy(g => g.Title)
                .ToList();

            _logger.LogDebug("Found {Count} goals from TFS", goals.Count);
            
            return goals;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching goals from TFS");
            // Return empty list on error to prevent breaking the UI
            return Enumerable.Empty<WorkItemDto>();
        }
    }
}
