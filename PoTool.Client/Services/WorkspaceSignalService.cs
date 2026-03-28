using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.Health;
using PoTool.Shared.Metrics;
using PoTool.Shared.PullRequests;
using PoTool.Shared.WorkItems;
using BacklogHealthDto = PoTool.Client.ApiClient.BacklogHealthDto;
using ProductDto = PoTool.Client.ApiClient.ProductDto;
using SprintDto = PoTool.Client.ApiClient.SprintDto;

namespace PoTool.Client.Services;

public sealed class WorkspaceSignalService
{
    private const int TrendSprintCount = 2;
    private const int CapacitySprintCount = 6;
    private const int PlanningReadyPbiThreshold = 3;
    private static readonly WorkspaceSignalSet NeutralSignals = new(
        "Confirm backlog is healthy",
        "Confirm delivery is on track",
        "Confirm trends are stable",
        "Confirm planning is healthy");

    private readonly IMetricsClient _metricsClient;
    private readonly IPullRequestsClient _pullRequestsClient;
    private readonly IWorkItemsClient _workItemsClient;
    private readonly SprintService _sprintService;
    private readonly WorkItemService _workItemService;

    public IReadOnlyList<CanonicalFilterMetadata> LatestDeliveryFilterMetadata { get; private set; } = Array.Empty<CanonicalFilterMetadata>();

    public IReadOnlyList<CanonicalFilterMetadata> LatestTrendFilterMetadata { get; private set; } = Array.Empty<CanonicalFilterMetadata>();

    public IReadOnlyList<CanonicalFilterMetadata> LatestPlanningFilterMetadata { get; private set; } = Array.Empty<CanonicalFilterMetadata>();

    public WorkspaceSignalService(
        IMetricsClient metricsClient,
        IPullRequestsClient pullRequestsClient,
        IWorkItemsClient workItemsClient,
        SprintService sprintService,
        WorkItemService workItemService)
    {
        _metricsClient = metricsClient;
        _pullRequestsClient = pullRequestsClient;
        _workItemsClient = workItemsClient;
        _sprintService = sprintService;
        _workItemService = workItemService;
    }

    public async Task<WorkspaceSignalSet> GetSignalsAsync(
        int productOwnerId,
        IReadOnlyCollection<ProductDto> products,
        int? selectedProductId,
        CancellationToken cancellationToken = default)
    {
        var healthTask = GetHealthSignalAsync(products, selectedProductId, cancellationToken);
        var deliveryTask = GetDeliverySignalAsync(productOwnerId, products, selectedProductId, cancellationToken);
        var trendsTask = GetTrendsSignalAsync(productOwnerId, products, selectedProductId, cancellationToken);
        var planningTask = GetPlanningSignalAsync(productOwnerId, products, selectedProductId, cancellationToken);

        await Task.WhenAll(healthTask, deliveryTask, trendsTask, planningTask);

        return new WorkspaceSignalSet(
            await healthTask,
            await deliveryTask,
            await trendsTask,
            await planningTask);
    }

    public async Task<string> GetHealthSignalAsync(
        IReadOnlyCollection<ProductDto> products,
        int? selectedProductId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scopedProducts = GetScopedProducts(products, selectedProductId);
        if (scopedProducts.Count == 0)
        {
            return NeutralSignals.Health;
        }

        var productIds = scopedProducts.Select(product => product.Id).ToArray();
        var summary = await _workItemService.GetValidationTriageSummaryAsync(productIds);

        cancellationToken.ThrowIfCancellationRequested();
        return SelectHealthSignal(summary);
    }

    public async Task<string> GetDeliverySignalAsync(
        int productOwnerId,
        IReadOnlyCollection<ProductDto> products,
        int? selectedProductId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scopedProducts = GetScopedProducts(products, selectedProductId);
        if (scopedProducts.Count == 0)
        {
            LatestDeliveryFilterMetadata = Array.Empty<CanonicalFilterMetadata>();
            return NeutralSignals.Delivery;
        }

        var teamIds = scopedProducts
            .SelectMany(product => product.TeamIds)
            .Distinct()
            .ToArray();

        var currentSprints = await LoadCurrentSprintsAsync(teamIds);
        var deliveryContexts = await LoadDeliveryContextsAsync(
            productOwnerId,
            selectedProductId,
            currentSprints,
            cancellationToken);
        LatestDeliveryFilterMetadata = deliveryContexts
            .SelectMany(context => context.FilterMetadata)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();
        return SelectDeliverySignal(deliveryContexts, DateTimeOffset.UtcNow);
    }

    public async Task<string> GetTrendsSignalAsync(
        int productOwnerId,
        IReadOnlyCollection<ProductDto> products,
        int? selectedProductId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scopedProducts = GetScopedProducts(products, selectedProductId);
        if (scopedProducts.Count == 0)
        {
            LatestTrendFilterMetadata = Array.Empty<CanonicalFilterMetadata>();
            return NeutralSignals.Trends;
        }

        var productIds = scopedProducts.Select(product => product.Id).ToArray();
        var teamIds = scopedProducts
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
        LatestTrendFilterMetadata = new[]
            {
                sprintTrendResponse?.FilterMetadata,
                prTrendResponse?.FilterMetadata
            }
            .Where(metadata => metadata is not null)
            .Cast<CanonicalFilterMetadata>()
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();
        return SelectTrendsSignal(sprintTrendResponse?.Data, prTrendResponse?.Data);
    }

    public async Task<string> GetPlanningSignalAsync(
        int productOwnerId,
        IReadOnlyCollection<ProductDto> products,
        int? selectedProductId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scopedProducts = GetScopedProducts(products, selectedProductId);
        if (scopedProducts.Count == 0)
        {
            LatestPlanningFilterMetadata = Array.Empty<CanonicalFilterMetadata>();
            return NeutralSignals.Planning;
        }

        var productIds = scopedProducts.Select(product => product.Id).ToArray();
        var teamIds = scopedProducts
            .SelectMany(product => product.TeamIds)
            .Distinct()
            .ToArray();

        var backlogStatesTask = LoadBacklogStatesAsync(scopedProducts, cancellationToken);
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
        LatestPlanningFilterMetadata = capacityCalibrationResponse?.FilterMetadata is null
            ? Array.Empty<CanonicalFilterMetadata>()
            : [capacityCalibrationResponse.FilterMetadata];

        cancellationToken.ThrowIfCancellationRequested();
        return SelectPlanningSignal(await backlogStatesTask, capacityCalibrationResponse?.Data);
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

    private static IReadOnlyList<ProductDto> GetScopedProducts(
        IReadOnlyCollection<ProductDto> products,
        int? selectedProductId)
    {
        if (!selectedProductId.HasValue)
        {
            return products.ToList();
        }

        return products
            .Where(product => product.Id == selectedProductId.Value)
            .ToList();
    }

    private async Task<IReadOnlyList<ProductBacklogStateDto>> LoadBacklogStatesAsync(
        IReadOnlyCollection<ProductDto> products,
        CancellationToken cancellationToken)
    {
        var states = await Task.WhenAll(products.Select(async product =>
        {
            try
            {
                return await _workItemsClient.GetBacklogStateAsync(product.Id, cancellationToken);
            }
            catch (ApiException ex) when (ex.StatusCode == 404)
            {
                return null;
            }
        }));

        return states
            .Where(state => state is not null)
            .Cast<ProductBacklogStateDto>()
            .ToList();
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

    private async Task<IReadOnlyList<DeliverySignalContext>> LoadDeliveryContextsAsync(
        int productOwnerId,
        int? selectedProductId,
        IReadOnlyCollection<SprintDto> currentSprints,
        CancellationToken cancellationToken)
    {
        var contexts = await Task.WhenAll(currentSprints.Select(async sprint =>
        {
            try
            {
                var sprintExecutionEnvelopeTask = _metricsClient.GetSprintExecutionEnvelopeAsync(
                    productOwnerId,
                    sprint.Id,
                    selectedProductId,
                    cancellationToken);
                var backlogHealthEnvelopeTask = _metricsClient.GetBacklogHealthEnvelopeAsync(
                    sprint.Path,
                    productOwnerId,
                    selectedProductId.HasValue ? [selectedProductId.Value] : null,
                    sprint.Id,
                    cancellationToken);

                await Task.WhenAll(sprintExecutionEnvelopeTask, backlogHealthEnvelopeTask);

                var sprintExecutionResponse = CanonicalClientResponseFactory.Create(await sprintExecutionEnvelopeTask);
                var backlogHealthResponse = CanonicalClientResponseFactory.Create(await backlogHealthEnvelopeTask);

                return new DeliverySignalContext(
                    sprint,
                    sprintExecutionResponse.Data,
                    backlogHealthResponse.Data,
                    new[]
                        {
                            sprintExecutionResponse.FilterMetadata,
                            backlogHealthResponse.FilterMetadata
                        }
                        .Where(metadata => metadata is not null)
                        .Cast<CanonicalFilterMetadata>()
                        .ToList());
            }
            catch (ApiException ex) when (ex.StatusCode == 404)
            {
                return null;
            }
        }));

        return contexts
            .Where(context => context is not null)
            .Cast<DeliverySignalContext>()
            .ToList();
    }

    private async Task<CanonicalClientResponse<GetSprintTrendMetricsResponse>?> LoadSprintTrendsAsync(
        int productOwnerId,
        IReadOnlyCollection<int> sprintIds,
        CancellationToken cancellationToken)
    {
        if (sprintIds.Count < 2)
        {
            return null;
        }

        var response = await _metricsClient.GetSprintTrendMetricsEnvelopeAsync(
            productOwnerId,
            sprintIds,
            null,
            false,
            true,
            cancellationToken);
        return CanonicalClientResponseFactory.Create(response);
    }

    private async Task<CanonicalClientResponse<GetPrSprintTrendsResponse>?> LoadPrTrendsAsync(
        IReadOnlyCollection<int> sprintIds,
        string productIdsCsv,
        CancellationToken cancellationToken)
    {
        if (sprintIds.Count < 2)
        {
            return null;
        }

        var response = await _pullRequestsClient.GetSprintTrendsEnvelopeAsync(sprintIds, productIdsCsv, null, cancellationToken);
        return CanonicalClientResponseFactory.Create(response);
    }

    private async Task<CanonicalClientResponse<CapacityCalibrationDto>?> LoadCapacityCalibrationAsync(
        int productOwnerId,
        IReadOnlyCollection<int> sprintIds,
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken)
    {
        if (sprintIds.Count == 0)
        {
            return null;
        }

        var response = await _metricsClient.GetCapacityCalibrationEnvelopeAsync(productOwnerId, sprintIds, productIds, cancellationToken);
        return CanonicalClientResponseFactory.Create(response);
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
