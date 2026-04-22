using Microsoft.EntityFrameworkCore;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.ExecutionRealityCheck;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Reconstructs the Phase 23c execution reality-check CDC slice from persisted sprint and activity history.
/// </summary>
public sealed class ExecutionRealityCheckCdcSliceService
{
    private const string StateFieldRefName = "System.State";
    private const string IterationPathFieldRefName = "System.IterationPath";
    private const string PbiType = "Product Backlog Item";
    private const string BugType = "Bug";

    private readonly PoToolDbContext _context;
    private readonly ILogger<ExecutionRealityCheckCdcSliceService> _logger;
    private readonly IWorkItemStateClassificationService? _stateClassificationService;
    private readonly ISprintSpilloverService _sprintSpilloverService;
    private readonly ISprintFactService _sprintFactService;
    private readonly ISprintExecutionMetricsCalculator _metricsCalculator;
    private readonly IExecutionRealityCheckCdcSliceProjector _projector;
    private readonly TimeProvider _timeProvider;

    public ExecutionRealityCheckCdcSliceService(
        PoToolDbContext context,
        ILogger<ExecutionRealityCheckCdcSliceService> logger,
        IWorkItemStateClassificationService? stateClassificationService,
        ISprintSpilloverService sprintSpilloverService,
        ISprintFactService sprintFactService,
        ISprintExecutionMetricsCalculator metricsCalculator,
        IExecutionRealityCheckCdcSliceProjector projector,
        TimeProvider timeProvider)
    {
        _context = context;
        _logger = logger;
        _stateClassificationService = stateClassificationService;
        _sprintSpilloverService = sprintSpilloverService;
        _sprintFactService = sprintFactService;
        _metricsCalculator = metricsCalculator;
        _projector = projector;
        _timeProvider = timeProvider;
    }

    public async Task<ExecutionRealityCheckCdcSliceResult> BuildAsync(
        int productOwnerId,
        int anchorSprintId,
        IReadOnlyList<int>? effectiveProductIds = null,
        CancellationToken cancellationToken = default)
    {
        var anchorSprint = await _context.Sprints
            .AsNoTracking()
            .FirstOrDefaultAsync(sprint => sprint.Id == anchorSprintId, cancellationToken);
        if (anchorSprint == null)
        {
            _logger.LogWarning(
                "Execution reality-check slice could not resolve anchor sprint {SprintId}",
                anchorSprintId);
            return ExecutionRealityCheckCdcSliceResult.InsufficientEvidence();
        }

        var productIds = effectiveProductIds?.Count > 0
            ? effectiveProductIds.Distinct().ToList()
            : await _context.Products
                .AsNoTracking()
                .Where(product => product.ProductOwnerId == productOwnerId)
                .Select(product => product.Id)
                .ToListAsync(cancellationToken);
        if (productIds.Count == 0)
        {
            _logger.LogWarning(
                "Execution reality-check slice found no products for ProductOwner {ProductOwnerId}",
                productOwnerId);
            return ExecutionRealityCheckCdcSliceResult.InsufficientEvidence();
        }

        var teamSprints = await _context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.TeamId == anchorSprint.TeamId)
            .ToListAsync(cancellationToken);
        var orderedWindow = SelectCompletedWindow(teamSprints, _timeProvider.GetUtcNow().UtcDateTime);
        if (orderedWindow.Count != ExecutionRealityCheckCdcSliceProjector.RequiredWindowSize)
        {
            return ExecutionRealityCheckCdcSliceResult.InsufficientEvidence();
        }
        if (orderedWindow.Any(static sprint => !HasCompleteSprintWindow(sprint)))
        {
            return ExecutionRealityCheckCdcSliceResult.InsufficientEvidence();
        }

        var sprintDefinitionsById = orderedWindow
            .ToDictionary(sprint => sprint.Id, CreateSprintDefinition);

        var teamSprintDefinitions = teamSprints
            .Where(HasCompleteSprintWindow)
            .Select(CreateSprintDefinition)
            .ToList();
        if (!HasContinuousOrdering(orderedWindow, sprintDefinitionsById, teamSprintDefinitions))
        {
            return ExecutionRealityCheckCdcSliceResult.InsufficientEvidence();
        }

        var resolvedItems = await _context.ResolvedWorkItems
            .AsNoTracking()
            .Where(item => item.ResolvedProductId != null
                && productIds.Contains(item.ResolvedProductId.Value)
                && (item.WorkItemType == PbiType || item.WorkItemType == BugType))
            .ToListAsync(cancellationToken);
        var resolvedWorkItemIds = resolvedItems.Select(item => item.WorkItemId).Distinct().ToList();
        if (resolvedWorkItemIds.Count == 0)
        {
            return ExecutionRealityCheckCdcSliceResult.InsufficientEvidence();
        }

        var workItems = await _context.WorkItems
            .AsNoTracking()
            .Where(workItem => resolvedWorkItemIds.Contains(workItem.TfsId)
                && (workItem.Type == PbiType || workItem.Type == BugType))
            .ToListAsync(cancellationToken);
        if (workItems.Count == 0)
        {
            return ExecutionRealityCheckCdcSliceResult.InsufficientEvidence();
        }

        var earliestSprintStartUtc = orderedWindow[0].StartDateUtc!.Value;
        var latestSprintEndUtc = orderedWindow[^1].EndDateUtc!.Value;
        var stateLookup = await GetStateLookupAsync(cancellationToken);

        var stateEvents = await _context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.ProductOwnerId == productOwnerId
                && entry.FieldRefName == StateFieldRefName
                && entry.EventTimestampUtc <= latestSprintEndUtc
                && resolvedWorkItemIds.Contains(entry.WorkItemId))
            .ToListAsync(cancellationToken);
        var iterationEvents = await _context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.ProductOwnerId == productOwnerId
                && entry.FieldRefName == IterationPathFieldRefName
                && entry.EventTimestampUtc >= earliestSprintStartUtc
                && resolvedWorkItemIds.Contains(entry.WorkItemId))
            .ToListAsync(cancellationToken);

        var workItemSnapshotsById = workItems.ToSnapshotDictionary();
        var canonicalWorkItemsById = workItems.ToDictionary(
            workItem => workItem.TfsId,
            workItem => workItem.ToCanonicalWorkItem());
        var stateEventsByWorkItem = stateEvents.ToFieldChangeEvents().GroupByWorkItemId();
        var iterationEventsByWorkItem = iterationEvents.ToFieldChangeEvents().GroupByWorkItemId();
        var rows = new List<ExecutionRealityCheckWindowRow>(orderedWindow.Count);

        foreach (var sprint in orderedWindow)
        {
            var sprintDefinition = sprintDefinitionsById[sprint.Id];
            var nextSprintPath = _sprintSpilloverService.GetNextSprintPath(sprintDefinition, teamSprintDefinitions);
            var sprintFact = _sprintFactService.BuildSprintFactResult(
                sprintDefinition,
                canonicalWorkItemsById,
                workItemSnapshotsById,
                iterationEventsByWorkItem,
                stateEventsByWorkItem,
                stateLookup,
                nextSprintPath);
            var metrics = _metricsCalculator.Calculate(new PoTool.Core.Domain.Metrics.SprintExecutionMetricsInput(
                sprintFact.CommittedStoryPoints,
                sprintFact.AddedStoryPoints,
                sprintFact.RemovedStoryPoints,
                sprintFact.DeliveredStoryPoints,
                sprintFact.DeliveredFromAddedStoryPoints,
                sprintFact.SpilloverStoryPoints));
            var denominator = sprintFact.CommittedStoryPoints - sprintFact.RemovedStoryPoints;

            rows.Add(new ExecutionRealityCheckWindowRow(
                sprint.Id,
                sprint.Path,
                sprint.TeamId,
                DateTime.SpecifyKind(sprint.StartDateUtc!.Value, DateTimeKind.Utc),
                DateTime.SpecifyKind(sprint.EndDateUtc!.Value, DateTimeKind.Utc),
                metrics.CommitmentCompletion,
                metrics.SpilloverRate,
                HasAuthoritativeDenominator: denominator > 0d,
                HasContinuousOrdering: true));
        }

        return _projector.TryProject(rows);
    }

    private static List<SprintEntity> SelectCompletedWindow(
        IReadOnlyList<SprintEntity> teamSprints,
        DateTime nowUtc)
    {
        return teamSprints
            .Where(HasCompleteSprintWindow)
            .Where(sprint => sprint.EndDateUtc!.Value < nowUtc)
            .OrderBy(sprint => sprint.StartDateUtc)
            .ThenBy(sprint => sprint.Id)
            .TakeLast(ExecutionRealityCheckCdcSliceProjector.RequiredWindowSize)
            .ToList();
    }

    private bool HasContinuousOrdering(
        IReadOnlyList<SprintEntity> orderedWindow,
        IReadOnlyDictionary<int, SprintDefinition> sprintDefinitionsById,
        IReadOnlyList<SprintDefinition> teamSprintDefinitions)
    {
        for (var index = 0; index < orderedWindow.Count; index++)
        {
            var sprint = orderedWindow[index];
            var sprintDefinition = sprintDefinitionsById[sprint.Id];
            var nextSprintPath = _sprintSpilloverService.GetNextSprintPath(sprintDefinition, teamSprintDefinitions);

            if (index < orderedWindow.Count - 1)
            {
                if (!string.Equals(nextSprintPath, orderedWindow[index + 1].Path, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(nextSprintPath))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasCompleteSprintWindow(SprintEntity sprint)
    {
        return sprint.StartDateUtc.HasValue && sprint.EndDateUtc.HasValue;
    }

    private static SprintDefinition CreateSprintDefinition(SprintEntity sprint)
    {
        ArgumentNullException.ThrowIfNull(sprint);

        if (!HasCompleteSprintWindow(sprint))
        {
            throw new InvalidOperationException("Execution reality-check slice requires sprints with both start and end dates.");
        }

        var startUtc = sprint.StartUtc
            ?? new DateTimeOffset(DateTime.SpecifyKind(sprint.StartDateUtc!.Value, DateTimeKind.Utc), TimeSpan.Zero);
        var endUtc = sprint.EndUtc
            ?? new DateTimeOffset(DateTime.SpecifyKind(sprint.EndDateUtc!.Value, DateTimeKind.Utc), TimeSpan.Zero);

        return new SprintDefinition(
            sprint.Id,
            sprint.TeamId,
            sprint.Path,
            sprint.Name,
            startUtc,
            endUtc);
    }

    private async Task<IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>> GetStateLookupAsync(
        CancellationToken cancellationToken)
    {
        if (_stateClassificationService == null)
        {
            return StateClassificationLookup.Default;
        }

        var response = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
        return StateClassificationLookup.Create(response.Classifications.ToCanonicalDomainStateClassifications());
    }
}
