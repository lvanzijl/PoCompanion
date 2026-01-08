using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEffortDistributionQuery.
/// Calculates effort distribution across area paths and iterations for heat map visualization.
/// </summary>
public sealed class GetEffortDistributionQueryHandler 
    : IQueryHandler<GetEffortDistributionQuery, EffortDistributionDto>
{
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<GetEffortDistributionQueryHandler> _logger;

    public GetEffortDistributionQueryHandler(
        IWorkItemRepository repository,
        ILogger<GetEffortDistributionQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<EffortDistributionDto> Handle(
        GetEffortDistributionQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetEffortDistributionQuery with AreaPathFilter: {AreaPathFilter}, MaxIterations: {MaxIterations}", 
            query.AreaPathFilter ?? "All", 
            query.MaxIterations);

        var allWorkItems = await _repository.GetAllAsync(cancellationToken);
        
        // Filter by area path if specified
        if (!string.IsNullOrWhiteSpace(query.AreaPathFilter))
        {
            allWorkItems = allWorkItems
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPathFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Filter to items with effort only
        var workItemsWithEffort = allWorkItems
            .Where(wi => wi.Effort.HasValue && wi.Effort.Value > 0)
            .ToList();

        // Get top area paths by work item count
        var areaPathsByVolume = workItemsWithEffort
            .GroupBy(wi => wi.AreaPath)
            .OrderByDescending(g => g.Count())
            .Take(10) // Limit to top 10 area paths for heat map clarity
            .Select(g => g.Key)
            .ToList();

        // Get recent iterations
        var iterationPaths = workItemsWithEffort
            .Where(wi => !string.IsNullOrWhiteSpace(wi.IterationPath))
            .Select(wi => wi.IterationPath)
            .Distinct()
            .OrderByDescending(path => path)
            .Take(query.MaxIterations)
            .ToList();

        // Calculate effort by area path
        var effortByArea = CalculateEffortByAreaPath(workItemsWithEffort, areaPathsByVolume);

        // Calculate effort by iteration
        var effortByIteration = CalculateEffortByIteration(
            workItemsWithEffort, 
            iterationPaths, 
            query.DefaultCapacityPerIteration);

        // Calculate heat map cells
        var heatMapData = CalculateHeatMapCells(
            workItemsWithEffort, 
            areaPathsByVolume, 
            iterationPaths,
            query.DefaultCapacityPerIteration);

        var totalEffort = workItemsWithEffort.Sum(wi => wi.Effort ?? 0);

        return new EffortDistributionDto(
            EffortByArea: effortByArea,
            EffortByIteration: effortByIteration,
            HeatMapData: heatMapData,
            TotalEffort: totalEffort,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private static List<EffortByAreaPath> CalculateEffortByAreaPath(
        List<WorkItemDto> workItems,
        List<string> areaPathsToInclude)
    {
        return workItems
            .Where(wi => areaPathsToInclude.Contains(wi.AreaPath))
            .GroupBy(wi => wi.AreaPath)
            .Select(group => new EffortByAreaPath(
                AreaPath: group.Key,
                TotalEffort: group.Sum(wi => wi.Effort ?? 0),
                WorkItemCount: group.Count(),
                AverageEffortPerItem: group.Average(wi => wi.Effort ?? 0)
            ))
            .OrderByDescending(e => e.TotalEffort)
            .ToList();
    }

    private static List<EffortByIteration> CalculateEffortByIteration(
        List<WorkItemDto> workItems,
        List<string> iterationPaths,
        int? defaultCapacity)
    {
        return iterationPaths
            .Select(iterationPath =>
            {
                var itemsInIteration = workItems
                    .Where(wi => wi.IterationPath.Equals(iterationPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var totalEffort = itemsInIteration.Sum(wi => wi.Effort ?? 0);
                var sprintName = ExtractSprintName(iterationPath);

                var utilization = defaultCapacity.HasValue && defaultCapacity.Value > 0
                    ? (double)totalEffort / defaultCapacity.Value * 100
                    : 0;

                return new EffortByIteration(
                    IterationPath: iterationPath,
                    SprintName: sprintName,
                    TotalEffort: totalEffort,
                    WorkItemCount: itemsInIteration.Count,
                    Capacity: defaultCapacity,
                    UtilizationPercentage: utilization
                );
            })
            .ToList();
    }

    private static List<EffortHeatMapCell> CalculateHeatMapCells(
        List<WorkItemDto> workItems,
        List<string> areaPaths,
        List<string> iterationPaths,
        int? defaultCapacity)
    {
        var cells = new List<EffortHeatMapCell>();

        foreach (var areaPath in areaPaths)
        {
            foreach (var iterationPath in iterationPaths)
            {
                var itemsInCell = workItems
                    .Where(wi => 
                        wi.AreaPath.Equals(areaPath, StringComparison.OrdinalIgnoreCase) &&
                        wi.IterationPath.Equals(iterationPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var effort = itemsInCell.Sum(wi => wi.Effort ?? 0);
                var status = DetermineCapacityStatus(effort, defaultCapacity);

                cells.Add(new EffortHeatMapCell(
                    AreaPath: areaPath,
                    IterationPath: iterationPath,
                    Effort: effort,
                    WorkItemCount: itemsInCell.Count,
                    Status: status
                ));
            }
        }

        return cells;
    }

    private static CapacityStatus DetermineCapacityStatus(int effort, int? capacity)
    {
        if (!capacity.HasValue || capacity.Value == 0)
        {
            return CapacityStatus.Unknown;
        }

        var utilizationPercentage = (double)effort / capacity.Value * 100;

        return utilizationPercentage switch
        {
            < 50 => CapacityStatus.Underutilized,
            >= 50 and < 85 => CapacityStatus.Normal,
            >= 85 and < 100 => CapacityStatus.NearCapacity,
            _ => CapacityStatus.OverCapacity
        };
    }

    private static string ExtractSprintName(string iterationPath)
    {
        var parts = iterationPath.Split('\\', '/');
        return parts.Length > 0 ? parts[^1] : iterationPath;
    }
}
