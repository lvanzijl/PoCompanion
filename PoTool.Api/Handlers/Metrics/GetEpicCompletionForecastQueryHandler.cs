using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEpicCompletionForecastQuery.
/// Calculates completion forecast for an Epic/Feature based on historical velocity.
/// </summary>
public sealed class GetEpicCompletionForecastQueryHandler 
    : IQueryHandler<GetEpicCompletionForecastQuery, EpicCompletionForecastDto?>
{
    private readonly IWorkItemRepository _repository;
    private readonly IMediator _mediator;
    private readonly ILogger<GetEpicCompletionForecastQueryHandler> _logger;

    public GetEpicCompletionForecastQueryHandler(
        IWorkItemRepository repository,
        IMediator mediator,
        ILogger<GetEpicCompletionForecastQueryHandler> logger)
    {
        _repository = repository;
        _mediator = mediator;
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

        // Get all child work items (Features, PBIs, Tasks under this Epic)
        var allWorkItems = await _repository.GetAllAsync(cancellationToken);
        var childItems = GetAllDescendants(epic, allWorkItems.ToList());

        // Calculate total and completed effort
        var totalEffort = childItems
            .Where(wi => wi.Effort.HasValue)
            .Sum(wi => wi.Effort!.Value);

        var completedEffort = childItems
            .Where(wi => wi.Effort.HasValue && IsCompleted(wi.State))
            .Sum(wi => wi.Effort!.Value);

        var remainingEffort = totalEffort - completedEffort;

        // Get velocity trend for the area path
        var velocityTrend = await _mediator.Send(
            new GetVelocityTrendQuery(epic.AreaPath, query.MaxSprintsForVelocity ?? 5),
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

    private static bool IsCompleted(string state)
    {
        return state.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
               state.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
               state.Equals("Removed", StringComparison.OrdinalIgnoreCase) ||
               state.Equals("Completed", StringComparison.OrdinalIgnoreCase);
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
