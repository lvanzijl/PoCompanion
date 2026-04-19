using PoTool.Core.Contracts;
using PoTool.Core.Domain.Planning;
using PoTool.Shared.Planning;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.Planning;

/// <summary>
/// Builds deterministic product planning board read models and executes planning operations in memory.
/// </summary>
public interface IProductPlanningBoardService
{
    ValueTask<ProductPlanningBoardDto?> BuildPlanningBoardAsync(int productId, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> GetPlanningBoardAsync(int productId, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteMoveEpicBySprintsAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteAdjustSpacingBeforeAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteRunInParallelAsync(int productId, int epicId, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteReturnToMainAsync(int productId, int epicId, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteReorderEpicAsync(int productId, int epicId, int targetRoadmapOrder, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteShiftPlanAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stateless application-layer bridge between active product/work-item inputs and the planning engine.
/// </summary>
public sealed class ProductPlanningBoardService : IProductPlanningBoardService
{
    private readonly IProductRepository _productRepository;
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly PlanningRecomputeService _recomputeService;
    private readonly PlanningOperationService _operationService;

    public ProductPlanningBoardService(
        IProductRepository productRepository,
        IWorkItemReadProvider workItemReadProvider)
        : this(
            productRepository,
            workItemReadProvider,
            new PlanningRecomputeService(),
            new PlanningOperationService())
    {
    }

    internal ProductPlanningBoardService(
        IProductRepository productRepository,
        IWorkItemReadProvider workItemReadProvider,
        PlanningRecomputeService recomputeService,
        PlanningOperationService operationService)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _workItemReadProvider = workItemReadProvider ?? throw new ArgumentNullException(nameof(workItemReadProvider));
        _recomputeService = recomputeService ?? throw new ArgumentNullException(nameof(recomputeService));
        _operationService = operationService ?? throw new ArgumentNullException(nameof(operationService));
    }

    public ValueTask<ProductPlanningBoardDto?> BuildPlanningBoardAsync(int productId, CancellationToken cancellationToken = default)
    {
        return BuildAsync(productId, static (service, bootstrap) => service.CreateReadModel(bootstrap, null), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> GetPlanningBoardAsync(int productId, CancellationToken cancellationToken = default)
    {
        return BuildPlanningBoardAsync(productId, cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteMoveEpicBySprintsAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, bootstrap => _operationService.MoveEpicBySprints(bootstrap.State, epicId, deltaSprints), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteAdjustSpacingBeforeAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, bootstrap => _operationService.AdjustSpacingBefore(bootstrap.State, epicId, deltaSprints), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteRunInParallelAsync(int productId, int epicId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, bootstrap => _operationService.RunInParallel(bootstrap.State, epicId), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteReturnToMainAsync(int productId, int epicId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, bootstrap => _operationService.ReturnToMain(bootstrap.State, epicId), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteReorderEpicAsync(int productId, int epicId, int targetRoadmapOrder, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, bootstrap => _operationService.ReorderEpic(bootstrap.State, epicId, targetRoadmapOrder), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteShiftPlanAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, bootstrap => _operationService.ShiftPlan(bootstrap.State, epicId, deltaSprints), cancellationToken);
    }

    private async ValueTask<ProductPlanningBoardDto?> BuildAsync(
        int productId,
        Func<ProductPlanningBoardService, BootstrapContext, ProductPlanningBoardDto> map,
        CancellationToken cancellationToken)
    {
        var bootstrap = await BuildBootstrapContextAsync(productId, cancellationToken);
        return bootstrap is null ? null : map(this, bootstrap);
    }

    private async ValueTask<ProductPlanningBoardDto?> ExecuteAsync(
        int productId,
        Func<BootstrapContext, PlanningOperationResult> execute,
        CancellationToken cancellationToken)
    {
        var bootstrap = await BuildBootstrapContextAsync(productId, cancellationToken);
        if (bootstrap is null)
        {
            return null;
        }

        return CreateReadModel(bootstrap, execute(bootstrap));
    }

    private async ValueTask<BootstrapContext?> BuildBootstrapContextAsync(int productId, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetProductByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var rootIds = product.BacklogRootWorkItemIds
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();

        var workItems = rootIds.Length == 0
            ? Array.Empty<WorkItemDto>()
            : (await _workItemReadProvider.GetByRootIdsAsync(rootIds, cancellationToken)).ToArray();

        var roadmapEpics = workItems
            .Where(static workItem => IsRoadmapEpic(workItem))
            .OrderBy(static workItem => workItem.BacklogPriority ?? double.MaxValue)
            .ThenBy(static workItem => workItem.TfsId)
            .Select((workItem, index) => new BootstrapEpic(
                workItem.TfsId,
                string.IsNullOrWhiteSpace(workItem.Title) ? $"Epic {workItem.TfsId}" : workItem.Title,
                index + 1))
            .ToArray();

        var bootstrapState = roadmapEpics.Length == 0
            ? PlanningState.Empty
            : _recomputeService.RecomputeFrom(
                new PlanningState(
                    roadmapEpics
                        .Select((epic, index) => new PlanningEpicState(
                            epic.EpicId,
                            epic.RoadmapOrder,
                            index,
                            0,
                            1,
                            0))
                        .ToArray()),
                0);

        return new BootstrapContext(product.Id, product.Name, roadmapEpics, bootstrapState);
    }

    private ProductPlanningBoardDto CreateReadModel(BootstrapContext bootstrap, PlanningOperationResult? operationResult)
    {
        var state = operationResult?.State ?? bootstrap.State;
        var issues = MapIssues(operationResult?.ValidationIssues ?? Array.Empty<PlanningValidationIssue>());
        var changedEpicIds = operationResult?.ChangedEpicIds ?? Array.Empty<int>();
        var affectedEpicIds = operationResult?.AffectedEpicIds ?? Array.Empty<int>();
        var issueLookup = issues
            .Where(static issue => issue.EpicId.HasValue)
            .GroupBy(issue => issue.EpicId!.Value)
            .ToDictionary(static group => group.Key, static group => (IReadOnlyList<PlanningBoardIssueDto>)group.ToArray());
        var titleLookup = bootstrap.Epics.ToDictionary(static epic => epic.EpicId, static epic => epic.EpicTitle);
        var changedEpicIdSet = changedEpicIds.ToHashSet();
        var affectedEpicIdSet = affectedEpicIds.ToHashSet();

        var epicItems = state.Epics
            .Select(epic => new PlanningBoardEpicItemDto(
                epic.EpicId,
                titleLookup.GetValueOrDefault(epic.EpicId, $"Epic {epic.EpicId}"),
                epic.RoadmapOrder,
                epic.TrackIndex,
                epic.PlannedStartSprintIndex,
                epic.ComputedStartSprintIndex,
                epic.DurationInSprints,
                epic.EndSprintIndexExclusive,
                issueLookup.GetValueOrDefault(epic.EpicId, Array.Empty<PlanningBoardIssueDto>()),
                changedEpicIdSet.Contains(epic.EpicId),
                affectedEpicIdSet.Contains(epic.EpicId)))
            .OrderBy(static epic => epic.RoadmapOrder)
            .ToArray();

        var maxTrackIndex = epicItems.Length == 0 ? 0 : epicItems.Max(static epic => epic.TrackIndex);
        var tracks = Enumerable
            .Range(0, maxTrackIndex + 1)
            .Select(trackIndex => new PlanningBoardTrackDto(
                trackIndex,
                trackIndex == 0,
                epicItems
                    .Where(epic => epic.TrackIndex == trackIndex)
                    .OrderBy(static epic => epic.ComputedStartSprintIndex)
                    .ThenBy(static epic => epic.RoadmapOrder)
                    .Select(static epic => epic.EpicId)
                    .ToArray()))
            .ToArray();

        return new ProductPlanningBoardDto(
            bootstrap.ProductId,
            bootstrap.ProductName,
            tracks,
            epicItems,
            issues,
            changedEpicIds,
            affectedEpicIds);
    }

    private static IReadOnlyList<PlanningBoardIssueDto> MapIssues(IReadOnlyList<PlanningValidationIssue> issues)
    {
        return issues
            .Select(static issue => new PlanningBoardIssueDto(
                "Validation",
                issue.Code.ToString(),
                issue.Message,
                issue.EpicId))
            .ToArray();
    }

    private static bool IsRoadmapEpic(WorkItemDto workItem)
    {
        return string.Equals(workItem.Type?.Trim(), "Epic", StringComparison.OrdinalIgnoreCase) &&
               HasRoadmapTag(workItem.Tags);
    }

    private static bool HasRoadmapTag(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return false;
        }

        return tags
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(static tag => string.Equals(tag, "roadmap", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record BootstrapContext(
        int ProductId,
        string ProductName,
        IReadOnlyList<BootstrapEpic> Epics,
        PlanningState State);

    private sealed record BootstrapEpic(
        int EpicId,
        string EpicTitle,
        int RoadmapOrder);
}
