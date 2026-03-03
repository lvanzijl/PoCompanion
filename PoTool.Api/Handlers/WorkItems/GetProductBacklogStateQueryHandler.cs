using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Health;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Health;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="GetProductBacklogStateQuery"/>.
/// Loads the work item graph for the requested product and delegates score computation
/// to <see cref="BacklogStateComputationService"/>. No validation logic is involved.
/// </summary>
public sealed class GetProductBacklogStateQueryHandler
    : IQueryHandler<GetProductBacklogStateQuery, ProductBacklogStateDto?>
{
    private readonly IProductRepository _productRepository;
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly BacklogStateComputationService _computationService;
    private readonly ILogger<GetProductBacklogStateQueryHandler> _logger;

    public GetProductBacklogStateQueryHandler(
        IProductRepository productRepository,
        IWorkItemReadProvider workItemReadProvider,
        BacklogStateComputationService computationService,
        ILogger<GetProductBacklogStateQueryHandler> logger)
    {
        _productRepository = productRepository;
        _workItemReadProvider = workItemReadProvider;
        _computationService = computationService;
        _logger = logger;
    }

    public async ValueTask<ProductBacklogStateDto?> Handle(
        GetProductBacklogStateQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetProductBacklogStateQuery for ProductId: {ProductId}", query.ProductId);

        var product = await _productRepository.GetProductByIdAsync(query.ProductId, cancellationToken);
        if (product is null)
        {
            _logger.LogWarning("Product {ProductId} not found", query.ProductId);
            return null;
        }

        if (product.BacklogRootWorkItemIds.Count == 0)
        {
            _logger.LogDebug("Product {ProductId} has no backlog root work items configured", query.ProductId);
            return new ProductBacklogStateDto
            {
                ProductId = query.ProductId,
                Epics = Array.Empty<EpicRefinementDto>()
            };
        }

        var allItems = (await _workItemReadProvider.GetByRootIdsAsync(
            product.BacklogRootWorkItemIds.ToArray(),
            cancellationToken)).ToList();

        _logger.LogDebug(
            "Loaded {Count} work items for product {ProductId}",
            allItems.Count, query.ProductId);

        var epics = allItems
            .Where(w => string.Equals(w.Type, WorkItemType.Epic, StringComparison.OrdinalIgnoreCase))
            .Select(epic => BuildEpicDto(epic, allItems))
            .ToList();

        return new ProductBacklogStateDto
        {
            ProductId = query.ProductId,
            Epics = epics
        };
    }

    private EpicRefinementDto BuildEpicDto(WorkItemDto epic, IReadOnlyList<WorkItemDto> allItems)
    {
        var epicScore = _computationService.ComputeEpicScore(epic, allItems);

        var features = allItems
            .Where(w => w.ParentTfsId == epic.TfsId &&
                        string.Equals(w.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase))
            .Select(feature => BuildFeatureDto(feature, allItems))
            .ToList();

        return new EpicRefinementDto
        {
            TfsId = epic.TfsId,
            Title = epic.Title,
            Score = epicScore.Score,
            Features = features
        };
    }

    private FeatureRefinementDto BuildFeatureDto(WorkItemDto feature, IReadOnlyList<WorkItemDto> allItems)
    {
        var featureScore = _computationService.ComputeFeatureScore(feature, allItems);

        var pbis = allItems
            .Where(w => w.ParentTfsId == feature.TfsId &&
                        string.Equals(w.Type, WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase))
            .Select(pbi =>
            {
                var pbiScore = _computationService.ComputePbiScore(pbi);
                return new PbiReadinessDto
                {
                    TfsId = pbi.TfsId,
                    Score = pbiScore.Score,
                    Effort = pbi.Effort
                };
            })
            .ToList();

        return new FeatureRefinementDto
        {
            TfsId = feature.TfsId,
            Title = feature.Title,
            Score = featureScore.Score,
            OwnerState = featureScore.OwnerState,
            Pbis = pbis
        };
    }
}
