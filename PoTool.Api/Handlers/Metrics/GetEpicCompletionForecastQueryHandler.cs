using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEpicCompletionForecastQuery.
/// Calculates completion forecast for an Epic/Feature based on historical velocity.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetEpicCompletionForecastQueryHandler
    : IQueryHandler<GetEpicCompletionForecastQuery, EpicCompletionForecastDto?>
{
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly ILogger<GetEpicCompletionForecastQueryHandler> _logger;

    public GetEpicCompletionForecastQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        IWorkItemStateClassificationService stateClassificationService,
        ILogger<GetEpicCompletionForecastQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _stateClassificationService = stateClassificationService;
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
                .Where(p => p.BacklogRootWorkItemId > 0)
                .Select(p => p.BacklogRootWorkItemId)
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

        // Get all child work items (Features, PBIs, Tasks under this Epic)
        var childItems = GetAllDescendants(epic, allWorkItems.ToList());

        // Calculate total and completed effort
        var totalEffort = childItems
            .Where(wi => wi.Effort.HasValue)
            .Sum(wi => wi.Effort!.Value);

        var completedEffort = 0;
        foreach (var wi in childItems.Where(wi => wi.Effort.HasValue))
        {
            if (await IsCompletedAsync(wi.Type, wi.State, cancellationToken))
            {
                completedEffort += wi.Effort!.Value;
            }
        }

        var remainingEffort = totalEffort - completedEffort;

        // Get velocity trend for the area path
        var velocityTrend = await _mediator.Send(
            new GetVelocityTrendQuery(ProductIds: null, AreaPath: epic.AreaPath, MaxSprints: query.MaxSprintsForVelocity ?? 5),
            cancellationToken);

        var estimatedVelocity = velocityTrend.AverageVelocity;

        // Calculate forecast
        var sprintsRemaining = estimatedVelocity > 0
            ? (int)Math.Ceiling(remainingEffort / estimatedVelocity)
            : 0;

        // Determine confidence based on data availability
        var confidence = DetermineConfidence(velocityTrend.Sprints.Count);

        // Build sprint-by-sprint forecast
        var forecastByDate = BuildSprintForecast(
            velocityTrend.Sprints,
            remainingEffort,
            estimatedVelocity);

        // Estimate completion date based on last sprint end date + remaining sprints
        DateTimeOffset? estimatedCompletionDate = null;
        if (velocityTrend.Sprints.Any() && sprintsRemaining > 0)
        {
            var lastSprint = velocityTrend.Sprints.OrderBy(s => s.EndDate).Last();
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

    private static List<WorkItemDto> GetAllDescendants(WorkItemDto parent, List<WorkItemDto> allWorkItems)
    {
        var descendants = new List<WorkItemDto>();
        var directChildren = allWorkItems.Where(wi => wi.ParentTfsId == parent.TfsId).ToList();

        foreach (var child in directChildren)
        {
            descendants.Add(child);
            descendants.AddRange(GetAllDescendants(child, allWorkItems));
        }

        return descendants;
    }

    private async Task<bool> IsCompletedAsync(string workItemType, string state, CancellationToken cancellationToken)
    {
        return await _stateClassificationService.IsDoneStateAsync(workItemType, state, cancellationToken);
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
        int remainingEffort,
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

            var expectedCompleted = Math.Min(currentRemaining, (int)Math.Round(estimatedVelocity));
            currentRemaining -= expectedCompleted;

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
