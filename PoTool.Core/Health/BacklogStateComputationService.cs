using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.Health;

/// <summary>
/// Computes backlog refinement and readiness scores per the Backlog State Model specification.
///
/// Responsibilities:
/// - Compute PBI readiness score (0 / 75 / 100)
/// - Compute Feature refinement score (0 / 25 / average-of-PBIs)
/// - Compute Epic refinement score (0 / 30 / average-of-Features)
/// - Derive Feature OwnerState (PO / Team / Ready)
///
/// Design constraints:
/// - Operates purely on already-loaded work item graph; does not query the database.
/// - Structural Integrity (SI) violations do NOT influence any score.
/// - Deterministic and stateless.
/// </summary>
public sealed class BacklogStateComputationService
{
    private static readonly IReadOnlySet<int> EmptyDoneIds = new HashSet<int>();
    /// <summary>
    /// Computes the readiness score for a single PBI.
    /// </summary>
    /// <param name="pbi">The PBI work item.</param>
    /// <returns>Score 0, 75, or 100.</returns>
    public PbiReadinessScore ComputePbiScore(WorkItemDto pbi)
    {
        ArgumentNullException.ThrowIfNull(pbi);

        if (string.IsNullOrWhiteSpace(pbi.Description))
        {
            return new PbiReadinessScore(pbi.TfsId, 0);
        }

        if (!pbi.Effort.HasValue || pbi.Effort.Value <= 0)
        {
            return new PbiReadinessScore(pbi.TfsId, 75);
        }

        return new PbiReadinessScore(pbi.TfsId, 100);
    }

    /// <summary>
    /// Computes the refinement score and ownership state for a single Feature.
    /// Done PBIs (those in <paramref name="doneItemIds"/>) contribute a score of 100.
    /// </summary>
    /// <param name="feature">The Feature work item.</param>
    /// <param name="allItems">All non-removed work items in the loaded graph (used to find PBI children).</param>
    /// <param name="doneItemIds">TFS IDs of items classified as Done; they contribute 100 to the average.</param>
    /// <returns>Score and OwnerState for the Feature.</returns>
    public FeatureRefinementScore ComputeFeatureScore(
        WorkItemDto feature,
        IEnumerable<WorkItemDto> allItems,
        IReadOnlySet<int> doneItemIds)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(allItems);
        ArgumentNullException.ThrowIfNull(doneItemIds);

        if (string.IsNullOrWhiteSpace(feature.Description))
        {
            return new FeatureRefinementScore(feature.TfsId, 0, FeatureOwnerState.PO);
        }

        var pbis = allItems
            .Where(w => w.ParentTfsId == feature.TfsId &&
                        string.Equals(w.Type, WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pbis.Count == 0)
        {
            return new FeatureRefinementScore(feature.TfsId, 25, FeatureOwnerState.Team);
        }

        var averageScore = (int)Math.Round(
            pbis.Average(pbi => doneItemIds.Contains(pbi.TfsId) ? 100.0 : ComputePbiScore(pbi).Score));
        var ownerState = averageScore == 100 ? FeatureOwnerState.Ready : FeatureOwnerState.Team;

        return new FeatureRefinementScore(feature.TfsId, averageScore, ownerState);
    }

    /// <summary>
    /// Computes the refinement score and ownership state for a single Feature.
    /// </summary>
    /// <param name="feature">The Feature work item.</param>
    /// <param name="allItems">All work items in the loaded graph (used to find PBI children).</param>
    /// <returns>Score and OwnerState for the Feature.</returns>
    public FeatureRefinementScore ComputeFeatureScore(WorkItemDto feature, IEnumerable<WorkItemDto> allItems)
        => ComputeFeatureScore(feature, allItems, EmptyDoneIds);

    /// <summary>
    /// Computes the refinement score for a single Epic.
    /// Done Features (those in <paramref name="doneItemIds"/>) contribute a score of 100.
    /// </summary>
    /// <param name="epic">The Epic work item.</param>
    /// <param name="allItems">All non-removed work items in the loaded graph (used to find Feature children).</param>
    /// <param name="doneItemIds">TFS IDs of items classified as Done; they contribute 100 to the average.</param>
    /// <returns>Score for the Epic.</returns>
    public EpicRefinementScore ComputeEpicScore(
        WorkItemDto epic,
        IEnumerable<WorkItemDto> allItems,
        IReadOnlySet<int> doneItemIds)
    {
        ArgumentNullException.ThrowIfNull(epic);
        ArgumentNullException.ThrowIfNull(allItems);
        ArgumentNullException.ThrowIfNull(doneItemIds);

        if (string.IsNullOrWhiteSpace(epic.Description))
        {
            return new EpicRefinementScore(epic.TfsId, 0);
        }

        var itemsList = allItems as IList<WorkItemDto> ?? allItems.ToList();

        var features = itemsList
            .Where(w => w.ParentTfsId == epic.TfsId &&
                        string.Equals(w.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (features.Count == 0)
        {
            return new EpicRefinementScore(epic.TfsId, 30);
        }

        var averageScore = (int)Math.Round(
            features.Average(f =>
                doneItemIds.Contains(f.TfsId) ? 100.0 : ComputeFeatureScore(f, itemsList, doneItemIds).Score));

        return new EpicRefinementScore(epic.TfsId, averageScore);
    }

    /// <summary>
    /// Computes the refinement score for a single Epic.
    /// </summary>
    /// <param name="epic">The Epic work item.</param>
    /// <param name="allItems">All work items in the loaded graph (used to find Feature children).</param>
    /// <returns>Score for the Epic.</returns>
    public EpicRefinementScore ComputeEpicScore(WorkItemDto epic, IEnumerable<WorkItemDto> allItems)
        => ComputeEpicScore(epic, allItems, EmptyDoneIds);
}
