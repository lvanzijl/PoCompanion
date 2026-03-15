using PoTool.Core.BacklogQuality;
using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Services;
using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;
using DomainStateClassification = PoTool.Core.Domain.Models.StateClassification;

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
    private readonly BacklogQualityAnalyzer _backlogQualityAnalyzer;

    public BacklogStateComputationService(BacklogQualityAnalyzer? backlogQualityAnalyzer = null)
    {
        _backlogQualityAnalyzer = backlogQualityAnalyzer ?? new BacklogQualityAnalyzer();
    }

    /// <summary>
    /// Computes the readiness score for a single PBI.
    /// </summary>
    /// <param name="pbi">The PBI work item.</param>
    /// <returns>Score 0, 75, or 100.</returns>
    public PbiReadinessScore ComputePbiScore(WorkItemDto pbi)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        var analysis = Analyze([pbi]);
        var score = analysis.ReadinessScores.Single(item => item.WorkItemId == pbi.TfsId);
        return new PbiReadinessScore(score.WorkItemId, score.Score.Value);
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
        if (doneItemIds.Contains(feature.TfsId))
        {
            return new FeatureRefinementScore(feature.TfsId, 100, FeatureOwnerState.Ready);
        }

        var analysis = Analyze(EnsureIncluded(feature, allItems), doneItemIds);
        var score = analysis.ReadinessScores.Single(item => item.WorkItemId == feature.TfsId);
        return new FeatureRefinementScore(
            score.WorkItemId,
            score.Score.Value,
            BacklogQualityDomainAdapter.ToFeatureOwnerState(score.OwnerState ?? ReadinessOwnerState.Team));
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
        if (doneItemIds.Contains(epic.TfsId))
        {
            return new EpicRefinementScore(epic.TfsId, 100);
        }

        var analysis = Analyze(EnsureIncluded(epic, allItems), doneItemIds);
        var score = analysis.ReadinessScores.Single(item => item.WorkItemId == epic.TfsId);
        return new EpicRefinementScore(score.WorkItemId, score.Score.Value);
    }

    /// <summary>
    /// Computes the refinement score for a single Epic.
    /// </summary>
    /// <param name="epic">The Epic work item.</param>
    /// <param name="allItems">All work items in the loaded graph (used to find Feature children).</param>
    /// <returns>Score for the Epic.</returns>
    public EpicRefinementScore ComputeEpicScore(WorkItemDto epic, IEnumerable<WorkItemDto> allItems)
        => ComputeEpicScore(epic, allItems, EmptyDoneIds);

    private BacklogQualityAnalysisResult Analyze(
        IEnumerable<WorkItemDto> workItems,
        IReadOnlySet<int>? doneItemIds = null)
    {
        doneItemIds ??= EmptyDoneIds;
        var graph = BacklogQualityDomainAdapter.CreateGraph(
            workItems,
            item => doneItemIds.Contains(item.TfsId)
                ? DomainStateClassification.Done
                : DomainStateClassification.New);

        return _backlogQualityAnalyzer.Analyze(graph);
    }

    private static IEnumerable<WorkItemDto> EnsureIncluded(WorkItemDto targetItem, IEnumerable<WorkItemDto> workItems)
    {
        var items = workItems as IList<WorkItemDto> ?? workItems.ToList();
        return items.Any(item => item.TfsId == targetItem.TfsId)
            ? items
            : items.Concat([targetItem]);
    }
}
