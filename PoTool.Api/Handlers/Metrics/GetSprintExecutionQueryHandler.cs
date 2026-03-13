using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetSprintExecutionQuery.
///
/// Reconstructs sprint backlog evolution from cached work items and activity events.
/// Derives initial scope, added/removed items, completion order, and starved work
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
    private readonly ILogger<GetSprintExecutionQueryHandler> _logger;

    public GetSprintExecutionQueryHandler(
        PoToolDbContext context,
        IWorkItemStateClassificationService stateClassificationService,
        ILogger<GetSprintExecutionQueryHandler> logger)
    {
        _context = context;
        _stateClassificationService = stateClassificationService;
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

        // ── Step 4: Load current work items in the sprint iteration path ──────
        var currentSprintItems = await _context.WorkItems
            .AsNoTracking()
            .Where(w => w.IterationPath == sprint.Path
                        && (w.Type == PbiType || w.Type == BugType)
                        && resolvedWorkItemIds.Contains(w.TfsId))
            .ToListAsync(cancellationToken);

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

        var addedWorkItemIds = new HashSet<int>();
        var removedEntries = new List<(int WorkItemId, DateTimeOffset Timestamp)>();

        if (sprintStart.HasValue && sprintEnd.HasValue)
        {
            var startUtc = sprintStart.Value.UtcDateTime;
            var endUtc = sprintEnd.Value.UtcDateTime;

            // Find items added TO this sprint during the sprint window
            var addedEvents = await _context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => e.ProductOwnerId == query.ProductOwnerId
                            && e.FieldRefName == IterationPathField
                            && e.NewValue == sprint.Path
                            && e.EventTimestampUtc >= startUtc
                            && e.EventTimestampUtc <= endUtc
                            && resolvedWorkItemIds.Contains(e.WorkItemId))
                .Select(e => new { e.WorkItemId, e.EventTimestamp })
                .ToListAsync(cancellationToken);

            foreach (var evt in addedEvents)
            {
                addedWorkItemIds.Add(evt.WorkItemId);
            }

            // Find items removed FROM this sprint during the sprint window
            var removedEvents = await _context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => e.ProductOwnerId == query.ProductOwnerId
                            && e.FieldRefName == IterationPathField
                            && e.OldValue == sprint.Path
                            && e.EventTimestampUtc >= startUtc
                            && e.EventTimestampUtc <= endUtc
                            && resolvedWorkItemIds.Contains(e.WorkItemId))
                .Select(e => new { e.WorkItemId, e.EventTimestamp })
                .ToListAsync(cancellationToken);

            foreach (var evt in removedEvents)
            {
                removedEntries.Add((evt.WorkItemId, evt.EventTimestamp));
            }
        }

        // ── Step 8: Load removed items (no longer in sprint iteration) ────────
        var removedWorkItemIds = removedEntries.Select(r => r.WorkItemId).Distinct().ToHashSet();
        // Exclude items that are also currently in the sprint (re-added after removal)
        var currentItemIds = currentSprintItems.Select(w => w.TfsId).ToHashSet();
        removedWorkItemIds.ExceptWith(currentItemIds);

        var removedWorkItems = removedWorkItemIds.Count > 0
            ? await _context.WorkItems
                .AsNoTracking()
                .Where(w => removedWorkItemIds.Contains(w.TfsId))
                .ToListAsync(cancellationToken)
            : new List<Persistence.Entities.WorkItemEntity>();

        // ── Step 9: Classify items ────────────────────────────────────────────
        var completedItems = currentSprintItems
            .Where(w => StateClassificationLookup.IsDone(stateLookup, w.Type, w.State))
            .OrderBy(w => w.ClosedDate ?? DateTimeOffset.MaxValue)
            .ToList();

        var unfinishedItems = currentSprintItems
            .Where(w => !StateClassificationLookup.IsDone(stateLookup, w.Type, w.State))
            .ToList();

        // Items in initial scope = current items NOT in addedDuringSprint + removed items NOT in addedDuringSprint
        var initialScopeIds = currentItemIds
            .Where(id => !addedWorkItemIds.Contains(id))
            .Concat(removedWorkItemIds.Where(id => !addedWorkItemIds.Contains(id)))
            .ToHashSet();

        // ── Step 10: Detect starved work ──────────────────────────────────────
        // Starved = items in initial scope that are unfinished, while items added later completed
        var hasLaterAddedCompletions = completedItems.Any(w => addedWorkItemIds.Contains(w.TfsId));
        var starvedItems = hasLaterAddedCompletions
            ? unfinishedItems.Where(w => initialScopeIds.Contains(w.TfsId)).ToList()
            : new List<Persistence.Entities.WorkItemEntity>();

        // ── Step 11: Build entry timestamps for added items ───────────────────
        var enteredSprintDates = new Dictionary<int, DateTimeOffset>();
        if (sprintStart.HasValue && sprintEnd.HasValue)
        {
            // Fetch raw rows first; SQLite cannot apply Min on DateTimeOffset in SQL.
            var addedItemEvents = await _context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => e.ProductOwnerId == query.ProductOwnerId
                            && e.FieldRefName == IterationPathField
                            && e.NewValue == sprint.Path
                            && addedWorkItemIds.Contains(e.WorkItemId))
                .Select(e => new { e.WorkItemId, e.EventTimestamp })
                .ToListAsync(cancellationToken);

            foreach (var entry in addedItemEvents
                         .GroupBy(e => e.WorkItemId)
                         .Select(g => new { WorkItemId = g.Key, EnteredDate = g.Min(e => e.EventTimestamp) }))
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
            ClosedDate = w.ClosedDate,
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

        var addedPbiDtos = currentSprintItems
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

        var starvedPbiDtos = starvedItems.Select(w => new SprintExecutionPbiDto
        {
            TfsId = w.TfsId,
            Title = w.Title,
            Effort = w.Effort,
            State = w.State,
            ClosedDate = w.ClosedDate,
            ProductName = ResolveProductName(w.TfsId)
        }).ToList();

        var summary = new SprintExecutionSummaryDto
        {
            InitialScopeCount = initialScopeIds.Count,
            InitialScopeEffort = currentSprintItems
                .Where(w => initialScopeIds.Contains(w.TfsId))
                .Concat(removedWorkItems.Where(w => initialScopeIds.Contains(w.TfsId)))
                .Sum(w => w.Effort ?? 0),
            AddedDuringSprintCount = addedWorkItemIds.Count,
            AddedDuringSprintEffort = currentSprintItems
                .Where(w => addedWorkItemIds.Contains(w.TfsId))
                .Sum(w => w.Effort ?? 0),
            RemovedDuringSprintCount = removedWorkItemIds.Count,
            RemovedDuringSprintEffort = removedWorkItems.Sum(w => w.Effort ?? 0),
            CompletedCount = completedItems.Count,
            CompletedEffort = completedItems.Sum(w => w.Effort ?? 0),
            UnfinishedCount = unfinishedItems.Count,
            UnfinishedEffort = unfinishedItems.Sum(w => w.Effort ?? 0),
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
            StarvedPbis = starvedPbiDtos,
            HasData = currentSprintItems.Count > 0 || removedWorkItems.Count > 0
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
            StarvedPbis = Array.Empty<SprintExecutionPbiDto>(),
            HasData = false
        };
    }
}
