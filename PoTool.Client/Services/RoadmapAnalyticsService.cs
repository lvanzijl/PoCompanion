using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Analytics integration layer for roadmap epic cards.
/// Computes and aggregates signals from existing cached work item data and
/// existing API endpoints (epic forecast, backlog state). Read-only — never
/// modifies TFS data or changes roadmap ordering or membership.
/// </summary>
public class RoadmapAnalyticsService
{
    private readonly IMetricsClient _metricsClient;
    private readonly IWorkItemsClient _workItemsClient;

    public RoadmapAnalyticsService(IMetricsClient metricsClient, IWorkItemsClient workItemsClient)
    {
        _metricsClient = metricsClient;
        _workItemsClient = workItemsClient;
    }

    /// <summary>
    /// Computes local analytics for a single epic from already-loaded work items.
    /// Includes effort aggregation, PBI count, epic age, and last activity.
    /// </summary>
    /// <param name="epicTfsId">TFS ID of the epic.</param>
    /// <param name="allWorkItems">Full hierarchy of work items (as returned by GetByRootIdsAsync).</param>
    /// <returns>Locally computed analytics for the epic.</returns>
    public static EpicLocalAnalytics ComputeLocalAnalytics(int epicTfsId, IEnumerable<WorkItemDto> allWorkItems)
    {
        var workItemList = allWorkItems as IList<WorkItemDto> ?? allWorkItems.ToList();

        // Find all descendants of this epic (PBIs, Bugs, Features, Tasks)
        var descendantIds = new HashSet<int> { epicTfsId };
        bool changed;
        do
        {
            changed = false;
            foreach (var wi in workItemList)
            {
                if (wi.ParentTfsId.HasValue &&
                    descendantIds.Contains(wi.ParentTfsId.Value) &&
                    descendantIds.Add(wi.TfsId))
                {
                    changed = true;
                }
            }
        } while (changed);

        // Remove the epic itself from descendants
        descendantIds.Remove(epicTfsId);

        var descendants = workItemList
            .Where(wi => descendantIds.Contains(wi.TfsId))
            .ToList();

        // PBIs = Product Backlog Items and Bugs (leaf-level deliverables)
        var pbis = descendants
            .Where(wi => wi.Type is "Product Backlog Item" or "Bug")
            .ToList();

        // Active items = not Closed, Done, or Removed
        var activeDescendants = descendants
            .Where(wi => !IsTerminalState(wi.State))
            .ToList();

        // Effort: sum of effort on active PBIs/Bugs (reuses delivery calculation pattern)
        var totalEffort = activeDescendants
            .Where(wi => wi.Type is "Product Backlog Item" or "Bug")
            .Sum(wi => wi.Effort ?? 0);

        var pbiCount = pbis.Count;

        // Epic age: days since epic creation
        var epicWorkItem = workItemList.FirstOrDefault(wi => wi.TfsId == epicTfsId);
        int? epicAgeDays = null;
        if (epicWorkItem?.CreatedDate != null)
        {
            epicAgeDays = (int)(DateTimeOffset.UtcNow - epicWorkItem.CreatedDate.Value).TotalDays;
        }

        // Last activity: most recent ChangedDate among the epic and all descendants
        // Note: The NSwag-generated WorkItemDto may not expose ChangedDate.
        // Fall back to RetrievedAt when ChangedDate is unavailable.
        var allDates = new List<DateTimeOffset>();
        if (epicWorkItem != null)
            allDates.Add(epicWorkItem.RetrievedAt);
        foreach (var d in descendants)
            allDates.Add(d.RetrievedAt);

        int? lastActivityDays = null;
        if (allDates.Count > 0)
        {
            var mostRecent = allDates.Max();
            lastActivityDays = (int)(DateTimeOffset.UtcNow - mostRecent).TotalDays;
        }

        return new EpicLocalAnalytics(totalEffort, pbiCount, epicAgeDays, lastActivityDays);
    }

    /// <summary>
    /// Loads epic completion forecast data from the existing Metrics API endpoint.
    /// Reuses the same calculation as the Planning workspace.
    /// Returns null if forecast data is unavailable for this epic.
    /// </summary>
    public async Task<EpicForecastAnalytics?> LoadForecastAsync(int epicTfsId)
    {
        try
        {
            var forecast = await _metricsClient.GetEpicForecastAsync(epicTfsId, 5);
            if (forecast == null)
                return null;

            // Reuse the same threshold as PlanningWorkspace:
            // Epic is "at risk" if it needs more than 3 sprints or exceeds 3x velocity
            var exceedsVelocity = forecast.SprintsRemaining > 3 ||
                                  (forecast.EstimatedVelocity > 0 &&
                                   forecast.RemainingEffort > forecast.EstimatedVelocity * 3);

            return new EpicForecastAnalytics(
                forecast.SprintsRemaining,
                exceedsVelocity,
                forecast.Confidence);
        }
        catch
        {
            // Forecast unavailable — skip gracefully
            return null;
        }
    }

    /// <summary>
    /// Loads backlog health signals for all epics in a product from the existing
    /// Backlog State API endpoint. Returns a dictionary keyed by epic TFS ID.
    /// Reuses the same refinement scoring as the Health workspace.
    /// </summary>
    public async Task<Dictionary<int, EpicHealthAnalytics>> LoadBacklogHealthAsync(int productId)
    {
        var result = new Dictionary<int, EpicHealthAnalytics>();
        try
        {
            var backlogState = await _workItemsClient.GetBacklogStateAsync(productId);
            if (backlogState?.Epics == null)
                return result;

            foreach (var epic in backlogState.Epics)
            {
                var hasRefinementBlockers = !epic.HasDescription;
                var validationIssueCount = epic.Features
                    .SelectMany(f => f.Pbis)
                    .Count(p => p.Score < 100);

                result[epic.TfsId] = new EpicHealthAnalytics(
                    epic.Score,
                    hasRefinementBlockers,
                    validationIssueCount > 0);
            }
        }
        catch
        {
            // Backlog health unavailable — skip gracefully
        }

        return result;
    }

    private static bool IsTerminalState(string state) =>
        state.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
        state.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
        state.Equals("Removed", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Locally computed analytics for a roadmap epic, derived from cached work item data.
/// </summary>
/// <param name="TotalEffort">Total remaining effort (story points) across active PBIs/Bugs.</param>
/// <param name="PbiCount">Total number of PBIs and Bugs under this epic.</param>
/// <param name="EpicAgeDays">Days since the epic was created, or null if creation date unavailable.</param>
/// <param name="LastActivityDays">Days since the most recent activity on the epic or its descendants.</param>
public sealed record EpicLocalAnalytics(
    int TotalEffort,
    int PbiCount,
    int? EpicAgeDays,
    int? LastActivityDays);

/// <summary>
/// Forecast-based analytics from the existing Metrics API (Planning workspace signals).
/// </summary>
/// <param name="SprintsRemaining">Estimated sprints to complete the epic.</param>
/// <param name="ExceedsVelocity">True if the epic exceeds ~3× median team velocity.</param>
/// <param name="Confidence">Forecast confidence level (High, Medium, Low).</param>
public sealed record EpicForecastAnalytics(
    int SprintsRemaining,
    bool ExceedsVelocity,
    ForecastConfidence Confidence);

/// <summary>
/// Backlog health analytics from the existing Backlog State API (Health workspace signals).
/// </summary>
/// <param name="RefinementScore">Refinement score (0–100) for the epic.</param>
/// <param name="HasRefinementBlockers">True if the epic is missing a description (blocks refinement).</param>
/// <param name="HasValidationIssues">True if any child PBIs have incomplete readiness (score &lt; 100).</param>
public sealed record EpicHealthAnalytics(
    int RefinementScore,
    bool HasRefinementBlockers,
    bool HasValidationIssues);
