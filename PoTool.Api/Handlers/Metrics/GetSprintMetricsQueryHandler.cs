using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetSprintMetricsQuery.
/// Reconstructs historical sprint metrics for a sprint window.
/// Planned scope comes from sprint membership at the canonical commitment timestamp,
/// and completed scope comes from the first canonical Done transition within the sprint window.
/// This matches the delivery rules in docs/domain/domain_model.md,
/// docs/domain/rules/sprint_rules.md, docs/domain/rules/metrics_rules.md,
/// and docs/domain/rules/source_rules.md.
/// </summary>
public sealed class GetSprintMetricsQueryHandler : IQueryHandler<GetSprintMetricsQuery, SprintMetricsDto?>
{
    private const string IterationPathFieldRefName = "System.IterationPath";
    private const string StateFieldRefName = "System.State";

    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly ISprintRepository _sprintRepository;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly ICanonicalStoryPointResolutionService _storyPointResolutionService;
    private readonly IMediator _mediator;
    private readonly PoToolDbContext _context;
    private readonly ILogger<GetSprintMetricsQueryHandler> _logger;

    public GetSprintMetricsQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        ISprintRepository sprintRepository,
        IWorkItemStateClassificationService stateClassificationService,
        ICanonicalStoryPointResolutionService storyPointResolutionService,
        IMediator mediator,
        PoToolDbContext context,
        ILogger<GetSprintMetricsQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _sprintRepository = sprintRepository;
        _stateClassificationService = stateClassificationService;
        _storyPointResolutionService = storyPointResolutionService;
        _mediator = mediator;
        _context = context;
        _logger = logger;
    }

    public async ValueTask<SprintMetricsDto?> Handle(
        GetSprintMetricsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetSprintMetricsQuery for iteration: {IterationPath}", query.IterationPath);

        var allSprints = await _sprintRepository.GetAllSprintsAsync(cancellationToken);
        var matchingSprint = allSprints.FirstOrDefault(s =>
            s.Path.Equals(query.IterationPath, StringComparison.OrdinalIgnoreCase));

        if (matchingSprint?.StartUtc == null || matchingSprint.EndUtc == null)
        {
            _logger.LogDebug(
                "Sprint metrics require a dated sprint window; no sprint metadata was found for {IterationPath}",
                query.IterationPath);
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

        var relevantWorkItems = allWorkItems
            .ToList();

        var workItemIds = relevantWorkItems.Select(wi => wi.TfsId).ToArray();
        var sprintStart = matchingSprint.StartUtc.Value;
        var sprintEnd = matchingSprint.EndUtc.Value;
        var sprintDefinition = matchingSprint.ToDefinition();
        var workItemSnapshotsById = relevantWorkItems.ToSnapshotDictionary();
        var commitmentTimestamp = SprintCommitmentLookup.GetCommitmentTimestamp(sprintStart);
        var iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>();
        var firstDoneByWorkItem = new Dictionary<int, DateTimeOffset>();
        var addedWorkItemIds = new HashSet<int>();
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null;

        if (workItemIds.Length > 0)
        {
            var allHistoryEvents = await _context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => workItemIds.Contains(e.WorkItemId)
                            && (e.FieldRefName == IterationPathFieldRefName || e.FieldRefName == StateFieldRefName))
                .ToListAsync(cancellationToken);
            var allHistoryFieldChanges = allHistoryEvents.ToFieldChangeEvents();

            var iterationEvents = allHistoryFieldChanges
                .Where(e => string.Equals(e.FieldRefName, IterationPathFieldRefName, StringComparison.OrdinalIgnoreCase))
                .Where(e => e.TimestampUtc >= sprintStart.UtcDateTime)
                .ToList();

            iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>(iterationEvents.GroupByWorkItemId());

            var classifications = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
            stateLookup = StateClassificationLookup.Create(classifications.Classifications);
            firstDoneByWorkItem = FirstDoneDeliveryLookup.Build(allHistoryFieldChanges, workItemSnapshotsById, stateLookup)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            addedWorkItemIds = iterationEvents
                .Where(e => string.Equals(e.NewValue, sprintDefinition.Path, StringComparison.OrdinalIgnoreCase))
                .Where(e =>
                {
                    var eventTimestamp = FirstDoneDeliveryLookup.GetEventTimestamp(e);
                    return eventTimestamp > commitmentTimestamp && eventTimestamp <= sprintEnd;
                })
                .Select(e => e.WorkItemId)
                .ToHashSet();
        }

        var committedWorkItemIds = SprintCommitmentLookup.BuildCommittedWorkItemIds(
                workItemSnapshotsById,
                iterationEventsByWorkItem,
                sprintDefinition.Path,
                commitmentTimestamp)
            .ToHashSet();

        var sprintScopeIds = committedWorkItemIds
            .Concat(addedWorkItemIds)
            .Distinct()
            .ToHashSet();

        var sprintScopeWorkItems = relevantWorkItems
            .Where(wi => sprintScopeIds.Contains(wi.TfsId))
            .ToList();

        var completedItems = sprintScopeWorkItems
            .Where(wi => firstDoneByWorkItem.TryGetValue(wi.TfsId, out var firstDoneTimestamp)
                         && firstDoneTimestamp >= sprintStart
                         && firstDoneTimestamp <= sprintEnd)
            .ToList();

        if (stateLookup == null)
        {
            var classificationsResponse = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
            stateLookup = StateClassificationLookup.Create(classificationsResponse.Classifications);
        }

        var completedStoryPoints = completedItems
            .Select(wi => ResolveSprintStoryPoints(wi, isDone: true))
            .Where(resolution => resolution.HasValue)
            .Select(resolution => resolution!.Value)
            .Sum();

        var plannedStoryPoints = sprintScopeWorkItems
            .Where(wi => committedWorkItemIds.Contains(wi.TfsId))
            .Select(wi => ResolveSprintStoryPoints(
                wi,
                StateClassificationLookup.IsDone(stateLookup, wi.Type, wi.State)))
            .Where(resolution => resolution.HasValue)
            .Select(resolution => resolution!.Value)
            .Sum();

        var completedPBIs = completedItems.Count(wi =>
            IsPbiType(wi.Type));

        var completedBugs = completedItems.Count(wi =>
            wi.Type.Equals(WorkItemType.Bug, StringComparison.OrdinalIgnoreCase));

        var completedTasks = completedItems.Count(wi =>
            wi.Type.Equals(WorkItemType.Task, StringComparison.OrdinalIgnoreCase));

        var sprintName = string.IsNullOrWhiteSpace(matchingSprint.Name)
            ? query.IterationPath
            : matchingSprint.Name;

        var metrics = new SprintMetricsDto(
            IterationPath: query.IterationPath,
            SprintName: sprintName,
            StartDate: sprintStart,
            EndDate: sprintEnd,
            CompletedStoryPoints: completedStoryPoints,
            PlannedStoryPoints: plannedStoryPoints,
            CompletedWorkItemCount: completedItems.Count,
            TotalWorkItemCount: sprintScopeWorkItems.Count,
            CompletedPBIs: completedPBIs,
            CompletedBugs: completedBugs,
            CompletedTasks: completedTasks
        );

        _logger.LogInformation(
            "Sprint metrics calculated for {IterationPath}: delivered {CompletedPoints} story points from {CompletedCount} scope items against {PlannedPoints} committed story points",
            query.IterationPath,
            completedStoryPoints,
            completedItems.Count,
            plannedStoryPoints);

        return metrics;
    }

    private int? ResolveSprintStoryPoints(WorkItemDto workItem, bool isDone)
    {
        var estimate = _storyPointResolutionService.Resolve(new StoryPointResolutionRequest(workItem.ToCanonicalWorkItem(), isDone));
        if (estimate.Source is StoryPointEstimateSource.Missing or StoryPointEstimateSource.Derived || !estimate.Value.HasValue)
        {
            return null;
        }

        return (int)estimate.Value.Value;
    }

    private static bool IsPbiType(string workItemType)
    {
        return workItemType.Equals(WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.PbiShort, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.UserStory, StringComparison.OrdinalIgnoreCase);
    }
}
