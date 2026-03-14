using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Services;
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
/// </summary>
public sealed class GetEpicCompletionForecastQueryHandler
    : IQueryHandler<GetEpicCompletionForecastQuery, EpicCompletionForecastDto?>
{
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly IHierarchyRollupService _hierarchyRollupService;
    private readonly ILogger<GetEpicCompletionForecastQueryHandler> _logger;

    public GetEpicCompletionForecastQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        IWorkItemStateClassificationService stateClassificationService,
        IHierarchyRollupService hierarchyRollupService,
        ILogger<GetEpicCompletionForecastQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _stateClassificationService = stateClassificationService;
        _hierarchyRollupService = hierarchyRollupService;
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

        var totalEffort = scope.Total;
        var completedEffort = scope.Completed;

        var remainingEffort = totalEffort - completedEffort;

        // Compute velocity inline from the distinct iteration paths of the epic's work items.
        // Avoids a dependency on a separate velocity query handler.
        var sprintMetricsList = await GetVelocitySprintsAsync(
            workItemsList,
            epic.AreaPath,
            query.MaxSprintsForVelocity ?? 5,
            cancellationToken);

        var estimatedVelocity = sprintMetricsList.Count > 0
            ? sprintMetricsList.Average(s => (double)s.CompletedStoryPoints)
            : 0.0;

        // Calculate forecast
        var sprintsRemaining = estimatedVelocity > 0
            ? (int)Math.Ceiling(remainingEffort / estimatedVelocity)
            : 0;

        // Determine confidence based on data availability
        var confidence = DetermineConfidence(sprintMetricsList.Count);

        // Build sprint-by-sprint forecast
        var forecastByDate = BuildSprintForecast(
            sprintMetricsList,
            remainingEffort,
            estimatedVelocity);

        // Estimate completion date based on last sprint end date + remaining sprints
        DateTimeOffset? estimatedCompletionDate = null;
        if (sprintMetricsList.Any() && sprintsRemaining > 0)
        {
            var lastSprint = sprintMetricsList.OrderBy(s => s.EndDate).Last();
            // Assume 2-week sprints (14 days)
            if (lastSprint.EndDate.HasValue)
            {
                estimatedCompletionDate = lastSprint.EndDate.Value.AddDays(sprintsRemaining * 14);
            }
        }

        return new EpicCompletionForecastDto(
            EpicId: epic.TfsId,
            Title: epic.Title,
            Type: epic.Type,
            TotalEffort: totalEffort,
            CompletedEffort: completedEffort,
            RemainingEffort: remainingEffort,
            EstimatedVelocity: estimatedVelocity,
            SprintsRemaining: sprintsRemaining,
            EstimatedCompletionDate: estimatedCompletionDate,
            Confidence: confidence,
            ForecastByDate: forecastByDate,
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

            var metrics = await _mediator.Send(new GetSprintMetricsQuery(path), cancellationToken);
            if (metrics == null) continue;

            // Skip sprints outside the 6-month window
            if (metrics.EndDate.HasValue && metrics.EndDate < sixMonthsAgo) continue;

            results.Add(metrics);
        }

        return results;
    }

    private static ForecastConfidence DetermineConfidence(int sprintCount)
    {
        if (sprintCount < 3)
            return ForecastConfidence.Low;

        if (sprintCount < 5)
            return ForecastConfidence.Medium;

        return ForecastConfidence.High;
    }

    private static List<SprintForecast> BuildSprintForecast(
        IReadOnlyList<SprintMetricsDto> historicalSprints,
        double remainingEffort,
        double estimatedVelocity)
    {
        var forecasts = new List<SprintForecast>();

        if (!historicalSprints.Any() || estimatedVelocity <= 0)
            return forecasts;

        var lastSprint = historicalSprints.OrderBy(s => s.EndDate).Last();
        if (!lastSprint.EndDate.HasValue)
            return forecasts;

        var currentRemaining = remainingEffort;
        var sprintNumber = 1;

        while (currentRemaining > 0 && sprintNumber <= 20) // Cap at 20 sprints
        {
            var sprintStart = lastSprint.EndDate.Value.AddDays((sprintNumber - 1) * 14);
            var sprintEnd = sprintStart.AddDays(14);

            var expectedCompleted = Math.Min(currentRemaining, estimatedVelocity);
            currentRemaining = Math.Max(0d, currentRemaining - expectedCompleted);

            var progressPercentage = remainingEffort > 0
                ? ((double)(remainingEffort - currentRemaining) / remainingEffort) * 100
                : 100;

            forecasts.Add(new SprintForecast(
                SprintName: $"Sprint +{sprintNumber}",
                IterationPath: $"Forecast/{sprintNumber}",
                SprintStartDate: sprintStart,
                SprintEndDate: sprintEnd,
                ExpectedCompletedEffort: expectedCompleted,
                RemainingEffortAfterSprint: currentRemaining,
                ProgressPercentage: progressPercentage
            ));

            sprintNumber++;
        }

        return forecasts;
    }
}
