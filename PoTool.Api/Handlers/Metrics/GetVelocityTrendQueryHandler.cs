using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics;
using PoTool.Core.Metrics.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetVelocityTrendQuery.
/// Calculates velocity trends across multiple sprints.
/// </summary>
public sealed class GetVelocityTrendQueryHandler : IQueryHandler<GetVelocityTrendQuery, VelocityTrendDto>
{
    private readonly IWorkItemRepository _repository;
    private readonly IMediator _mediator;
    private readonly ILogger<GetVelocityTrendQueryHandler> _logger;

    public GetVelocityTrendQueryHandler(
        IWorkItemRepository repository,
        IMediator mediator,
        ILogger<GetVelocityTrendQueryHandler> logger)
    {
        _repository = repository;
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<VelocityTrendDto> Handle(
        GetVelocityTrendQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetVelocityTrendQuery for AreaPath: {AreaPath}, MaxSprints: {MaxSprints}", 
            query.AreaPath ?? "All", query.MaxSprints);

        var allWorkItems = await _repository.GetAllAsync(cancellationToken);
        
        // Filter by area path if specified
        if (!string.IsNullOrWhiteSpace(query.AreaPath))
        {
            allWorkItems = allWorkItems
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Get distinct iteration paths and sort them (most recent first based on path naming)
        var iterationPaths = allWorkItems
            .Select(wi => wi.IterationPath)
            .Distinct()
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .OrderByDescending(path => path) // Simple ordering - could be improved with sprint dates
            .Take(query.MaxSprints)
            .ToList();

        _logger.LogDebug("Found {Count} distinct iterations", iterationPaths.Count);

        // Get metrics for each sprint
        var sprintMetricsList = new List<SprintMetricsDto>();
        foreach (var iterationPath in iterationPaths)
        {
            var sprintMetrics = await _mediator.Send(
                new GetSprintMetricsQuery(iterationPath), 
                cancellationToken);
            
            if (sprintMetrics != null)
            {
                sprintMetricsList.Add(sprintMetrics);
            }
        }

        // Calculate velocity averages
        var completedPoints = sprintMetricsList
            .Select(s => s.CompletedStoryPoints)
            .ToList();

        var averageVelocity = completedPoints.Any() 
            ? Math.Round(completedPoints.Average(), 2) 
            : 0;

        var threeSprintAverage = completedPoints.Count >= 3
            ? Math.Round(completedPoints.Take(3).Average(), 2)
            : averageVelocity;

        var sixSprintAverage = completedPoints.Count >= 6
            ? Math.Round(completedPoints.Take(6).Average(), 2)
            : averageVelocity;

        var totalCompletedStoryPoints = completedPoints.Sum();

        var velocityTrend = new VelocityTrendDto(
            Sprints: sprintMetricsList.AsReadOnly(),
            AverageVelocity: averageVelocity,
            ThreeSprintAverage: threeSprintAverage,
            SixSprintAverage: sixSprintAverage,
            TotalCompletedStoryPoints: totalCompletedStoryPoints,
            TotalSprints: sprintMetricsList.Count
        );

        _logger.LogInformation(
            "Velocity trend calculated: {TotalSprints} sprints, average velocity: {AverageVelocity}",
            velocityTrend.TotalSprints, velocityTrend.AverageVelocity);

        return velocityTrend;
    }
}
