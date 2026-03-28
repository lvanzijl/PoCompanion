using Mediator;
using PoTool.Api.Adapters;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Filters;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Metrics.Filters;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEpicCompletionForecastQuery.
/// Calculates completion forecast for an Epic/Feature based on historical velocity.
/// Uses product-scoped hierarchical loading when products are configured.
///
/// Velocity computation: iterates the distinct iteration paths of the epic's child work
/// items, retrieves historical SprintMetrics for each (via GetSprintMetricsQuery), and derives
/// average velocity from CompletedStoryPoints. Sprints older than 6 months are excluded,
/// and the result is capped at MaxSprintsForVelocity.
/// The forecast DTO exposes canonical story-point property names and this handler
/// maps them directly from canonical story-point scope rollups.
/// </summary>
public sealed class GetEpicCompletionForecastQueryHandler
    : IQueryHandler<GetEpicCompletionForecastQuery, EpicCompletionForecastDto?>
{
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly IHierarchyRollupService _hierarchyRollupService;
    private readonly ICompletionForecastService _completionForecastService;
    private readonly ILogger<GetEpicCompletionForecastQueryHandler> _logger;

    public GetEpicCompletionForecastQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        IWorkItemStateClassificationService stateClassificationService,
        IHierarchyRollupService hierarchyRollupService,
        ICompletionForecastService completionForecastService,
        ILogger<GetEpicCompletionForecastQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _stateClassificationService = stateClassificationService;
        _hierarchyRollupService = hierarchyRollupService;
        _completionForecastService = completionForecastService;
        _logger = logger;
    }

    public async ValueTask<EpicCompletionForecastDto?> Handle(
        GetEpicCompletionForecastQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetEpicCompletionForecastQuery for Epic: {EpicId}", query.EpicId);

        // Get the Epic/Feature work item
        var epic = await _repository.GetByTfsIdAsync(query.EpicId, cancellationToken);
        if (epic == null)
        {
            _logger.LogDebug("Epic not found: {EpicId}", query.EpicId);
            return null;
        }

        // Load work items using product-scoped approach
        IEnumerable<WorkItemDto> allWorkItems;
        var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
        var productsList = allProducts.ToList();

        if (productsList.Count > 0)
        {
            var rootIds = productsList
                .SelectMany(p => p.BacklogRootWorkItemIds)
                .ToArray();

            if (rootIds.Length > 0)
            {
                var workItemsQuery = new GetWorkItemsByRootIdsQuery(rootIds);
                allWorkItems = await _mediator.Send(workItemsQuery, cancellationToken);
            }
            else
            {
                allWorkItems = await _repository.GetAllAsync(cancellationToken);
            }
        }
        else
        {
            allWorkItems = await _repository.GetAllAsync(cancellationToken);
        }

        var workItemsList = allWorkItems.ToList();
        if (workItemsList.All(wi => wi.TfsId != epic.TfsId))
        {
            workItemsList.Add(epic);
        }

        var doneByWorkItemId = await BuildDoneLookupAsync(workItemsList, cancellationToken);
        var canonicalWorkItems = workItemsList
            .Select(workItem => workItem.ToCanonicalWorkItem())
            .ToList();
        var scope = _hierarchyRollupService.RollupCanonicalScope(epic.ToCanonicalWorkItem(), canonicalWorkItems, doneByWorkItemId);

        var totalScopeStoryPoints = scope.Total;
        var completedScopeStoryPoints = scope.Completed;

        // Compute velocity inline from the distinct iteration paths of the epic's work items.
        // Avoids a dependency on a separate velocity query handler.
        var sprintMetricsList = await GetVelocitySprintsAsync(
            workItemsList,
            epic.AreaPath,
            query.MaxSprintsForVelocity ?? 5,
            cancellationToken);

        var forecast = _completionForecastService.Forecast(
            totalScopeStoryPoints,
            completedScopeStoryPoints,
            sprintMetricsList
                .Select(sprint => new HistoricalVelocitySample(
                    sprint.SprintName,
                    sprint.EndDate,
                    sprint.CompletedStoryPoints))
                .ToList());

        return new EpicCompletionForecastDto(
            EpicId: epic.TfsId,
            Title: epic.Title,
            Type: epic.Type,
            TotalStoryPoints: forecast.TotalScopeStoryPoints,
            DoneStoryPoints: forecast.CompletedScopeStoryPoints,
            RemainingStoryPoints: forecast.RemainingScopeStoryPoints,
            EstimatedVelocity: forecast.EstimatedVelocity,
            SprintsRemaining: forecast.SprintsRemaining,
            EstimatedCompletionDate: forecast.EstimatedCompletionDate,
            Confidence: MapConfidence(forecast.Confidence),
            ForecastByDate: forecast.Projections
                .Select(projection => new SprintForecast(
                    projection.SprintName,
                    projection.IterationPath,
                    projection.SprintStartDate,
                    projection.SprintEndDate,
                    projection.ExpectedCompletedStoryPoints,
                    projection.RemainingStoryPointsAfterSprint,
                    projection.ProgressPercentage))
                .ToList(),
            AreaPath: epic.AreaPath ?? "Unknown",
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private async Task<Dictionary<int, bool>> BuildDoneLookupAsync(
        IEnumerable<WorkItemDto> workItems,
        CancellationToken cancellationToken)
    {
        var doneByWorkItemId = new Dictionary<int, bool>();
        foreach (var workItem in workItems)
        {
            doneByWorkItemId[workItem.TfsId] = await IsCompletedAsync(workItem.Type, workItem.State, cancellationToken);
        }

        return doneByWorkItemId;
    }

    private async Task<bool> IsCompletedAsync(string workItemType, string state, CancellationToken cancellationToken)
    {
        return await _stateClassificationService.IsDoneStateAsync(workItemType, state, cancellationToken);
    }

    /// <summary>
    /// Derives sprint velocity data directly from work items and sprint metrics.
    /// Replaces the former dependency on GetVelocityTrendQuery.
    ///
    /// Algorithm:
    ///   1. Collect distinct iteration paths from all work items scoped to the epic's area path.
    ///   2. For each iteration path, call GetSprintMetricsQuery.
    ///   3. Exclude sprints that ended more than 6 months ago.
    ///   4. Return the most recent maxSprints results ordered by end date descending.
    /// </summary>
    private async Task<List<SprintMetricsDto>> GetVelocitySprintsAsync(
        List<WorkItemDto> allWorkItems,
        string? areaPath,
        int maxSprints,
        CancellationToken cancellationToken)
    {
        var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-6);

        // Scope iteration paths to the epic's area path when available
        var iterationPaths = allWorkItems
            .Where(wi => string.IsNullOrWhiteSpace(areaPath)
                         || wi.AreaPath.StartsWith(areaPath, StringComparison.OrdinalIgnoreCase))
            .Select(wi => wi.IterationPath)
            .Distinct()
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .OrderByDescending(p => p)   // simple lexicographic ordering; refine if sprint naming is non-sortable
            .Take(maxSprints * 2)        // over-fetch to allow 6-month filtering below
            .ToList();

        var results = new List<SprintMetricsDto>(maxSprints);

        foreach (var path in iterationPaths)
        {
            if (results.Count >= maxSprints) break;

            var metrics = await _mediator.Send(
                new GetSprintMetricsQuery(
                    new SprintEffectiveFilter(
                        new SprintFilterContext(
                            FilterSelection<int>.All(),
                            FilterSelection<int>.All(),
                            FilterSelection<string>.All(),
                            FilterSelection<string>.Selected([path]),
                            FilterTimeSelection.None()),
                        null,
                        null,
                        null,
                        Array.Empty<int>(),
                        [path],
                        null,
                        null)),
                cancellationToken);
            if (metrics == null) continue;

            // Skip sprints outside the 6-month window
            if (metrics.EndDate.HasValue && metrics.EndDate < sixMonthsAgo) continue;

            results.Add(metrics);
        }

        return results;
    }

    private static ForecastConfidence MapConfidence(ForecastConfidenceLevel confidence)
    {
        return confidence switch
        {
            ForecastConfidenceLevel.Low => ForecastConfidence.Low,
            ForecastConfidenceLevel.Medium => ForecastConfidence.Medium,
            _ => ForecastConfidence.High
        };
    }
}
