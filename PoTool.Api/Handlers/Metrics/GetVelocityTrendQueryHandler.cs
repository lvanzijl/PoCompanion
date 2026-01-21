using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;

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

        IEnumerable<WorkItemDto> allWorkItems;

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
                // Load work items hierarchically from product roots
                _logger.LogDebug("Loading work items hierarchically from {Count} product roots: {RootIds}",
                    rootWorkItemIds.Count, string.Join(", ", rootWorkItemIds));
                
                // Use GetWorkItemsByRootIdsAsync through mediator
                var rootIds = rootWorkItemIds.ToArray();
                var workItemsQuery = new GetWorkItemsByRootIdsQuery(rootIds);
                allWorkItems = await _mediator.Send(workItemsQuery, cancellationToken);

                _logger.LogDebug("Loaded {Count} work items in product hierarchies (roots: {RootIds})",
                    allWorkItems.Count(),
                    string.Join(", ", rootWorkItemIds));
            }
            else
            {
                _logger.LogWarning("No valid products found for IDs: {ProductIds}", string.Join(", ", query.ProductIds));
                allWorkItems = Enumerable.Empty<WorkItemDto>();
            }
        }
        // Otherwise, filter by area path if specified (legacy behavior)
        else if (!string.IsNullOrWhiteSpace(query.AreaPath))
        {
            allWorkItems = await _repository.GetAllAsync(cancellationToken);
            allWorkItems = allWorkItems
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            // No filters specified - load from configured products
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
                    _logger.LogDebug("Loading work items from all {Count} configured products", rootIds.Length);
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

        // Get metrics for each sprint, filtering to last 6 months
        var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-6);
        var sprintMetricsList = new List<SprintMetricsDto>();
        foreach (var iterationPath in iterationPaths)
        {
            var sprintMetrics = await _mediator.Send(
                new GetSprintMetricsQuery(iterationPath),
                cancellationToken);

            if (sprintMetrics != null)
            {
                // Only include sprints that ended within the last 6 months (or have no end date)
                if (sprintMetrics.EndDate == null || sprintMetrics.EndDate >= sixMonthsAgo)
                {
                    sprintMetricsList.Add(sprintMetrics);
                }
                else
                {
                    _logger.LogDebug("Filtering out sprint {Sprint} - ended before 6-month window ({EndDate})", 
                        sprintMetrics.SprintName, sprintMetrics.EndDate);
                }
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
