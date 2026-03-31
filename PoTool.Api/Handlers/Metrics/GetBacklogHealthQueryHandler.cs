using Mediator;
using PoTool.Api.Services;
using PoTool.Core.BacklogQuality;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetBacklogHealthQuery.
/// Calculates backlog health metrics for a specific iteration.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetBacklogHealthQueryHandler
    : IQueryHandler<GetBacklogHealthQuery, BacklogHealthDto?>
{
    private readonly SprintScopedWorkItemLoader _workItemLoader;
    private readonly IBacklogQualityAnalysisService _backlogQualityAnalysisService;
    private readonly ILogger<GetBacklogHealthQueryHandler> _logger;

    public GetBacklogHealthQueryHandler(
        SprintScopedWorkItemLoader workItemLoader,
        IBacklogQualityAnalysisService backlogQualityAnalysisService,
        ILogger<GetBacklogHealthQueryHandler> logger)
    {
        _workItemLoader = workItemLoader;
        _backlogQualityAnalysisService = backlogQualityAnalysisService;
        _logger = logger;
    }

    public async ValueTask<BacklogHealthDto?> Handle(
        GetBacklogHealthQuery query,
        CancellationToken cancellationToken)
    {
        var iterationPath = query.EffectiveFilter.IterationPath;
        if (string.IsNullOrWhiteSpace(iterationPath))
        {
            return null;
        }

        _logger.LogDebug("Handling GetBacklogHealthQuery for iteration: {IterationPath}", iterationPath);

        var allWorkItems = await _workItemLoader.LoadAsync(query.EffectiveFilter, cancellationToken);

        var iterationWorkItems = allWorkItems
            .Where(wi => wi.IterationPath.Equals(iterationPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!iterationWorkItems.Any())
        {
            _logger.LogDebug("No work items found for iteration: {IterationPath}", iterationPath);
            return null;
        }

        var analysis = await _backlogQualityAnalysisService.AnalyzeAsync(iterationWorkItems, cancellationToken);
        var (startDate, endDate) = ExtractSprintDates(iterationWorkItems);

        return BacklogHealthDtoFactory.Create(
            iterationPath,
            iterationWorkItems,
            analysis,
            startDate,
            endDate);
    }

    private static (DateTimeOffset?, DateTimeOffset?) ExtractSprintDates(List<WorkItemDto> workItems)
    {
        // For now, return null - in future, could parse from JsonPayload
        // or maintain separate sprint metadata
        return (null, null);
    }
}
