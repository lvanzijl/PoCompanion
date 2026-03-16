using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Portfolio;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.Domain.WorkItems;

namespace PoTool.Api.Services;

public class PortfolioFlowProjectionService
{
    private const string StoryPointsFieldRefName = "Microsoft.VSTS.Scheduling.StoryPoints";
    private const string BusinessValueFieldRefName = "Microsoft.VSTS.Common.BusinessValue";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PortfolioFlowProjectionService> _logger;
    private readonly IWorkItemStateClassificationService? _stateClassificationService;
    private readonly ISprintCompletionService _sprintCompletionService;
    private readonly ICanonicalStoryPointResolutionService _storyPointResolutionService;

    public PortfolioFlowProjectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<PortfolioFlowProjectionService> logger,
        IWorkItemStateClassificationService? stateClassificationService,
        ISprintCompletionService sprintCompletionService,
        ICanonicalStoryPointResolutionService storyPointResolutionService)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(sprintCompletionService);
        ArgumentNullException.ThrowIfNull(storyPointResolutionService);

        _scopeFactory = scopeFactory;
        _logger = logger;
        _stateClassificationService = stateClassificationService;
        _sprintCompletionService = sprintCompletionService;
        _storyPointResolutionService = storyPointResolutionService;
    }

    public virtual async Task<IReadOnlyList<PortfolioFlowProjectionEntity>> ComputeProjectionsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        CancellationToken cancellationToken = default)
    {
        var sprintIdList = sprintIds.Distinct().ToList();
        if (sprintIdList.Count == 0)
        {
            return Array.Empty<PortfolioFlowProjectionEntity>();
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productIds = await context.Products
            .Where(product => product.ProductOwnerId == productOwnerId)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        if (productIds.Count == 0)
        {
            return Array.Empty<PortfolioFlowProjectionEntity>();
        }

        var sprints = await context.Sprints
            .Where(sprint => sprintIdList.Contains(sprint.Id))
            .ToListAsync(cancellationToken);

        var validSprints = sprints
            .Where(sprint => sprint.StartDateUtc != null && sprint.EndDateUtc != null)
            .ToList();

        if (validSprints.Count == 0)
        {
            return Array.Empty<PortfolioFlowProjectionEntity>();
        }

        var currentResolvedItems = await context.ResolvedWorkItems
            .AsNoTracking()
            .Where(resolved => resolved.ResolvedProductId != null && productIds.Contains(resolved.ResolvedProductId.Value))
            .ToListAsync(cancellationToken);

        var productIdStrings = productIds
            .Select(productId => productId.ToString(CultureInfo.InvariantCulture))
            .ToList();

        var membershipEventEntities = await context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(activityEvent =>
                activityEvent.ProductOwnerId == productOwnerId
                && activityEvent.FieldRefName == PortfolioEntryLookup.ResolvedProductIdFieldRefName
                && ((activityEvent.OldValue != null && productIdStrings.Contains(activityEvent.OldValue))
                    || (activityEvent.NewValue != null && productIdStrings.Contains(activityEvent.NewValue))))
            .ToListAsync(cancellationToken);

        var candidateWorkItemIdsByProduct = productIds.ToDictionary(
            productId => productId,
            productId => new HashSet<int>(
                currentResolvedItems
                    .Where(resolved => resolved.ResolvedProductId == productId)
                    .Select(resolved => resolved.WorkItemId)));

        foreach (var membershipEvent in membershipEventEntities)
        {
            var oldProductId = ParseNullableInt(membershipEvent.OldValue);
            var newProductId = ParseNullableInt(membershipEvent.NewValue);

            if (oldProductId.HasValue && candidateWorkItemIdsByProduct.TryGetValue(oldProductId.Value, out var oldProductCandidates))
            {
                oldProductCandidates.Add(membershipEvent.WorkItemId);
            }

            if (newProductId.HasValue && candidateWorkItemIdsByProduct.TryGetValue(newProductId.Value, out var newProductCandidates))
            {
                newProductCandidates.Add(membershipEvent.WorkItemId);
            }
        }

        var candidateWorkItemIds = candidateWorkItemIdsByProduct.Values
            .SelectMany(workItemIds => workItemIds)
            .Distinct()
            .ToList();

        if (candidateWorkItemIds.Count == 0)
        {
            return Array.Empty<PortfolioFlowProjectionEntity>();
        }

        var workItems = await context.WorkItems
            .AsNoTracking()
            .Where(workItem => candidateWorkItemIds.Contains(workItem.TfsId))
            .ToListAsync(cancellationToken);

        var resolvedItems = await context.ResolvedWorkItems
            .AsNoTracking()
            .Where(resolved => candidateWorkItemIds.Contains(resolved.WorkItemId))
            .ToListAsync(cancellationToken);

        var stateEventEntities = await context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(activityEvent =>
                activityEvent.ProductOwnerId == productOwnerId
                && activityEvent.FieldRefName == "System.State"
                && candidateWorkItemIds.Contains(activityEvent.WorkItemId))
            .ToListAsync(cancellationToken);

        var storyPointEventEntities = await context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(activityEvent =>
                activityEvent.ProductOwnerId == productOwnerId
                && activityEvent.FieldRefName == StoryPointsFieldRefName
                && candidateWorkItemIds.Contains(activityEvent.WorkItemId))
            .ToListAsync(cancellationToken);

        var businessValueEventEntities = await context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(activityEvent =>
                activityEvent.ProductOwnerId == productOwnerId
                && activityEvent.FieldRefName == BusinessValueFieldRefName
                && candidateWorkItemIds.Contains(activityEvent.WorkItemId))
            .ToListAsync(cancellationToken);

        var stateLookup = await GetStateLookupAsync(cancellationToken);
        var workItemsById = workItems.ToDictionary(workItem => workItem.TfsId, workItem => workItem);
        var resolvedItemsByWorkItemId = resolvedItems.ToDictionary(resolved => resolved.WorkItemId, resolved => resolved);
        var stateEventsByWorkItem = stateEventEntities.ToFieldChangeEvents().GroupByWorkItemId();
        var storyPointEventsByWorkItem = storyPointEventEntities.ToFieldChangeEvents().GroupByWorkItemId();
        var businessValueEventsByWorkItem = businessValueEventEntities.ToFieldChangeEvents().GroupByWorkItemId();
        var membershipEventsByWorkItem = membershipEventEntities.ToFieldChangeEvents().GroupByWorkItemId();
        var firstDoneByWorkItem = _sprintCompletionService.BuildFirstDoneByWorkItem(
            stateEventEntities.ToFieldChangeEvents(),
            workItems.ToSnapshotDictionary(),
            stateLookup);

        var validSprintIds = validSprints.Select(sprint => sprint.Id).ToList();
        var existingProjections = await context.Set<PortfolioFlowProjectionEntity>()
            .Where(projection => validSprintIds.Contains(projection.SprintId) && productIds.Contains(projection.ProductId))
            .ToListAsync(cancellationToken);

        var existingByKey = existingProjections.ToDictionary(
            projection => (projection.SprintId, projection.ProductId),
            projection => projection);

        var results = new List<PortfolioFlowProjectionEntity>();

        foreach (var sprint in validSprints)
        {
            foreach (var productId in productIds)
            {
                var projection = ComputeProductSprintProjection(
                    sprint,
                    productId,
                    candidateWorkItemIdsByProduct[productId],
                    resolvedItemsByWorkItemId,
                    workItemsById,
                    stateLookup,
                    firstDoneByWorkItem,
                    stateEventsByWorkItem,
                    storyPointEventsByWorkItem,
                    businessValueEventsByWorkItem,
                    membershipEventsByWorkItem);

                projection.ProjectionTimestamp = DateTimeOffset.UtcNow;

                if (existingByKey.TryGetValue((sprint.Id, productId), out var existingProjection))
                {
                    ApplyProjection(existingProjection, projection);
                    existingProjection.ProjectionTimestamp = projection.ProjectionTimestamp;
                    results.Add(existingProjection);
                }
                else
                {
                    context.Add(projection);
                    results.Add(projection);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Computed {ProjectionCount} PortfolioFlow projections for ProductOwner {ProductOwnerId}",
            results.Count,
            productOwnerId);

        return results;
    }

    internal PortfolioFlowProjectionEntity ComputeProductSprintProjection(
        SprintEntity sprint,
        int productId,
        IReadOnlyCollection<int> candidateWorkItemIds,
        IReadOnlyDictionary<int, ResolvedWorkItemEntity> resolvedItemsByWorkItemId,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null,
        IReadOnlyDictionary<int, DateTimeOffset>? firstDoneByWorkItem = null,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? stateEventsByWorkItem = null,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? storyPointEventsByWorkItem = null,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? businessValueEventsByWorkItem = null,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? membershipEventsByWorkItem = null)
    {
        ArgumentNullException.ThrowIfNull(sprint);
        ArgumentNullException.ThrowIfNull(candidateWorkItemIds);
        ArgumentNullException.ThrowIfNull(resolvedItemsByWorkItemId);
        ArgumentNullException.ThrowIfNull(workItemsByTfsId);

        if (sprint.StartDateUtc is null || sprint.EndDateUtc is null)
        {
            throw new ArgumentException("PortfolioFlow projections require sprints with start and end dates.", nameof(sprint));
        }

        var sprintStart = new DateTimeOffset(DateTime.SpecifyKind(sprint.StartDateUtc.Value, DateTimeKind.Utc), TimeSpan.Zero);
        var sprintEnd = new DateTimeOffset(DateTime.SpecifyKind(sprint.EndDateUtc.Value, DateTimeKind.Utc), TimeSpan.Zero);
        var candidatePbiIds = candidateWorkItemIds
            .Where(workItemsByTfsId.ContainsKey)
            .Where(workItemId => CanonicalWorkItemTypes.IsAuthoritativePbi(workItemsByTfsId[workItemId].Type))
            .ToList();

        var stockStoryPoints = 0d;
        var remainingScopeStoryPoints = 0d;
        var inflowStoryPoints = 0d;
        var throughputStoryPoints = 0d;

        foreach (var workItemId in candidatePbiIds)
        {
            var workItem = workItemsByTfsId[workItemId];
            resolvedItemsByWorkItemId.TryGetValue(workItemId, out var resolvedItem);
            var membershipEvents = membershipEventsByWorkItem?.GetValueOrDefault(workItemId);
            var stateEvents = stateEventsByWorkItem?.GetValueOrDefault(workItemId);
            var currentResolvedProductId = resolvedItem?.ResolvedProductId;

            if (GetResolvedProductIdAtTimestamp(currentResolvedProductId, membershipEvents, sprintEnd) == productId)
            {
                var stateAtSprintEnd = StateReconstructionLookup.GetStateAtTimestamp(workItem.State, stateEvents, sprintEnd);
                var stateAtSprintEndClassification = StateClassificationLookup.GetClassification(stateLookup, workItem.Type, stateAtSprintEnd);

                if (stateAtSprintEndClassification != StateClassification.Removed)
                {
                    stockStoryPoints += ResolveStoryPointScopeAtTimestamp(
                        workItem,
                        sprintEnd,
                        candidatePbiIds,
                        workItemsByTfsId,
                        resolvedItemsByWorkItemId,
                        stateLookup,
                        stateAtSprintEndClassification == StateClassification.Done,
                        stateEventsByWorkItem,
                        storyPointEventsByWorkItem,
                        businessValueEventsByWorkItem);
                }

                if (stateAtSprintEndClassification is StateClassification.New or StateClassification.InProgress)
                {
                    remainingScopeStoryPoints += ResolveStoryPointScopeAtTimestamp(
                        workItem,
                        sprintEnd,
                        candidatePbiIds,
                        workItemsByTfsId,
                        resolvedItemsByWorkItemId,
                        stateLookup,
                        isDone: false,
                        stateEventsByWorkItem,
                        storyPointEventsByWorkItem,
                        businessValueEventsByWorkItem);
                }
            }

            if (firstDoneByWorkItem != null
                && firstDoneByWorkItem.TryGetValue(workItemId, out var firstDoneTimestamp)
                && firstDoneTimestamp >= sprintStart
                && firstDoneTimestamp <= sprintEnd
                && GetResolvedProductIdAtTimestamp(currentResolvedProductId, membershipEvents, firstDoneTimestamp) == productId)
            {
                throughputStoryPoints += ResolveStoryPointScopeAtTimestamp(
                    workItem,
                    firstDoneTimestamp,
                    candidatePbiIds,
                    workItemsByTfsId,
                    resolvedItemsByWorkItemId,
                    stateLookup,
                    isDone: true,
                    stateEventsByWorkItem,
                    storyPointEventsByWorkItem,
                    businessValueEventsByWorkItem);
            }

            var enteredPortfolioAt = PortfolioEntryLookup.GetFirstEnteredPortfolioTimestamp(membershipEvents, productId);
            if (enteredPortfolioAt.HasValue
                && enteredPortfolioAt.Value >= sprintStart
                && enteredPortfolioAt.Value <= sprintEnd)
            {
                var stateAtEntry = StateReconstructionLookup.GetStateAtTimestamp(workItem.State, stateEvents, enteredPortfolioAt.Value);
                inflowStoryPoints += ResolveStoryPointScopeAtTimestamp(
                    workItem,
                    enteredPortfolioAt.Value,
                    candidatePbiIds,
                    workItemsByTfsId,
                    resolvedItemsByWorkItemId,
                    stateLookup,
                    StateClassificationLookup.IsDone(stateLookup, workItem.Type, stateAtEntry),
                    stateEventsByWorkItem,
                    storyPointEventsByWorkItem,
                    businessValueEventsByWorkItem);
            }
        }

        return new PortfolioFlowProjectionEntity
        {
            SprintId = sprint.Id,
            ProductId = productId,
            StockStoryPoints = stockStoryPoints,
            RemainingScopeStoryPoints = remainingScopeStoryPoints,
            InflowStoryPoints = inflowStoryPoints,
            ThroughputStoryPoints = throughputStoryPoints,
            CompletionPercent = stockStoryPoints > 0d
                ? ((stockStoryPoints - remainingScopeStoryPoints) / stockStoryPoints) * 100d
                : null
        };
    }

    private double ResolveStoryPointScopeAtTimestamp(
        WorkItemEntity workItem,
        DateTimeOffset targetTimestamp,
        IReadOnlyCollection<int> candidatePbiIds,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<int, ResolvedWorkItemEntity> resolvedItemsByWorkItemId,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        bool isDone,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? stateEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? storyPointEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? businessValueEventsByWorkItem)
    {
        var historicalWorkItem = new CanonicalWorkItem(
            workItem.TfsId,
            workItem.Type,
            workItem.ParentTfsId,
            GetNullableIntAtTimestamp(
                workItem.BusinessValue,
                businessValueEventsByWorkItem?.GetValueOrDefault(workItem.TfsId),
                BusinessValueFieldRefName,
                targetTimestamp),
            GetNullableIntAtTimestamp(
                workItem.StoryPoints,
                storyPointEventsByWorkItem?.GetValueOrDefault(workItem.TfsId),
                StoryPointsFieldRefName,
                targetTimestamp));

        var siblingCandidates = BuildFeaturePbiCandidatesAtTimestamp(
            workItem,
            targetTimestamp,
            candidatePbiIds,
            workItemsByTfsId,
            resolvedItemsByWorkItemId,
            stateLookup,
            stateEventsByWorkItem,
            storyPointEventsByWorkItem,
            businessValueEventsByWorkItem);

        var estimate = _storyPointResolutionService.Resolve(new StoryPointResolutionRequest(
            historicalWorkItem,
            isDone,
            siblingCandidates));

        return estimate.Value ?? 0d;
    }

    private IReadOnlyList<StoryPointResolutionCandidate> BuildFeaturePbiCandidatesAtTimestamp(
        WorkItemEntity workItem,
        DateTimeOffset targetTimestamp,
        IReadOnlyCollection<int> candidatePbiIds,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<int, ResolvedWorkItemEntity> resolvedItemsByWorkItemId,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? stateEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? storyPointEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? businessValueEventsByWorkItem)
    {
        resolvedItemsByWorkItemId.TryGetValue(workItem.TfsId, out var resolvedItem);
        var featureId = resolvedItem?.ResolvedFeatureId ?? workItem.ParentTfsId;

        return candidatePbiIds
            .Where(candidateWorkItemId => workItemsByTfsId.ContainsKey(candidateWorkItemId))
            .Select(candidateWorkItemId => new
            {
                WorkItem = workItemsByTfsId[candidateWorkItemId],
                Resolved = resolvedItemsByWorkItemId.GetValueOrDefault(candidateWorkItemId)
            })
            .Where(candidate => (candidate.Resolved?.ResolvedFeatureId ?? candidate.WorkItem.ParentTfsId) == featureId)
            .Select(candidate =>
            {
                var reconstructedState = StateReconstructionLookup.GetStateAtTimestamp(
                    candidate.WorkItem.State,
                    stateEventsByWorkItem?.GetValueOrDefault(candidate.WorkItem.TfsId),
                    targetTimestamp);

                return new StoryPointResolutionCandidate(
                    new CanonicalWorkItem(
                        candidate.WorkItem.TfsId,
                        candidate.WorkItem.Type,
                        candidate.WorkItem.ParentTfsId,
                        GetNullableIntAtTimestamp(
                            candidate.WorkItem.BusinessValue,
                            businessValueEventsByWorkItem?.GetValueOrDefault(candidate.WorkItem.TfsId),
                            BusinessValueFieldRefName,
                            targetTimestamp),
                        GetNullableIntAtTimestamp(
                            candidate.WorkItem.StoryPoints,
                            storyPointEventsByWorkItem?.GetValueOrDefault(candidate.WorkItem.TfsId),
                            StoryPointsFieldRefName,
                            targetTimestamp)),
                    StateClassificationLookup.IsDone(stateLookup, candidate.WorkItem.Type, reconstructedState));
            })
            .ToList();
    }

    private async Task<IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>> GetStateLookupAsync(
        CancellationToken cancellationToken)
    {
        if (_stateClassificationService == null)
        {
            return StateClassificationLookup.Default;
        }

        var response = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
        return StateClassificationLookup.Create(response.Classifications.ToDomainStateClassifications());
    }

    private static void ApplyProjection(
        PortfolioFlowProjectionEntity target,
        PortfolioFlowProjectionEntity source)
    {
        target.StockStoryPoints = source.StockStoryPoints;
        target.RemainingScopeStoryPoints = source.RemainingScopeStoryPoints;
        target.InflowStoryPoints = source.InflowStoryPoints;
        target.ThroughputStoryPoints = source.ThroughputStoryPoints;
        target.CompletionPercent = source.CompletionPercent;
    }

    private static int? GetResolvedProductIdAtTimestamp(
        int? currentResolvedProductId,
        IReadOnlyList<FieldChangeEvent>? membershipEvents,
        DateTimeOffset targetTimestamp)
    {
        var reconstructedProductId = currentResolvedProductId;

        if (membershipEvents == null || membershipEvents.Count == 0)
        {
            return reconstructedProductId;
        }

        foreach (var membershipEvent in membershipEvents
                     .Where(activityEvent => string.Equals(
                         activityEvent.FieldRefName,
                         PortfolioEntryLookup.ResolvedProductIdFieldRefName,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(activityEvent => activityEvent.Timestamp > targetTimestamp)
                     .OrderByDescending(activityEvent => activityEvent.TimestampUtc)
                     .ThenByDescending(activityEvent => activityEvent.EventId)
                     .ThenByDescending(activityEvent => activityEvent.UpdateId))
        {
            reconstructedProductId = ParseNullableInt(membershipEvent.OldValue);
        }

        return reconstructedProductId;
    }

    private static int? GetNullableIntAtTimestamp(
        int? currentValue,
        IReadOnlyList<FieldChangeEvent>? fieldEvents,
        string fieldRefName,
        DateTimeOffset targetTimestamp)
    {
        var reconstructedValue = currentValue;

        if (fieldEvents == null || fieldEvents.Count == 0)
        {
            return reconstructedValue;
        }

        foreach (var fieldEvent in fieldEvents
                     .Where(activityEvent => string.Equals(activityEvent.FieldRefName, fieldRefName, StringComparison.OrdinalIgnoreCase))
                     .Where(activityEvent => activityEvent.Timestamp > targetTimestamp)
                     .OrderByDescending(activityEvent => activityEvent.TimestampUtc)
                     .ThenByDescending(activityEvent => activityEvent.EventId)
                     .ThenByDescending(activityEvent => activityEvent.UpdateId))
        {
            reconstructedValue = ParseNullableInt(fieldEvent.OldValue);
        }

        return reconstructedValue;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
