namespace PoTool.Shared.Health;

/// <summary>
/// Readiness score for a single PBI (Product Backlog Item).
/// Owner is always Team per the Backlog State Model.
/// </summary>
public sealed class PbiReadinessDto
{
    /// <summary>TFS identifier of the PBI.</summary>
    public required int TfsId { get; init; }

    /// <summary>
    /// Readiness score: 0 (no description), 75 (description, no effort), or 100 (description + effort).
    /// </summary>
    public required int Score { get; init; }

    /// <summary>
    /// Display title of the PBI work item.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Story-point effort for this PBI as stored in TFS. Null when not set.
    /// Used to compute total ready work on the health dashboard.
    /// </summary>
    public int? Effort { get; init; }
}

/// <summary>
/// Refinement score and ownership state for a single Feature.
/// </summary>
public sealed class FeatureRefinementDto
{
    /// <summary>TFS identifier of the Feature.</summary>
    public required int TfsId { get; init; }

    /// <summary>Display title of the Feature work item.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// Refinement score: 0 (no description), 25 (no PBIs), or average PBI score otherwise.
    /// </summary>
    public required int Score { get; init; }

    /// <summary>
    /// Dynamic ownership state: PO (description missing), Team (score &lt; 100), Ready (score == 100).
    /// </summary>
    public required FeatureOwnerState OwnerState { get; init; }

    /// <summary>Per-PBI readiness scores for this feature's children.</summary>
    public required IReadOnlyList<PbiReadinessDto> Pbis { get; init; }
}

/// <summary>
/// Refinement score for a single Epic.
/// Owner is always Product Owner per the Backlog State Model.
/// </summary>
public sealed class EpicRefinementDto
{
    /// <summary>TFS identifier of the Epic.</summary>
    public required int TfsId { get; init; }

    /// <summary>Display title of the Epic work item.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// Refinement score: 0 (no description), 30 (no Features), or average Feature score otherwise.
    /// </summary>
    public required int Score { get; init; }

    /// <summary>Per-Feature refinement scores for this epic's children.</summary>
    public required IReadOnlyList<FeatureRefinementDto> Features { get; init; }
}

/// <summary>
/// Product-scoped backlog state response.
/// Contains hierarchical refinement scores for all Epics belonging to a product.
/// </summary>
public sealed class ProductBacklogStateDto
{
    /// <summary>Product identifier.</summary>
    public required int ProductId { get; init; }

    /// <summary>Refinement scores for all Epics in this product.</summary>
    public required IReadOnlyList<EpicRefinementDto> Epics { get; init; }
}
