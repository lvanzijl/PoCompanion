using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;
using PoTool.Shared.WorkItems;
using SprintMetrics = PoTool.Core.Domain.Metrics;

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
    private readonly ISprintCommitmentService _sprintCommitmentService;
    private readonly ISprintScopeChangeService _sprintScopeChangeService;
    private readonly ISprintCompletionService _sprintCompletionService;
    private readonly ISprintSpilloverService _sprintSpilloverService;
    private readonly ISprintFactService _sprintFactService;
    private readonly SprintMetrics.ISprintExecutionMetricsCalculator _sprintExecutionMetricsCalculator;
    private readonly ILogger<GetSprintExecutionQueryHandler> _logger;

    public GetSprintExecutionQueryHandler(
        PoToolDbContext context,
        IWorkItemStateClassificationService stateClassificationService,
        ISprintCommitmentService sprintCommitmentService,
        ISprintScopeChangeService sprintScopeChangeService,
        ISprintCompletionService sprintCompletionService,
        ISprintSpilloverService sprintSpilloverService,
        ISprintFactService sprintFactService,
        SprintMetrics.ISprintExecutionMetricsCalculator sprintExecutionMetricsCalculator,
        ILogger<GetSprintExecutionQueryHandler> logger)
    {
        _context = context;
        _stateClassificationService = stateClassificationService;
        _sprintCommitmentService = sprintCommitmentService;
        _sprintScopeChangeService = sprintScopeChangeService;
        _sprintCompletionService = sprintCompletionService;
        _sprintSpilloverService = sprintSpilloverService;
        _sprintFactService = sprintFactService;
        _sprintExecutionMetricsCalculator = sprintExecutionMetricsCalculator;
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

        var workItemSnapshotsById = relevantWorkItems.ToSnapshotDictionary();
        var canonicalWorkItemsById = relevantWorkItems.ToDictionary(workItem => workItem.TfsId, workItem => workItem.ToCanonicalWorkItem());
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
        var stateLookup = StateClassificationLookup.Create(classifications.Classifications.ToDomainStateClassifications());

        // ── Step 7: Detect sprint additions and removals from activity events ─
        var sprintStart = sprint.StartUtc;
        var sprintEnd = sprint.EndUtc;
        var commitmentTimestamp = sprintStart.HasValue
            ? _sprintCommitmentService.GetCommitmentTimestamp(sprintStart.Value)
            : (DateTimeOffset?)null;
        var firstDoneByWorkItem = new Dictionary<int, DateTimeOffset>();
        var committedWorkItemIds = new HashSet<int>();
        var addedEntries = new List<SprintScopeAdded>();
        var addedWorkItemIds = new HashSet<int>();
        var removedEntries = new List<(int WorkItemId, DateTimeOffset Timestamp)>();
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

            firstDoneByWorkItem = _sprintCompletionService.BuildFirstDoneByWorkItem(
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

            iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>(iterationFieldChanges.GroupByWorkItemId());
        }

        if (commitmentTimestamp.HasValue)
        {
            committedWorkItemIds = _sprintCommitmentService.BuildCommittedWorkItemIds(
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
            addedEntries = _sprintScopeChangeService.DetectScopeAdded(sprintDefinition, iterationEventsByWorkItem).ToList();
            addedWorkItemIds = addedEntries
                .Select(entry => entry.WorkItemId)
                .ToHashSet();

            removedEntries = _sprintScopeChangeService
                .DetectScopeRemoved(sprintDefinition, iterationEventsByWorkItem)
                .Select(entry => (entry.WorkItemId, entry.RemovedAt))
                .ToList();
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
            ? _sprintSpilloverService.BuildSpilloverWorkItemIds(
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
        var sprintFact = _sprintFactService.BuildSprintFactResult(
            sprintDefinition,
            canonicalWorkItemsById,
            workItemSnapshotsById,
            iterationEventsByWorkItem,
            stateEventsByWorkItem,
            stateLookup,
            nextSprintPath);

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
            foreach (var entry in addedEntries
                         .GroupBy(scopeChange => scopeChange.WorkItemId)
                         .Select(group => new
                         {
                             WorkItemId = group.Key,
                             EnteredDate = group.Min(scopeChange => scopeChange.AddedAt)
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

        var metrics = _sprintExecutionMetricsCalculator.Calculate(new SprintMetrics.SprintExecutionMetricsInput(
            sprintFact.CommittedStoryPoints,
            sprintFact.AddedStoryPoints,
            sprintFact.RemovedStoryPoints,
            sprintFact.DeliveredStoryPoints,
            sprintFact.DeliveredFromAddedStoryPoints,
            sprintFact.SpilloverStoryPoints));

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
            CommittedSP = sprintFact.CommittedStoryPoints,
            AddedSP = sprintFact.AddedStoryPoints,
            RemovedSP = sprintFact.RemovedStoryPoints,
            DeliveredSP = sprintFact.DeliveredStoryPoints,
            DeliveredFromAddedSP = sprintFact.DeliveredFromAddedStoryPoints,
            SpilloverSP = sprintFact.SpilloverStoryPoints,
            RemainingStoryPoints = sprintFact.RemainingStoryPoints,
            ChurnRate = metrics.ChurnRate,
            CommitmentCompletion = metrics.CommitmentCompletion,
            SpilloverRate = metrics.SpilloverRate,
            AddedDeliveryRate = metrics.AddedDeliveryRate,
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

        return _sprintSpilloverService.GetNextSprintPath(
            sprint,
            teamSprints.Select(candidate => candidate.ToDefinition()));
    }

    private static bool IsPbiType(string workItemType)
    {
        return workItemType.Equals(WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.PbiShort, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.UserStory, StringComparison.OrdinalIgnoreCase);
    }
}
