using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.Metrics.Models;
using PoTool.Core.Metrics.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetSprintExecutionQuery.
///
/// Reconstructs sprint backlog evolution from cached work items and activity events.
/// Derives initial scope, added/removed items, completion order, spillover, and starved work
/// using lightweight queries against existing data — no heavy revision fetches.
/// </summary>
public sealed class GetSprintExecutionQueryHandler
    : IQueryHandler<GetSprintExecutionQuery, SprintExecutionDto>
{
    private const string IterationPathField = "System.IterationPath";
    private const string PbiType = "Product Backlog Item";
    private const string BugType = "Bug";

    private readonly PoToolDbContext _context;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly ICanonicalStoryPointResolutionService _storyPointResolutionService;
    private readonly ILogger<GetSprintExecutionQueryHandler> _logger;

    public GetSprintExecutionQueryHandler(
        PoToolDbContext context,
        IWorkItemStateClassificationService stateClassificationService,
        ICanonicalStoryPointResolutionService storyPointResolutionService,
        ILogger<GetSprintExecutionQueryHandler> logger)
    {
        _context = context;
        _stateClassificationService = stateClassificationService;
        _storyPointResolutionService = storyPointResolutionService;
        _logger = logger;
    }

    public async ValueTask<SprintExecutionDto> Handle(
        GetSprintExecutionQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling GetSprintExecutionQuery for ProductOwner {ProductOwnerId}, Sprint {SprintId}, Product {ProductId}",
            query.ProductOwnerId, query.SprintId, query.ProductId);

        // ── Step 1: Load sprint metadata ──────────────────────────────────────
        var sprint = await _context.Sprints
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.SprintId, cancellationToken);

        if (sprint == null)
        {
            _logger.LogWarning("Sprint {SprintId} not found", query.SprintId);
            return EmptyResult(query.SprintId);
        }

        // ── Step 2: Resolve products for the product owner ────────────────────
        var ownerProductIds = await _context.Products
            .Where(p => p.ProductOwnerId == query.ProductOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (ownerProductIds.Count == 0)
        {
            _logger.LogWarning("No products found for ProductOwner {ProductOwnerId}", query.ProductOwnerId);
            return EmptyResult(query.SprintId, sprint.Name, sprint.StartUtc, sprint.EndUtc);
        }

        // Apply product filter if specified
        var targetProductIds = query.ProductId.HasValue
            ? ownerProductIds.Where(id => id == query.ProductId.Value).ToList()
            : ownerProductIds;

        if (targetProductIds.Count == 0)
        {
            return EmptyResult(query.SprintId, sprint.Name, sprint.StartUtc, sprint.EndUtc);
        }

        // ── Step 3: Get resolved PBI/Bug IDs for target products ──────────────
        var resolvedItems = await _context.ResolvedWorkItems
            .AsNoTracking()
            .Where(r => r.ResolvedProductId != null
                        && targetProductIds.Contains(r.ResolvedProductId.Value)
                        && (r.WorkItemType == PbiType || r.WorkItemType == BugType))
            .Select(r => new { r.WorkItemId, r.ResolvedProductId })
            .ToListAsync(cancellationToken);

        var resolvedWorkItemIds = resolvedItems.Select(r => r.WorkItemId).ToHashSet();
        var productByWorkItem = resolvedItems.ToDictionary(r => r.WorkItemId, r => r.ResolvedProductId!.Value);

        // ── Step 4: Load relevant work items ──────────────────────────────────
        var relevantWorkItems = await _context.WorkItems
            .AsNoTracking()
            .Where(w => (w.Type == PbiType || w.Type == BugType)
                        && resolvedWorkItemIds.Contains(w.TfsId))
            .ToListAsync(cancellationToken);

        var relevantWorkItemsById = relevantWorkItems.ToDictionary(w => w.TfsId, w => w);
        var workItemSnapshotsById = relevantWorkItems.ToSnapshotDictionary();
        var currentSprintItems = relevantWorkItems
            .Where(w => w.IterationPath == sprint.Path)
            .ToList();
        var sprintDefinition = sprint.ToDefinition();

        // ── Step 5: Get product names ─────────────────────────────────────────
        var productNames = await _context.Products
            .AsNoTracking()
            .Where(p => targetProductIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        // ── Step 6: Get canonical state classifications ───────────────────────
        var classifications = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
        var stateLookup = StateClassificationLookup.Create(classifications.Classifications);

        // ── Step 7: Detect sprint additions and removals from activity events ─
        var sprintStart = sprint.StartUtc;
        var sprintEnd = sprint.EndUtc;
        var commitmentTimestamp = sprintStart.HasValue
            ? SprintCommitmentLookup.GetCommitmentTimestamp(sprintStart.Value)
            : (DateTimeOffset?)null;
        var firstDoneByWorkItem = new Dictionary<int, DateTimeOffset>();
        var committedWorkItemIds = new HashSet<int>();

        var addedWorkItemIds = new HashSet<int>();
        var removedEntries = new List<(int WorkItemId, DateTimeOffset Timestamp)>();
        var iterationEvents = new List<FieldChangeEvent>();
        var iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>();
        var stateEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>();
        var stateHistoryCutoffUtc = DateTime.UtcNow;

        if (sprintEnd.HasValue)
        {
            var stateEvents = await _context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => e.ProductOwnerId == query.ProductOwnerId
                            && e.FieldRefName == "System.State"
                            && e.EventTimestampUtc <= stateHistoryCutoffUtc
                            && resolvedWorkItemIds.Contains(e.WorkItemId))
                .ToListAsync(cancellationToken);
            var stateFieldChanges = stateEvents.ToFieldChangeEvents();

            stateEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>(stateFieldChanges.GroupByWorkItemId());

            firstDoneByWorkItem = FirstDoneDeliveryLookup.Build(
                    stateFieldChanges,
                    workItemSnapshotsById,
                    stateLookup)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        if (sprintStart.HasValue)
        {
            var rawIterationEvents = await _context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => e.ProductOwnerId == query.ProductOwnerId
                            && e.FieldRefName == IterationPathField
                            && e.EventTimestampUtc >= sprintStart.Value.UtcDateTime
                            && resolvedWorkItemIds.Contains(e.WorkItemId))
                .ToListAsync(cancellationToken);
            var iterationFieldChanges = rawIterationEvents.ToFieldChangeEvents();

            iterationEvents = iterationFieldChanges.ToList();
            iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>(iterationFieldChanges.GroupByWorkItemId());
        }

        if (commitmentTimestamp.HasValue)
        {
            committedWorkItemIds = SprintCommitmentLookup.BuildCommittedWorkItemIds(
                    workItemSnapshotsById,
                    iterationEventsByWorkItem,
                    sprintDefinition.Path,
                    commitmentTimestamp.Value)
                .ToHashSet();
        }
        else
        {
            committedWorkItemIds = currentSprintItems
                .Select(w => w.TfsId)
                .ToHashSet();
        }

        if (commitmentTimestamp.HasValue && sprintEnd.HasValue)
        {
            foreach (var evt in iterationEvents
                         .Where(e => string.Equals(e.NewValue, sprint.Path, StringComparison.OrdinalIgnoreCase)
                                     && FirstDoneDeliveryLookup.GetEventTimestamp(e) > commitmentTimestamp.Value
                                     && FirstDoneDeliveryLookup.GetEventTimestamp(e) <= sprintEnd.Value))
            {
                addedWorkItemIds.Add(evt.WorkItemId);
            }

            foreach (var evt in iterationEvents
                         .Where(e => string.Equals(e.OldValue, sprint.Path, StringComparison.OrdinalIgnoreCase)
                                     && FirstDoneDeliveryLookup.GetEventTimestamp(e) > commitmentTimestamp.Value
                                     && FirstDoneDeliveryLookup.GetEventTimestamp(e) <= sprintEnd.Value))
            {
                removedEntries.Add((evt.WorkItemId, FirstDoneDeliveryLookup.GetEventTimestamp(evt)));
            }
        }

        var nextSprintPath = await ResolveNextSprintPathAsync(sprintDefinition, cancellationToken);

        // ── Step 8: Load removed items (no longer in sprint iteration) ────────
        var removedWorkItemIds = removedEntries.Select(r => r.WorkItemId).Distinct().ToHashSet();
        // Exclude items that are also currently in the sprint (re-added after removal)
        var currentItemIds = currentSprintItems.Select(w => w.TfsId).ToHashSet();
        removedWorkItemIds.ExceptWith(currentItemIds);

        var removedWorkItems = relevantWorkItems
            .Where(w => removedWorkItemIds.Contains(w.TfsId))
            .ToList();

        // ── Step 9: Classify items ────────────────────────────────────────────
        var completedItems = sprintStart.HasValue && sprintEnd.HasValue
            ? relevantWorkItems
                .Where(w => firstDoneByWorkItem.TryGetValue(w.TfsId, out var firstDoneTimestamp)
                            && firstDoneTimestamp >= sprintStart.Value
                            && firstDoneTimestamp <= sprintEnd.Value)
                .OrderBy(w => firstDoneByWorkItem[w.TfsId])
                .ThenBy(w => w.TfsId)
                .ToList()
            : new List<Persistence.Entities.WorkItemEntity>();

        var completedItemIds = completedItems.Select(w => w.TfsId).ToHashSet();

        var unfinishedItems = currentSprintItems
            .Where(w => !completedItemIds.Contains(w.TfsId))
            .ToList();

        var initialScopeIds = committedWorkItemIds;
        IReadOnlySet<int> spilloverWorkItemIds = sprintEnd.HasValue
            ? SprintSpilloverLookup.BuildSpilloverWorkItemIds(
                committedWorkItemIds,
                workItemSnapshotsById,
                stateEventsByWorkItem,
                iterationEventsByWorkItem,
                stateLookup,
                sprintDefinition,
                nextSprintPath,
                sprintEnd.Value)
            : new HashSet<int>();
        var spilloverItems = relevantWorkItems
            .Where(w => w.Type == PbiType && spilloverWorkItemIds.Contains(w.TfsId))
            .ToList();

        // ── Step 10: Detect starved work ──────────────────────────────────────
        // Starved = items in initial scope that are unfinished, while items added later completed
        var hasLaterAddedCompletions = completedItems.Any(w => addedWorkItemIds.Contains(w.TfsId));
        var starvedItems = hasLaterAddedCompletions
            ? unfinishedItems.Where(w => initialScopeIds.Contains(w.TfsId)).ToList()
            : new List<Persistence.Entities.WorkItemEntity>();

        // ── Step 11: Build entry timestamps for added items ───────────────────
        var enteredSprintDates = new Dictionary<int, DateTimeOffset>();
        if (commitmentTimestamp.HasValue && sprintEnd.HasValue)
        {
            foreach (var entry in iterationEvents
                         .Where(e => string.Equals(e.NewValue, sprint.Path, StringComparison.OrdinalIgnoreCase)
                                     && addedWorkItemIds.Contains(e.WorkItemId)
                                     && FirstDoneDeliveryLookup.GetEventTimestamp(e) > commitmentTimestamp.Value
                                     && FirstDoneDeliveryLookup.GetEventTimestamp(e) <= sprintEnd.Value)
                         .GroupBy(e => e.WorkItemId)
                         .Select(g => new
                         {
                             WorkItemId = g.Key,
                             EnteredDate = g.Min(e => FirstDoneDeliveryLookup.GetEventTimestamp(e))
                         }))
            {
                enteredSprintDates[entry.WorkItemId] = entry.EnteredDate;
            }
        }

        // ── Step 12: Build removal timestamps ─────────────────────────────────
        var removedFromSprintDates = removedEntries
            .GroupBy(r => r.WorkItemId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.Timestamp));

        // ── Step 13: Construct DTOs ───────────────────────────────────────────
        string? ResolveProductName(int tfsId) =>
            productByWorkItem.TryGetValue(tfsId, out var pid) && productNames.TryGetValue(pid, out var name)
                ? name : null;

        int completionOrder = 0;
        var completedPbiDtos = completedItems.Select(w => new SprintExecutionPbiDto
        {
            TfsId = w.TfsId,
            Title = w.Title,
            Effort = w.Effort,
            State = w.State,
            ClosedDate = firstDoneByWorkItem[w.TfsId],
            EnteredSprintDate = enteredSprintDates.GetValueOrDefault(w.TfsId),
            ProductName = ResolveProductName(w.TfsId),
            CompletionOrder = ++completionOrder
        }).ToList();

        var unfinishedPbiDtos = unfinishedItems.Select(w => new SprintExecutionPbiDto
        {
            TfsId = w.TfsId,
            Title = w.Title,
            Effort = w.Effort,
            State = w.State,
            ClosedDate = w.ClosedDate,
            EnteredSprintDate = enteredSprintDates.GetValueOrDefault(w.TfsId),
            ProductName = ResolveProductName(w.TfsId)
        }).ToList();

        var addedPbiDtos = relevantWorkItems
            .Where(w => addedWorkItemIds.Contains(w.TfsId))
            .Select(w => new SprintExecutionPbiDto
            {
                TfsId = w.TfsId,
                Title = w.Title,
                Effort = w.Effort,
                State = w.State,
                ClosedDate = w.ClosedDate,
                EnteredSprintDate = enteredSprintDates.GetValueOrDefault(w.TfsId),
                ProductName = ResolveProductName(w.TfsId)
            }).ToList();

        var removedPbiDtos = removedWorkItems.Select(w => new SprintExecutionPbiDto
        {
            TfsId = w.TfsId,
            Title = w.Title,
            Effort = w.Effort,
            State = w.State,
            ClosedDate = w.ClosedDate,
            RemovedFromSprintDate = removedFromSprintDates.GetValueOrDefault(w.TfsId),
            ProductName = ResolveProductName(w.TfsId)
        }).ToList();

        var spilloverPbiDtos = spilloverItems.Select(w => new SprintExecutionPbiDto
        {
            TfsId = w.TfsId,
            Title = w.Title,
            Effort = w.Effort,
            State = w.State,
            ClosedDate = w.ClosedDate,
            ProductName = ResolveProductName(w.TfsId)
        }).ToList();

        var starvedPbiDtos = starvedItems.Select(w => new SprintExecutionPbiDto
        {
            TfsId = w.TfsId,
            Title = w.Title,
            Effort = w.Effort,
            State = w.State,
            ClosedDate = w.ClosedDate,
            ProductName = ResolveProductName(w.TfsId)
        }).ToList();

        var committedSp = SumStoryPoints(
            relevantWorkItems.Where(w => initialScopeIds.Contains(w.TfsId)),
            stateLookup,
            relevantWorkItemsById,
            excludeDerived: true);
        var addedSp = SumStoryPoints(
            relevantWorkItems.Where(w => addedWorkItemIds.Contains(w.TfsId)),
            stateLookup,
            relevantWorkItemsById,
            excludeDerived: false);
        var removedSp = SumStoryPoints(
            removedWorkItems,
            stateLookup,
            relevantWorkItemsById,
            excludeDerived: false);
        var deliveredSp = SumDeliveredStoryPoints(completedItems, relevantWorkItemsById);
        var deliveredFromAddedSp = SumDeliveredStoryPoints(
            completedItems.Where(w => addedWorkItemIds.Contains(w.TfsId)),
            relevantWorkItemsById);
        var spilloverSp = SumStoryPoints(
            spilloverItems,
            stateLookup,
            relevantWorkItemsById,
            excludeDerived: true);
        var churnRate = SafeDivide(addedSp + removedSp, committedSp + addedSp);
        var commitmentCompletion = SafeDivide(deliveredSp, committedSp - removedSp);
        var spilloverRate = SafeDivide(spilloverSp, committedSp - removedSp);
        var addedDeliveryRate = SafeDivide(deliveredFromAddedSp, addedSp);

        var summary = new SprintExecutionSummaryDto
        {
            InitialScopeCount = initialScopeIds.Count,
            InitialScopeEffort = relevantWorkItems
                .Where(w => initialScopeIds.Contains(w.TfsId))
                .Sum(w => w.Effort ?? 0),
            AddedDuringSprintCount = addedWorkItemIds.Count,
            AddedDuringSprintEffort = relevantWorkItems
                .Where(w => addedWorkItemIds.Contains(w.TfsId))
                .Sum(w => w.Effort ?? 0),
            RemovedDuringSprintCount = removedWorkItemIds.Count,
            RemovedDuringSprintEffort = removedWorkItems.Sum(w => w.Effort ?? 0),
            CompletedCount = completedItems.Count,
            CompletedEffort = completedItems.Sum(w => w.Effort ?? 0),
            UnfinishedCount = unfinishedItems.Count,
            UnfinishedEffort = unfinishedItems.Sum(w => w.Effort ?? 0),
            SpilloverCount = spilloverItems.Count,
            SpilloverEffort = spilloverItems.Sum(w => w.Effort ?? 0),
            CommittedSP = committedSp,
            AddedSP = addedSp,
            RemovedSP = removedSp,
            DeliveredSP = deliveredSp,
            DeliveredFromAddedSP = deliveredFromAddedSp,
            SpilloverSP = spilloverSp,
            ChurnRate = churnRate,
            CommitmentCompletion = commitmentCompletion,
            SpilloverRate = spilloverRate,
            AddedDeliveryRate = addedDeliveryRate,
            StarvedCount = starvedItems.Count
        };

        return new SprintExecutionDto
        {
            SprintId = sprint.Id,
            SprintName = sprint.Name,
            StartUtc = sprint.StartUtc,
            EndUtc = sprint.EndUtc,
            Summary = summary,
            CompletedPbis = completedPbiDtos,
            UnfinishedPbis = unfinishedPbiDtos,
            AddedDuringSprint = addedPbiDtos,
            RemovedDuringSprint = removedPbiDtos,
            SpilloverPbis = spilloverPbiDtos,
            StarvedPbis = starvedPbiDtos,
            HasData = committedWorkItemIds.Count > 0
                || addedWorkItemIds.Count > 0
                || removedWorkItems.Count > 0
                || completedItems.Count > 0
                || currentSprintItems.Count > 0
        };
    }

    private static SprintExecutionDto EmptyResult(
        int sprintId,
        string? sprintName = null,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null)
    {
        return new SprintExecutionDto
        {
            SprintId = sprintId,
            SprintName = sprintName ?? string.Empty,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Summary = new SprintExecutionSummaryDto(),
            CompletedPbis = Array.Empty<SprintExecutionPbiDto>(),
            UnfinishedPbis = Array.Empty<SprintExecutionPbiDto>(),
            AddedDuringSprint = Array.Empty<SprintExecutionPbiDto>(),
            RemovedDuringSprint = Array.Empty<SprintExecutionPbiDto>(),
            SpilloverPbis = Array.Empty<SprintExecutionPbiDto>(),
            StarvedPbis = Array.Empty<SprintExecutionPbiDto>(),
            HasData = false
        };
    }

    private async Task<string?> ResolveNextSprintPathAsync(
        SprintDefinition sprint,
        CancellationToken cancellationToken)
    {
        var teamSprints = await _context.Sprints
            .AsNoTracking()
            .Where(candidate => candidate.TeamId == sprint.TeamId)
            .ToListAsync(cancellationToken);

        return SprintSpilloverLookup.GetNextSprintPath(
            sprint,
            teamSprints.Select(candidate => candidate.ToDefinition()));
    }

    private double SumStoryPoints(
        IEnumerable<WorkItemEntity> workItems,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsById,
        bool excludeDerived)
    {
        return workItems
            .Select(workItem => ResolveStoryPointEstimate(workItem, stateLookup, workItemsById, excludeDerived))
            .Where(estimate => estimate.HasValue)
            .Select(estimate => estimate!.Value)
            .Sum();
    }

    private double SumDeliveredStoryPoints(
        IEnumerable<WorkItemEntity> workItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsById)
    {
        return workItems
            .Select(workItem => ResolveDeliveredStoryPointEstimate(workItem, workItemsById))
            .Where(estimate => estimate.HasValue)
            .Select(estimate => estimate!.Value)
            .Sum();
    }

    private double? ResolveStoryPointEstimate(
        WorkItemEntity workItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsById,
        bool excludeDerived)
    {
        var estimate = _storyPointResolutionService.Resolve(new StoryPointResolutionRequest(
            ToWorkItemDto(workItem),
            StateClassificationLookup.IsDone(stateLookup, workItem.Type, workItem.State),
            BuildFeaturePbiCandidates(workItem, stateLookup, workItemsById)));

        if (!estimate.Value.HasValue || estimate.Source == StoryPointEstimateSource.Missing)
        {
            return null;
        }

        if (excludeDerived && estimate.Source == StoryPointEstimateSource.Derived)
        {
            return null;
        }

        return estimate.Value.Value;
    }

    private double? ResolveDeliveredStoryPointEstimate(
        WorkItemEntity workItem,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsById)
    {
        var estimate = _storyPointResolutionService.Resolve(new StoryPointResolutionRequest(
            ToWorkItemDto(workItem),
            IsDone: true,
            BuildFeaturePbiCandidates(workItem, stateLookup: null, workItemsById)));

        if (!estimate.Value.HasValue || estimate.Source is StoryPointEstimateSource.Missing or StoryPointEstimateSource.Derived)
        {
            return null;
        }

        return estimate.Value.Value;
    }

    private static IReadOnlyCollection<StoryPointResolutionCandidate> BuildFeaturePbiCandidates(
        WorkItemEntity workItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsById)
    {
        if (workItem.ParentTfsId == null)
        {
            return [];
        }

        return workItemsById.Values
            .Where(candidate => candidate.ParentTfsId == workItem.ParentTfsId)
            .Where(candidate => IsPbiType(candidate.Type))
            .Select(candidate => new StoryPointResolutionCandidate(
                ToWorkItemDto(candidate),
                StateClassificationLookup.IsDone(stateLookup, candidate.Type, candidate.State)))
            .ToList();
    }

    private static WorkItemDto ToWorkItemDto(WorkItemEntity workItem)
    {
        return new WorkItemDto(
            TfsId: workItem.TfsId,
            Type: workItem.Type,
            Title: workItem.Title,
            ParentTfsId: workItem.ParentTfsId,
            AreaPath: workItem.AreaPath,
            IterationPath: workItem.IterationPath,
            State: workItem.State,
            RetrievedAt: workItem.RetrievedAt,
            Effort: workItem.Effort,
            Description: workItem.Description,
            CreatedDate: workItem.CreatedDate,
            ClosedDate: workItem.ClosedDate,
            Severity: workItem.Severity,
            Tags: workItem.Tags,
            IsBlocked: workItem.IsBlocked,
            Relations: null,
            ChangedDate: workItem.TfsChangedDate,
            BusinessValue: workItem.BusinessValue,
            BacklogPriority: workItem.BacklogPriority,
            StoryPoints: workItem.StoryPoints);
    }

    private static double SafeDivide(double numerator, double denominator)
    {
        return denominator <= 0d ? 0d : numerator / denominator;
    }

    private static bool IsPbiType(string workItemType)
    {
        return workItemType.Equals(WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.PbiShort, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.UserStory, StringComparison.OrdinalIgnoreCase);
    }
}
