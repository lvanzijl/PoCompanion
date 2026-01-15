using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetVelocityTrendQuery.
/// Calculates velocity trends across multiple sprints.
/// Supports filtering by product IDs (with deduplication) or area path.
/// </summary>
public sealed class GetVelocityTrendQueryHandler : IQueryHandler<GetVelocityTrendQuery, VelocityTrendDto>
{
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<GetVelocityTrendQueryHandler> _logger;

    public GetVelocityTrendQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        ILogger<GetVelocityTrendQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<VelocityTrendDto> Handle(
        GetVelocityTrendQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetVelocityTrendQuery for ProductIds: {ProductIds}, AreaPath: {AreaPath}, MaxSprints: {MaxSprints}",
            query.ProductIds != null ? string.Join(", ", query.ProductIds) : "None",
            query.AreaPath ?? "All", 
            query.MaxSprints);

        var allWorkItems = await _repository.GetAllAsync(cancellationToken);

        // Filter by product hierarchy if ProductIds are specified
        if (query.ProductIds != null && query.ProductIds.Length > 0)
        {
            var rootWorkItemIds = new List<int>();
            
            // Collect root work item IDs from all specified products
            foreach (var productId in query.ProductIds)
            {
                var product = await _productRepository.GetProductByIdAsync(productId, cancellationToken);
                if (product == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found, skipping", productId);
                    continue;
                }
                rootWorkItemIds.Add(product.BacklogRootWorkItemId);
            }

            if (rootWorkItemIds.Count > 0)
            {
                // Filter to only work items in the products' hierarchies
                // FilterDescendants automatically deduplicates by TfsId using HashSet internally
                allWorkItems = WorkItemHierarchyHelper.FilterDescendants(rootWorkItemIds, allWorkItems);

                _logger.LogDebug(
                    "Filtered to {Count} work items in product hierarchies (roots: {RootIds}), deduplicated by TfsId",
                    allWorkItems.Count(),
                    string.Join(", ", rootWorkItemIds));
            }
            else
            {
                _logger.LogWarning("No valid products found for IDs: {ProductIds}", string.Join(", ", query.ProductIds));
            }
        }
        // Otherwise, filter by area path if specified (legacy behavior)
        else if (!string.IsNullOrWhiteSpace(query.AreaPath))
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
