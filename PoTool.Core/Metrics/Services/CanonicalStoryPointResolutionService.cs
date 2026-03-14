using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.Metrics.Services;

/// <summary>
/// Resolves canonical story point estimates using the domain estimation rules.
/// </summary>
public interface ICanonicalStoryPointResolutionService
{
    /// <summary>
    /// Resolves the canonical story point estimate for a work item.
    /// </summary>
    /// <param name="request">The work item and optional feature-sibling context.</param>
    /// <returns>The resolved estimate and its source classification.</returns>
    ResolvedStoryPointEstimate Resolve(StoryPointResolutionRequest request);

    /// <summary>
    /// Resolves a parent-level fallback estimate using canonical field precedence without PBI-only restrictions.
    /// </summary>
    /// <param name="request">The parent work item and Done-state context.</param>
    /// <returns>The resolved fallback estimate and its source classification.</returns>
    ResolvedStoryPointEstimate ResolveParentFallback(StoryPointFallbackRequest request);
}

/// <summary>
/// Distinguishes between direct, fallback, derived, and missing estimates.
/// </summary>
public enum StoryPointEstimateSource
{
    Missing,
    Real,
    Fallback,
    Derived
}

/// <summary>
/// A canonical story point estimate and its source classification.
/// </summary>
/// <param name="Value">The resolved story point value, if any.</param>
/// <param name="Source">The source classification for the resolved value.</param>
public sealed record ResolvedStoryPointEstimate(
    double? Value,
    StoryPointEstimateSource Source)
{
    public bool HasValue => Value.HasValue;
}

/// <summary>
/// Candidate work item context used for sibling-based estimate derivation.
/// </summary>
/// <param name="WorkItem">The work item to resolve.</param>
/// <param name="IsDone">Whether the work item is canonically Done.</param>
public sealed record StoryPointResolutionCandidate(
    WorkItemDto WorkItem,
    bool IsDone);

/// <summary>
/// Request for resolving a canonical story point estimate.
/// </summary>
/// <param name="WorkItem">The work item to resolve.</param>
/// <param name="IsDone">Whether the work item is canonically Done.</param>
/// <param name="FeaturePbis">Optional feature-local PBI candidates used for derived estimates.</param>
public sealed record StoryPointResolutionRequest(
    WorkItemDto WorkItem,
    bool IsDone,
    IReadOnlyCollection<StoryPointResolutionCandidate>? FeaturePbis = null);

/// <summary>
/// Request for resolving a non-PBI fallback estimate during parent rollups.
/// </summary>
/// <param name="WorkItem">The parent work item to resolve.</param>
/// <param name="IsDone">Whether the parent work item is canonically Done.</param>
public sealed record StoryPointFallbackRequest(
    WorkItemDto WorkItem,
    bool IsDone);

/// <summary>
/// Implements canonical story point resolution:
/// StoryPoints -> BusinessValue -> Missing, with optional feature-sibling derivation.
/// </summary>
public sealed class CanonicalStoryPointResolutionService : ICanonicalStoryPointResolutionService
{
    private static ResolvedStoryPointEstimate MissingEstimate =>
        new(null, StoryPointEstimateSource.Missing);

    public ResolvedStoryPointEstimate Resolve(StoryPointResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorkItem);

        var directEstimate = ResolveDirect(request.WorkItem, request.IsDone);
        if (directEstimate.Source != StoryPointEstimateSource.Missing)
        {
            return directEstimate;
        }

        return TryResolveDerived(request) ?? MissingEstimate;
    }

    public ResolvedStoryPointEstimate ResolveParentFallback(StoryPointFallbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorkItem);

        return ResolveFallbackEstimate(request.WorkItem, request.IsDone);
    }

    private static ResolvedStoryPointEstimate ResolveDirect(WorkItemDto workItem, bool isDone)
    {
        if (!IsAuthoritativePbi(workItem.Type))
        {
            return MissingEstimate;
        }

        return ResolveFallbackEstimate(workItem, isDone);
    }

    private static ResolvedStoryPointEstimate ResolveFallbackEstimate(WorkItemDto workItem, bool isDone)
    {
        if (workItem.StoryPoints is int storyPoints)
        {
            if (storyPoints != 0 || isDone)
            {
                return new ResolvedStoryPointEstimate(storyPoints, StoryPointEstimateSource.Real);
            }
        }

        if (workItem.BusinessValue is int businessValue)
        {
            return new ResolvedStoryPointEstimate(businessValue, StoryPointEstimateSource.Fallback);
        }

        return MissingEstimate;
    }

    private static ResolvedStoryPointEstimate? TryResolveDerived(StoryPointResolutionRequest request)
    {
        if (!IsAuthoritativePbi(request.WorkItem.Type) || request.WorkItem.ParentTfsId == null)
        {
            return null;
        }

        if (request.FeaturePbis == null || request.FeaturePbis.Count == 0)
        {
            return null;
        }

        var siblingEstimates = request.FeaturePbis
            .Where(candidate => candidate.WorkItem.TfsId != request.WorkItem.TfsId)
            .Where(candidate => candidate.WorkItem.ParentTfsId == request.WorkItem.ParentTfsId)
            .Where(candidate => IsAuthoritativePbi(candidate.WorkItem.Type))
            .Select(candidate => ResolveDirect(candidate.WorkItem, candidate.IsDone))
            .Where(estimate => estimate.HasValue)
            .Select(estimate => estimate.Value!.Value)
            .ToList();

        if (siblingEstimates.Count == 0)
        {
            return null;
        }

        return new ResolvedStoryPointEstimate(
            siblingEstimates.Average(),
            StoryPointEstimateSource.Derived);
    }

    private static bool IsAuthoritativePbi(string workItemType)
    {
        return workItemType.Equals(WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.PbiShort, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.UserStory, StringComparison.OrdinalIgnoreCase);
    }
}
