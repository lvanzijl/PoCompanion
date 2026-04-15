using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.Health;
using PoTool.Shared.Metrics;
using PoTool.Shared.PullRequests;
using PoTool.Shared.WorkItems;

namespace PoTool.Client.Services;

public sealed class WorkspaceSignalService
{
    private const int TrendSprintCount = 2;
    private const int CapacitySprintCount = 6;
    private const int PlanningReadyPbiThreshold = 3;
    private const int DeliveryContextLoadBatchSize = 8;
    private static readonly WorkspaceSignalSet NeutralSignals = new(
        "Confirm backlog is healthy",
        "Confirm delivery is on track",
        "Confirm trends are stable",
        "Confirm planning is healthy");

    private readonly IMetricsClient _metricsClient;
    private readonly IPullRequestsClient _pullRequestsClient;
    private readonly SprintService _sprintService;
    private readonly WorkItemService _workItemService;
    private readonly ILogger<WorkspaceSignalService> _logger;

    public IReadOnlyList<CanonicalFilterMetadata> LatestDeliveryFilterMetadata { get; private set; } = Array.Empty<CanonicalFilterMetadata>();

    public IReadOnlyList<CanonicalFilterMetadata> LatestTrendFilterMetadata { get; private set; } = Array.Empty<CanonicalFilterMetadata>();

    public IReadOnlyList<CanonicalFilterMetadata> LatestPlanningFilterMetadata { get; private set; } = Array.Empty<CanonicalFilterMetadata>();

    public WorkspaceSignalService(
        IMetricsClient metricsClient,
        IPullRequestsClient pullRequestsClient,
        SprintService sprintService,
        WorkItemService workItemService,
        ILogger<WorkspaceSignalService> logger)
    {
        _metricsClient = metricsClient;
        _pullRequestsClient = pullRequestsClient;
        _sprintService = sprintService;
        _workItemService = workItemService;
        _logger = logger;
    }

    public async Task<WorkspaceSignalSet> GetSignalsAsync(
        int productOwnerId,
        IReadOnlyCollection<ProductDto> products,
        FilterState requestedState,
        CancellationToken cancellationToken = default)
    {
        var healthTask = GetHealthSignalAsync(products, requestedState, cancellationToken);
        var deliveryTask = GetDeliverySignalAsync(productOwnerId, products, requestedState, cancellationToken);
        var trendsTask = GetTrendsSignalAsync(productOwnerId, products, requestedState, cancellationToken);
        var planningTask = GetPlanningSignalAsync(productOwnerId, products, requestedState, cancellationToken);

        await Task.WhenAll(healthTask, deliveryTask, trendsTask, planningTask);

        return new WorkspaceSignalSet(
            (await healthTask).Data ?? NeutralSignals.Health,
            (await deliveryTask).Data ?? NeutralSignals.Delivery,
            (await trendsTask).Data ?? NeutralSignals.Trends,
            (await planningTask).Data ?? NeutralSignals.Planning);
    }

    public async Task<DataStateResult<string>> GetHealthSignalAsync(
        IReadOnlyCollection<ProductDto> products,
        FilterState requestedState,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scope = GlobalProductSelectionHelper.ResolveEffectiveScope(requestedState, products);
        if (scope.HasInvalidSelection)
        {
            return DataStateResult<string>.Invalid(scope.Reason);
        }

        if (scope.Products.Count == 0)
        {
            return DataStateResult<string>.Empty("No products are available for the current signal scope.");
        }

        var summary = await _workItemService.GetValidationTriageSummaryResultAsync(
            scope.EffectiveProductIds.ToArray(),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return MapSignalResult(summary, SelectHealthSignal);
    }

    public async Task<DataStateResult<string>> GetDeliverySignalAsync(
        int productOwnerId,
        IReadOnlyCollection<ProductDto> products,
        FilterState requestedState,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scope = GlobalProductSelectionHelper.ResolveEffectiveScope(requestedState, products);
        if (scope.HasInvalidSelection)
        {
            LatestDeliveryFilterMetadata = Array.Empty<CanonicalFilterMetadata>();
            return DataStateResult<string>.Invalid(scope.Reason);
        }

        if (scope.Products.Count == 0)
        {
            LatestDeliveryFilterMetadata = Array.Empty<CanonicalFilterMetadata>();
            return DataStateResult<string>.Empty("No products are available for the current signal scope.");
        }

        var teamIds = scope.Products
            .SelectMany(product => product.TeamIds)
            .Distinct()
            .ToArray();

        var currentSprints = await LoadCurrentSprintsAsync(teamIds);
        var deliveryContextsResult = await LoadDeliveryContextsAsync(
            productOwnerId,
            scope.Products,
            currentSprints,
            cancellationToken);
        LatestDeliveryFilterMetadata = deliveryContextsResult.Metadata;

        cancellationToken.ThrowIfCancellationRequested();
        return MapSignalResult(
            deliveryContextsResult,
            contexts => SelectDeliverySignal(contexts, DateTimeOffset.UtcNow));
    }

    public async Task<DataStateResult<string>> GetTrendsSignalAsync(
        int productOwnerId,
        IReadOnlyCollection<ProductDto> products,
        FilterState requestedState,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scope = GlobalProductSelectionHelper.ResolveEffectiveScope(requestedState, products);
        if (scope.HasInvalidSelection)
        {
            LatestTrendFilterMetadata = Array.Empty<CanonicalFilterMetadata>();
            return DataStateResult<string>.Invalid(scope.Reason);
        }

        if (scope.Products.Count == 0)
        {
            LatestTrendFilterMetadata = Array.Empty<CanonicalFilterMetadata>();
            return DataStateResult<string>.Empty("No products are available for the current signal scope.");
        }

        var productIds = scope.Products.Select(product => product.Id).ToArray();
        var teamIds = scope.Products
            .SelectMany(product => product.TeamIds)
            .Distinct()
            .ToArray();

        var recentSprints = await LoadRecentSprintsAsync(teamIds);
        var trendSprintIds = recentSprints
            .Take(TrendSprintCount)
            .Select(sprint => sprint.Id)
            .ToArray();
        var productIdsCsv = string.Join(",", productIds);

        var sprintTrendTask = LoadSprintTrendsAsync(productOwnerId, trendSprintIds, cancellationToken);
        var prTrendTask = LoadPrTrendsAsync(trendSprintIds, productIdsCsv, cancellationToken);

        await Task.WhenAll(sprintTrendTask, prTrendTask);

        var sprintTrendResponse = await sprintTrendTask;
        var prTrendResponse = await prTrendTask;
        LatestTrendFilterMetadata = sprintTrendResponse.Metadata
            .Concat(prTrendResponse.Metadata)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();
        if (sprintTrendResponse.CanUseData || prTrendResponse.CanUseData)
        {
            return DataStateResult<string>.Ready(
                SelectTrendsSignal(sprintTrendResponse.Data, prTrendResponse.Data),
                LatestTrendFilterMetadata);
        }

        return AggregateSignalStates(
            NeutralSignals.Trends,
            "No trend signal data matched the current scope.",
            sprintTrendResponse,
            prTrendResponse);
    }

    public async Task<DataStateResult<string>> GetPlanningSignalAsync(
        int productOwnerId,
        IReadOnlyCollection<ProductDto> products,
        FilterState requestedState,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scope = GlobalProductSelectionHelper.ResolveEffectiveScope(requestedState, products);
        if (scope.HasInvalidSelection)
        {
            LatestPlanningFilterMetadata = Array.Empty<CanonicalFilterMetadata>();
            return DataStateResult<string>.Invalid(scope.Reason);
        }

        if (scope.Products.Count == 0)
        {
            LatestPlanningFilterMetadata = Array.Empty<CanonicalFilterMetadata>();
            return DataStateResult<string>.Empty("No products are available for the current signal scope.");
        }

        var productIds = scope.Products.Select(product => product.Id).ToArray();
        var teamIds = scope.Products
            .SelectMany(product => product.TeamIds)
            .Distinct()
            .ToArray();

        var backlogStatesTask = LoadBacklogStatesAsync(scope.Products, cancellationToken);
        var recentSprintsTask = LoadRecentSprintsAsync(teamIds);

        await Task.WhenAll(backlogStatesTask, recentSprintsTask);

        var capacitySprintIds = (await recentSprintsTask)
            .Take(CapacitySprintCount)
            .Select(sprint => sprint.Id)
            .ToArray();

        var capacityCalibrationResponse = await LoadCapacityCalibrationAsync(
            productOwnerId,
            capacitySprintIds,
            productIds,
            cancellationToken);
        LatestPlanningFilterMetadata = capacityCalibrationResponse.Metadata;

        cancellationToken.ThrowIfCancellationRequested();
        var backlogStatesResult = await backlogStatesTask;
        if (backlogStatesResult.CanUseData)
        {
            return DataStateResult<string>.Ready(
                SelectPlanningSignal(backlogStatesResult.Data, capacityCalibrationResponse.CanUseData ? capacityCalibrationResponse.Data : null),
                backlogStatesResult.Metadata.Concat(capacityCalibrationResponse.Metadata).ToList());
        }

        return AggregateSignalStates(
            NeutralSignals.Planning,
            "No planning signal data matched the current scope.",
            backlogStatesResult,
            capacityCalibrationResponse);
    }

    public static string SelectHealthSignal(ValidationTriageSummaryDto? summary)
    {
        if (summary is null)
        {
            return NeutralSignals.Health;
        }

        var candidates = new List<WorkspaceSignalCandidate>();

        var parentChildMismatchCount = CountRuleGroups(summary.StructuralIntegrity, "SI-1");
        if (parentChildMismatchCount > 0)
        {
            candidates.Add(new WorkspaceSignalCandidate(
                $"Investigate {parentChildMismatchCount} parent-child state {Pluralize(parentChildMismatchCount, "mismatch", "mismatches")}",
                Priority: 1,
                Impact: parentChildMismatchCount,
                Count: parentChildMismatchCount));
        }

        var missingEffortCount = summary.MissingEffort.TotalItemCount;
        if (missingEffortCount > 0)
        {
            candidates.Add(new WorkspaceSignalCandidate(
                $"Investigate {missingEffortCount} {Pluralize(missingEffortCount, "item", "items")} missing effort",
                Priority: 2,
                Impact: missingEffortCount,
                Count: missingEffortCount));
        }

        var refinementCount = summary.RefinementReadiness.TotalItemCount + summary.RefinementCompleteness.TotalItemCount;
        if (refinementCount > 0)
        {
            candidates.Add(new WorkspaceSignalCandidate(
                $"Investigate {refinementCount} {Pluralize(refinementCount, "item", "items")} need refinement",
                Priority: 3,
                Impact: refinementCount,
                Count: refinementCount));
        }

        var inconsistentStateCount = CountRuleGroups(summary.StructuralIntegrity, "SI-2", "SI-3");
        if (inconsistentStateCount > 0)
        {
            candidates.Add(new WorkspaceSignalCandidate(
                $"Investigate {inconsistentStateCount} inconsistent state {Pluralize(inconsistentStateCount, "transition", "transitions")}",
                Priority: 4,
                Impact: inconsistentStateCount,
                Count: inconsistentStateCount));
        }

        if (candidates.Count == 0)
        {
            var totalIssues =
                summary.StructuralIntegrity.TotalItemCount +
                summary.RefinementReadiness.TotalItemCount +
                summary.RefinementCompleteness.TotalItemCount +
                summary.MissingEffort.TotalItemCount;

            if (totalIssues > 0)
            {
                candidates.Add(new WorkspaceSignalCandidate(
                    $"Investigate {totalIssues} backlog {Pluralize(totalIssues, "issue", "issues")}",
                    Priority: 5,
                    Impact: totalIssues,
                    Count: totalIssues));
            }
        }

        return SelectSignalText(candidates, NeutralSignals.Health);
    }

    public static string SelectDeliverySignal(
        IEnumerable<DeliverySignalContext>? contexts,
        DateTimeOffset nowUtc)
    {
        var candidates = contexts?
            .SelectMany(context => GetDeliveryCandidates(context, nowUtc))
            .ToList()
            ?? [];

        return SelectSignalText(candidates, NeutralSignals.Delivery);
    }

    public static string SelectTrendsSignal(
        GetSprintTrendMetricsResponse? sprintTrends,
        GetPrSprintTrendsResponse? pullRequestTrends)
    {
        var candidates = new List<WorkspaceSignalCandidate>();

        if (TryGetLatestPair(sprintTrends?.Metrics, metric => metric.StartUtc, out var currentSprint, out var previousSprint))
        {
            var bugRateChange = CompareValues(currentSprint.TotalBugsCreatedCount, previousSprint.TotalBugsCreatedCount);
            if (bugRateChange.IsIncrease)
            {
                candidates.Add(new WorkspaceSignalCandidate(
                    $"Bug spike detected (+{Math.Round(bugRateChange.Impact)}%)",
                    Priority: 1,
                    Impact: bugRateChange.Impact,
                    Count: currentSprint.TotalBugsCreatedCount));
            }

            var deliveryChange = CompareValues(currentSprint.TotalCompletedPbiCount, previousSprint.TotalCompletedPbiCount);
            if (deliveryChange.IsDecrease)
            {
                candidates.Add(new WorkspaceSignalCandidate(
                    $"Delivery throughput dropped {Math.Round(deliveryChange.Impact)}%",
                    Priority: 3,
                    Impact: deliveryChange.Impact,
                    Count: currentSprint.TotalCompletedPbiCount));
            }
        }

        if (TryGetLatestPair(pullRequestTrends?.Sprints, sprint => sprint.StartUtc, out var currentPrSprint, out var previousPrSprint))
        {
            var currentMergeHours = currentPrSprint.MedianTimeToMergeHours;
            var previousMergeHours = previousPrSprint.MedianTimeToMergeHours;

            if (currentMergeHours.HasValue && previousMergeHours.HasValue)
            {
                var cycleTimeChange = CompareValues(currentMergeHours.Value, previousMergeHours.Value);
                if (cycleTimeChange.IsIncrease)
                {
                    candidates.Add(new WorkspaceSignalCandidate(
                        $"PR merge time increased {Math.Round(cycleTimeChange.Impact)}%",
                        Priority: 2,
                        Impact: cycleTimeChange.Impact,
                        Count: currentPrSprint.TotalPrs));
                }
                else if (cycleTimeChange.IsDecrease)
                {
                    candidates.Add(new WorkspaceSignalCandidate(
                        "PR merge time improving",
                        Priority: 4,
                        Impact: cycleTimeChange.Impact,
                        Count: currentPrSprint.TotalPrs));
                }
            }
        }

        return SelectSignalText(candidates, NeutralSignals.Trends);
    }

    public static string SelectPlanningSignal(
        IEnumerable<ProductBacklogStateDto>? backlogStates,
        CapacityCalibrationDto? capacityCalibration)
    {
        var stats = CalculatePlanningStats(backlogStates);

        if (stats.ReadyFeatureCount == 0)
        {
            return "No features are ready for sprint";
        }

        if (stats.ReadyPbiCount <= PlanningReadyPbiThreshold)
        {
            return $"Only {stats.ReadyPbiCount} {Pluralize(stats.ReadyPbiCount, "PBI", "PBIs")} ready for sprint";
        }

        if (capacityCalibration is not null && capacityCalibration.MedianVelocity > 0 && stats.ReadyEffort < capacityCalibration.MedianVelocity)
        {
            var remainingPoints = Math.Max(1, (int)Math.Ceiling(capacityCalibration.MedianVelocity - stats.ReadyEffort));
            return $"Ready work is short by {remainingPoints} pts";
        }

        return NeutralSignals.Planning;
    }

    private async Task<DataStateResult<IReadOnlyList<ProductBacklogStateDto>>> LoadBacklogStatesAsync(
        IReadOnlyCollection<ProductDto> products,
        CancellationToken cancellationToken)
    {
        var states = await Task.WhenAll(products.Select(product =>
            _workItemService.GetBacklogStateResultAsync(product.Id, cancellationToken)));

        var readyStates = states
            .Where(state => state.CanUseData)
            .Select(state => state.Data!)
            .ToList();

        if (readyStates.Count > 0)
        {
            return DataStateResult<IReadOnlyList<ProductBacklogStateDto>>.Ready(
                readyStates,
                states.SelectMany(state => state.Metadata).ToList());
        }

        return AggregateDataStates<ProductBacklogStateDto>(
            states,
            "No backlog state data matched the current scope.");
    }

    private async Task<IReadOnlyList<SprintDto>> LoadCurrentSprintsAsync(IEnumerable<int> teamIds)
    {
        var currentSprints = await Task.WhenAll(teamIds.Select(teamId => _sprintService.GetCurrentSprintForTeamAsync(teamId)));
        return currentSprints
            .Where(sprint => sprint is not null)
            .Cast<SprintDto>()
            .OrderBy(sprint => sprint.EndUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(sprint => sprint.StartUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(sprint => sprint.Id)
            .ToList();
    }

    private async Task<IReadOnlyList<SprintDto>> LoadRecentSprintsAsync(IEnumerable<int> teamIds)
    {
        var sprintsByTeam = await Task.WhenAll(teamIds.Select(teamId => _sprintService.GetSprintsForTeamAsync(teamId)));
        return sprintsByTeam
            .SelectMany(sprints => sprints)
            .GroupBy(sprint => sprint.Id)
            .Select(group => group.First())
            .OrderByDescending(sprint => sprint.StartUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(sprint => sprint.EndUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(sprint => sprint.Id)
            .ToList();
    }

    private async Task<DataStateResult<IReadOnlyList<DeliverySignalContext>>> LoadDeliveryContextsAsync(
        int productOwnerId,
        IReadOnlyCollection<ProductDto> scopedProducts,
        IReadOnlyCollection<SprintDto> currentSprints,
        CancellationToken cancellationToken)
    {
        var combinations = currentSprints
            .SelectMany(sprint => scopedProducts
                .Where(product => product.TeamIds.Contains(sprint.TeamId))
                .Select(product => (Sprint: sprint, ProductId: product.Id)))
            .ToArray();
        if (combinations.Length == 0)
        {
            return DataStateResult<IReadOnlyList<DeliverySignalContext>>.Empty("No current sprint delivery contexts matched the current scope.");
        }

        var contexts = new List<DataStateResult<DeliverySignalContext>>(combinations.Length);

        // Home delivery signals normally fan out across a small set of current team sprints and scoped products.
        // Each batch caps concurrent request execution at 8 contexts; larger fan-out shapes are processed in
        // subsequent batches so the browser and API are not hit with the full combination count at once.
        foreach (var batch in combinations.Chunk(DeliveryContextLoadBatchSize))
        {
            contexts.AddRange(await Task.WhenAll(batch.Select(scope =>
                LoadDeliveryContextAsync(productOwnerId, scope.Sprint, scope.ProductId, cancellationToken))));
        }

        var readyContexts = contexts
            .Where(context => context.CanUseData)
            .Select(context => context.Data!)
            .ToList();

        if (readyContexts.Count > 0)
        {
            return DataStateResult<IReadOnlyList<DeliverySignalContext>>.Ready(
                readyContexts,
                contexts.SelectMany(context => context.Metadata).ToList());
        }

        return AggregateDataStates<DeliverySignalContext>(
            contexts,
            "No sprint delivery contexts matched the current scope.");
    }

    private async Task<DataStateResult<DeliverySignalContext>> LoadDeliveryContextAsync(
        int productOwnerId,
        SprintDto sprint,
        int productId,
        CancellationToken cancellationToken)
    {
        try
        {
            var sprintExecutionEnvelopeTask = _metricsClient.GetSprintExecutionAsync(
                productOwnerId,
                sprint.Id,
                productId,
                cancellationToken);
            var backlogHealthEnvelopeTask = _metricsClient.GetBacklogHealthAsync(
                sprint.Path,
                productOwnerId,
                [productId],
                sprint.Id,
                cancellationToken);

            await Task.WhenAll(sprintExecutionEnvelopeTask, backlogHealthEnvelopeTask);

            var sprintExecutionResponse = (await sprintExecutionEnvelopeTask)
                .ToDataStateResponse()
                .ToDataStateResult();
            var backlogHealthResponse = (await backlogHealthEnvelopeTask)
                .ToDataStateResponse()
                .ToDataStateResult();

            if (!sprintExecutionResponse.CanUseData || !backlogHealthResponse.CanUseData)
            {
                if (sprintExecutionResponse.Status == DataStateResultStatus.Invalid || backlogHealthResponse.Status == DataStateResultStatus.Invalid)
                {
                    return DataStateResult<DeliverySignalContext>.Invalid(
                        sprintExecutionResponse.Reason ?? backlogHealthResponse.Reason ?? "The sprint delivery context did not return usable data.",
                        filterMetadata: sprintExecutionResponse.Metadata.Concat(backlogHealthResponse.Metadata).ToList());
                }

                if (sprintExecutionResponse.Status == DataStateResultStatus.NotReady || backlogHealthResponse.Status == DataStateResultStatus.NotReady)
                {
                    return DataStateResult<DeliverySignalContext>.NotReady(
                        sprintExecutionResponse.Reason ?? backlogHealthResponse.Reason ?? "The sprint delivery context is not ready yet.",
                        filterMetadata: sprintExecutionResponse.Metadata.Concat(backlogHealthResponse.Metadata).ToList());
                }

                if (sprintExecutionResponse.Status == DataStateResultStatus.Failed || backlogHealthResponse.Status == DataStateResultStatus.Failed)
                {
                    return DataStateResult<DeliverySignalContext>.Failed(
                        sprintExecutionResponse.Reason ?? backlogHealthResponse.Reason ?? "The sprint delivery context could not be loaded.",
                        filterMetadata: sprintExecutionResponse.Metadata.Concat(backlogHealthResponse.Metadata).ToList());
                }

                return DataStateResult<DeliverySignalContext>.Empty(
                    sprintExecutionResponse.Reason ?? backlogHealthResponse.Reason ?? "The sprint delivery context did not return usable data.",
                    filterMetadata: sprintExecutionResponse.Metadata.Concat(backlogHealthResponse.Metadata).ToList());
            }

            var metadata = sprintExecutionResponse.Metadata
                .Concat(backlogHealthResponse.Metadata)
                .ToList();
            var context = new DeliverySignalContext(
                sprint,
                sprintExecutionResponse.Data,
                backlogHealthResponse.Data,
                metadata);

            return sprintExecutionResponse.HasInvalidFilter || backlogHealthResponse.HasInvalidFilter
                ? DataStateResult<DeliverySignalContext>.Invalid(
                    sprintExecutionResponse.Reason ?? backlogHealthResponse.Reason ?? "The delivery scope was corrected before loading signal data.",
                    context,
                    filterMetadata: metadata)
                : DataStateResult<DeliverySignalContext>.Ready(context, metadata);
        }
        catch (ApiException ex) when (ex.StatusCode == 400 || ex.StatusCode == 404)
        {
            _logger.LogWarning(
                ex,
                "Skipping home delivery signal context for ProductOwner {ProductOwnerId}, Sprint {SprintId}, Product {ProductId}.",
                productOwnerId,
                sprint.Id,
                productId);
            return ex.StatusCode == 400
                ? DataStateResult<DeliverySignalContext>.Invalid(
                    $"Sprint '{sprint.Id}' rejected product '{productId}' for delivery signal scope.")
                : DataStateResult<DeliverySignalContext>.Empty(
                    $"No sprint delivery context was available for sprint '{sprint.Id}' and product '{productId}'.");
        }
    }

    private async Task<DataStateResult<GetSprintTrendMetricsResponse>> LoadSprintTrendsAsync(
        int productOwnerId,
        IReadOnlyCollection<int> sprintIds,
        CancellationToken cancellationToken)
    {
        if (sprintIds.Count < 2)
        {
            return DataStateResult<GetSprintTrendMetricsResponse>.Empty("Need at least two sprints to evaluate delivery trends.");
        }

        return (await _metricsClient.GetSprintTrendMetricsAsync(
            productOwnerId,
            sprintIds,
            null,
            false,
            true,
            cancellationToken))
            .ToDataStateResponse()
            .ToDataStateResult();
    }

    private async Task<DataStateResult<GetPrSprintTrendsResponse>> LoadPrTrendsAsync(
        IReadOnlyCollection<int> sprintIds,
        string productIdsCsv,
        CancellationToken cancellationToken)
    {
        if (sprintIds.Count < 2)
        {
            return DataStateResult<GetPrSprintTrendsResponse>.Empty("Need at least two sprints to evaluate PR trends.");
        }

        return (await _pullRequestsClient.GetSprintTrendsAsync(sprintIds, productIdsCsv, null, cancellationToken))
            .ToDataStateResponse()
            .ToDataStateResult();
    }

    private async Task<DataStateResult<CapacityCalibrationDto>> LoadCapacityCalibrationAsync(
        int productOwnerId,
        IReadOnlyCollection<int> sprintIds,
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken)
    {
        if (sprintIds.Count == 0)
        {
            return DataStateResult<CapacityCalibrationDto>.Empty("No sprint range was available for capacity calibration.");
        }

        return (await _metricsClient.GetCapacityCalibrationAsync(productOwnerId, sprintIds, productIds, cancellationToken))
            .ToDataStateResponse()
            .ToDataStateResult();
    }

    private static IEnumerable<WorkspaceSignalCandidate> GetDeliveryCandidates(
        DeliverySignalContext context,
        DateTimeOffset nowUtc)
    {
        var execution = context.SprintExecution;
        var backlogHealth = context.BacklogHealth;

        var unfinishedCount = execution?.Summary.UnfinishedCount ?? 0;
        if (unfinishedCount > 0 && IsNearSprintEnd(context.Sprint.EndUtc ?? execution?.EndUtc, nowUtc))
        {
            yield return new WorkspaceSignalCandidate(
                $"{unfinishedCount} {Pluralize(unfinishedCount, "PBI", "PBIs")} may spill over this sprint",
                Priority: 1,
                Impact: unfinishedCount,
                Count: unfinishedCount);
        }

        var addedDuringSprintCount = execution?.Summary.AddedDuringSprintCount ?? 0;
        var initialScopeCount = execution?.Summary.InitialScopeCount ?? 0;
        var scopeIncreasePct = initialScopeCount > 0
            ? (double)addedDuringSprintCount / initialScopeCount * 100d
            : 0d;

        if (addedDuringSprintCount > 0 && scopeIncreasePct >= 25d)
        {
            yield return new WorkspaceSignalCandidate(
                $"{Math.Round(scopeIncreasePct)}% scope added mid-sprint",
                Priority: 2,
                Impact: scopeIncreasePct,
                Count: addedDuringSprintCount);
        }

        if (addedDuringSprintCount > 0)
        {
            yield return new WorkspaceSignalCandidate(
                $"{addedDuringSprintCount} {Pluralize(addedDuringSprintCount, "PBI", "PBIs")} added mid-sprint",
                Priority: 3,
                Impact: addedDuringSprintCount,
                Count: addedDuringSprintCount);
        }

        var blockedItemCount = backlogHealth?.BlockedItems ?? 0;
        if (blockedItemCount > 0)
        {
            yield return new WorkspaceSignalCandidate(
                $"{blockedItemCount} blocked {Pluralize(blockedItemCount, "PBI", "PBIs")} in sprint",
                Priority: 4,
                Impact: blockedItemCount,
                Count: blockedItemCount);
        }

        var missingEffortCount = backlogHealth?.WorkItemsWithoutEffort ?? 0;
        if (missingEffortCount > 0)
        {
            yield return new WorkspaceSignalCandidate(
                $"{missingEffortCount} {Pluralize(missingEffortCount, "PBI", "PBIs")} missing effort in sprint",
                Priority: 5,
                Impact: missingEffortCount,
                Count: missingEffortCount);
        }
    }

    private static PlanningStats CalculatePlanningStats(IEnumerable<ProductBacklogStateDto>? backlogStates)
    {
        var readyFeatures = backlogStates?
            .SelectMany(state => state.Epics)
            .SelectMany(epic => epic.Features)
            .Where(feature => feature.OwnerState == FeatureOwnerState.Ready || feature.Score == 100)
            .ToList()
            ?? [];

        var readyPbis = readyFeatures
            .SelectMany(feature => feature.Pbis)
            .Where(pbi => pbi.Score == 100)
            .ToList();

        return new PlanningStats(
            readyFeatures.Count,
            readyPbis.Count,
            readyPbis.Sum(pbi => pbi.Effort ?? 0));
    }

    private static DataStateResult<string> MapSignalResult<T>(
        DataStateResult<T> result,
        Func<T?, string> selector)
    {
        if (result.CanUseData)
        {
            return result.Status == DataStateResultStatus.Invalid
                ? DataStateResult<string>.Invalid(
                    result.Reason ?? "The selected filter scope was corrected before loading the signal.",
                    selector(result.Data),
                    result.RetryAfterSeconds,
                    result.Metadata)
                : DataStateResult<string>.Ready(
                    selector(result.Data),
                    result.Metadata,
                    result.Reason,
                    result.RetryAfterSeconds);
        }

        return result.Status switch
        {
            DataStateResultStatus.Empty => DataStateResult<string>.Empty(result.Reason, result.RetryAfterSeconds, result.Metadata),
            DataStateResultStatus.NotReady => DataStateResult<string>.NotReady(result.Reason, result.RetryAfterSeconds, result.Metadata),
            DataStateResultStatus.Invalid => DataStateResult<string>.Invalid(result.Reason, retryAfterSeconds: result.RetryAfterSeconds, filterMetadata: result.Metadata),
            _ => DataStateResult<string>.Failed(result.Reason, result.RetryAfterSeconds, result.Metadata)
        };
    }

    private static DataStateResult<IReadOnlyList<T>> AggregateDataStates<T>(
        IEnumerable<DataStateResult<T>> results,
        string emptyReason)
    {
        var resultList = results.ToList();
        var metadata = resultList.SelectMany(result => result.Metadata).ToList();
        if (resultList.Count == 0)
        {
            return DataStateResult<IReadOnlyList<T>>.Empty(emptyReason);
        }

        var invalidResult = resultList.FirstOrDefault(result => result.Status == DataStateResultStatus.Invalid);
        if (invalidResult is not null)
        {
            return DataStateResult<IReadOnlyList<T>>.Invalid(
                invalidResult.Reason ?? emptyReason,
                retryAfterSeconds: invalidResult.RetryAfterSeconds,
                filterMetadata: metadata);
        }

        var notReadyResult = resultList.FirstOrDefault(result => result.Status == DataStateResultStatus.NotReady);
        if (notReadyResult is not null)
        {
            return DataStateResult<IReadOnlyList<T>>.NotReady(
                notReadyResult.Reason ?? emptyReason,
                notReadyResult.RetryAfterSeconds,
                metadata);
        }

        var failedResult = resultList.FirstOrDefault(result => result.Status == DataStateResultStatus.Failed);
        if (failedResult is not null)
        {
            return DataStateResult<IReadOnlyList<T>>.Failed(
                failedResult.Reason ?? emptyReason,
                failedResult.RetryAfterSeconds,
                metadata);
        }

        return DataStateResult<IReadOnlyList<T>>.Empty(emptyReason, filterMetadata: metadata);
    }

    private static DataStateResult<string> AggregateSignalStates<T1, T2>(
        string neutralSignal,
        string emptyReason,
        DataStateResult<T1> first,
        DataStateResult<T2> second)
    {
        var metadata = first.Metadata.Concat(second.Metadata).ToList();

        if (first.Status == DataStateResultStatus.Invalid || second.Status == DataStateResultStatus.Invalid)
        {
            return DataStateResult<string>.Invalid(
                first.Reason ?? second.Reason ?? emptyReason,
                neutralSignal,
                first.RetryAfterSeconds ?? second.RetryAfterSeconds,
                metadata);
        }

        if (first.Status == DataStateResultStatus.NotReady || second.Status == DataStateResultStatus.NotReady)
        {
            return DataStateResult<string>.NotReady(
                first.Reason ?? second.Reason ?? emptyReason,
                first.RetryAfterSeconds ?? second.RetryAfterSeconds,
                metadata);
        }

        if (first.Status == DataStateResultStatus.Failed || second.Status == DataStateResultStatus.Failed)
        {
            return DataStateResult<string>.Failed(
                first.Reason ?? second.Reason ?? emptyReason,
                first.RetryAfterSeconds ?? second.RetryAfterSeconds,
                metadata);
        }

        return DataStateResult<string>.Empty(emptyReason, filterMetadata: metadata);
    }

    private static string SelectSignalText(
        IEnumerable<WorkspaceSignalCandidate> candidates,
        string neutralSignal)
    {
        return candidates
            .OrderBy(candidate => candidate.Priority)
            .ThenByDescending(candidate => candidate.Impact)
            .ThenByDescending(candidate => candidate.Count)
            .ThenBy(candidate => candidate.Text, StringComparer.Ordinal)
            .Select(candidate => candidate.Text)
            .FirstOrDefault() ?? neutralSignal;
    }

    private static bool TryGetLatestPair<T>(
        IEnumerable<T>? values,
        Func<T, DateTimeOffset?> dateSelector,
        out T latest,
        out T previous)
    {
        var orderedValues = values?
            .OrderByDescending(value => dateSelector(value) ?? DateTimeOffset.MinValue)
            .ToList()
            ?? [];

        if (orderedValues.Count >= 2)
        {
            latest = orderedValues[0];
            previous = orderedValues[1];
            return true;
        }

        latest = default!;
        previous = default!;
        return false;
    }

    private static ValueComparison CompareValues(double currentValue, double previousValue)
    {
        var delta = currentValue - previousValue;
        if (delta == 0)
        {
            return ValueComparison.None;
        }

        if (previousValue > 0)
        {
            var percentChange = Math.Abs(delta) / previousValue * 100d;
            if (percentChange < 10d)
            {
                return ValueComparison.None;
            }

            return new ValueComparison(delta > 0, delta < 0, percentChange, IsPercentage: true);
        }

        if (Math.Abs(delta) < 1d)
        {
            return ValueComparison.None;
        }

        return new ValueComparison(delta > 0, delta < 0, Math.Abs(delta), IsPercentage: false);
    }

    private static int CountRuleGroups(
        ValidationCategoryTriageDto category,
        params string[] ruleIds)
    {
        if (category.TopRuleGroups is null || category.TopRuleGroups.Count == 0)
        {
            return 0;
        }

        return category.TopRuleGroups
            .Where(group => ruleIds.Contains(group.RuleId, StringComparer.OrdinalIgnoreCase))
            .Sum(group => group.ItemCount);
    }

    private static bool IsNearSprintEnd(DateTimeOffset? sprintEndUtc, DateTimeOffset nowUtc)
    {
        return sprintEndUtc.HasValue && nowUtc >= sprintEndUtc.Value.AddDays(-3);
    }

    private static string Pluralize(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }

    private sealed record WorkspaceSignalCandidate(
        string Text,
        int Priority,
        double Impact,
        int Count);

    private sealed record ValueComparison(
        bool IsIncrease,
        bool IsDecrease,
        double Impact,
        bool IsPercentage)
    {
        public static ValueComparison None { get; } = new(false, false, 0d, false);

        public string FormatIncrease(string prefix)
        {
            return IsPercentage
                ? $"{prefix} {Math.Round(Impact):F0}%"
                : $"{prefix} {Math.Round(Impact):F0}";
        }

        public string FormatDecrease(string prefix)
        {
            return IsPercentage
                ? $"{prefix} {Math.Round(Impact):F0}%"
                : $"{prefix} {Math.Round(Impact):F0}";
        }
    }

    private sealed record PlanningStats(
        int ReadyFeatureCount,
        int ReadyPbiCount,
        int ReadyEffort);
}

public sealed record WorkspaceSignalSet(
    string Health,
    string Delivery,
    string Trends,
    string Planning)
{
    public static WorkspaceSignalSet Neutral { get; } = new(
        "Confirm backlog is healthy",
        "Confirm delivery is on track",
        "Confirm trends are stable",
        "Confirm planning is healthy");
}

public sealed record DeliverySignalContext(
    SprintDto Sprint,
    SprintExecutionDto? SprintExecution,
    BacklogHealthDto? BacklogHealth,
    IReadOnlyList<CanonicalFilterMetadata> FilterMetadata);
