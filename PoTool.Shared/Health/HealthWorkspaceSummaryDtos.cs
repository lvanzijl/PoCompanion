namespace PoTool.Shared.Health;

/// <summary>
/// Lightweight per-epic summary used by the Health workspace product cards.
/// </summary>
public sealed class HealthWorkspaceEpicSummaryDto
{
    /// <summary>TFS identifier of the epic.</summary>
    public required int TfsId { get; init; }

    /// <summary>Display title of the epic.</summary>
    public required string Title { get; init; }

    /// <summary>Refinement score of the epic.</summary>
    public required int Score { get; init; }

    /// <summary>
    /// Sum of visible PBI effort points under the epic.
    /// </summary>
    public required int Effort { get; init; }
}

/// <summary>
/// Lightweight product summary used by the Health workspace dashboard cards.
/// </summary>
public sealed class HealthWorkspaceProductSummaryDto
{
    /// <summary>Product identifier.</summary>
    public required int ProductId { get; init; }

    /// <summary>
    /// Visible story points that belong to fully ready epics.
    /// </summary>
    public required int ReadyEffort { get; init; }

    /// <summary>
    /// Count of fully ready features that still belong to epics that are not fully ready.
    /// </summary>
    public required int FeaturesReadyInPendingEpics { get; init; }

    /// <summary>
    /// Highest-scoring visible epics, limited for Health workspace display.
    /// </summary>
    public required IReadOnlyList<HealthWorkspaceEpicSummaryDto> TopEpics { get; init; }
}
