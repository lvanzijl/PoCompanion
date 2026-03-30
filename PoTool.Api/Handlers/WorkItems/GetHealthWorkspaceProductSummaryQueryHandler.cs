using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Health;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Health;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="GetHealthWorkspaceProductSummaryQuery"/>.
/// Produces a lightweight dashboard summary without returning the full Epic → Feature → PBI graph.
/// </summary>
public sealed class GetHealthWorkspaceProductSummaryQueryHandler
    : IQueryHandler<GetHealthWorkspaceProductSummaryQuery, HealthWorkspaceProductSummaryDto?>
{
    private const int TopEpicCount = 3;

    private readonly IProductRepository _productRepository;
    private readonly IWorkItemQuery _workItemQuery;
    private readonly BacklogStateComputationService _computationService;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly ILogger<GetHealthWorkspaceProductSummaryQueryHandler> _logger;

    public GetHealthWorkspaceProductSummaryQueryHandler(
        IProductRepository productRepository,
        IWorkItemQuery workItemQuery,
        BacklogStateComputationService computationService,
        IWorkItemStateClassificationService stateClassificationService,
        ILogger<GetHealthWorkspaceProductSummaryQueryHandler> logger)
    {
        _productRepository = productRepository;
        _workItemQuery = workItemQuery;
        _computationService = computationService;
        _stateClassificationService = stateClassificationService;
        _logger = logger;
    }

    public async ValueTask<HealthWorkspaceProductSummaryDto?> Handle(
        GetHealthWorkspaceProductSummaryQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetHealthWorkspaceProductSummaryQuery for ProductId: {ProductId}", query.ProductId);

        var product = await _productRepository.GetProductByIdAsync(query.ProductId, cancellationToken);
        if (product is null)
        {
            _logger.LogWarning("Product {ProductId} not found", query.ProductId);
            return null;
        }

        if (product.BacklogRootWorkItemIds.Count == 0)
        {
            return new HealthWorkspaceProductSummaryDto
            {
                ProductId = query.ProductId,
                ReadyEffort = 0,
                FeaturesReadyInPendingEpics = 0,
                TopEpics = Array.Empty<HealthWorkspaceEpicSummaryDto>()
            };
        }

        var allItems = (await _workItemQuery.GetByRootIdsAsync(
            product.BacklogRootWorkItemIds.ToArray(),
            cancellationToken)).ToList();

        var classifications = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
        var doneStateLookup = classifications.Classifications
            .Where(c => c.Classification == StateClassification.Done)
            .Select(c => (Type: c.WorkItemType.ToLowerInvariant(), State: c.StateName.ToLowerInvariant()))
            .ToHashSet();
        var removedStateLookup = classifications.Classifications
            .Where(c => c.Classification == StateClassification.Removed)
            .Select(c => (Type: c.WorkItemType.ToLowerInvariant(), State: c.StateName.ToLowerInvariant()))
            .ToHashSet();

        var removedItemIds = allItems
            .Where(w => removedStateLookup.Contains((w.Type.ToLowerInvariant(), w.State.ToLowerInvariant())))
            .Select(w => w.TfsId)
            .ToHashSet();

        var doneItemIds = allItems
            .Where(w => doneStateLookup.Contains((w.Type.ToLowerInvariant(), w.State.ToLowerInvariant())))
            .Select(w => w.TfsId)
            .ToHashSet();

        var scoringItems = allItems
            .Where(w => !removedItemIds.Contains(w.TfsId))
            .ToList();
        var displayItems = scoringItems
            .Where(w => !doneItemIds.Contains(w.TfsId))
            .ToList();

        var epicSummaries = displayItems
            .Where(w => string.Equals(w.Type, WorkItemType.Epic, StringComparison.OrdinalIgnoreCase))
            .Select(epic => BuildEpicSummary(epic, scoringItems, displayItems, doneItemIds))
            .ToList();

        return new HealthWorkspaceProductSummaryDto
        {
            ProductId = query.ProductId,
            ReadyEffort = epicSummaries
                .Where(epic => epic.Score == 100)
                .Sum(epic => epic.Effort),
            FeaturesReadyInPendingEpics = epicSummaries
                .Where(epic => epic.Score < 100)
                .Sum(epic => epic.ReadyFeatureCount),
            TopEpics = epicSummaries
                .OrderByDescending(epic => epic.Score)
                .ThenBy(epic => epic.Title, StringComparer.OrdinalIgnoreCase)
                .Take(TopEpicCount)
                .Select(epic => new HealthWorkspaceEpicSummaryDto
                {
                    TfsId = epic.TfsId,
                    Title = epic.Title,
                    Score = epic.Score,
                    Effort = epic.Effort
                })
                .ToList()
        };
    }

    private EpicSummary BuildEpicSummary(
        WorkItemDto epic,
        IReadOnlyList<WorkItemDto> scoringItems,
        IReadOnlyList<WorkItemDto> displayItems,
        IReadOnlySet<int> doneItemIds)
    {
        var epicScore = _computationService.ComputeEpicScore(epic, scoringItems, doneItemIds);

        var features = displayItems
            .Where(w => w.ParentTfsId == epic.TfsId &&
                        string.Equals(w.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var readyFeatureCount = features.Count(feature =>
            _computationService.ComputeFeatureScore(feature, scoringItems, doneItemIds).Score == 100);

        var visibleFeatureIds = features
            .Select(feature => feature.TfsId)
            .ToHashSet();

        var effort = displayItems
            .Where(w => visibleFeatureIds.Contains(w.ParentTfsId ?? 0) &&
                        string.Equals(w.Type, WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase))
            .Sum(w => w.Effort ?? 0);

        return new EpicSummary(epic.TfsId, epic.Title, epicScore.Score, effort, readyFeatureCount);
    }

    private sealed record EpicSummary(
        int TfsId,
        string Title,
        int Score,
        int Effort,
        int ReadyFeatureCount);
}
