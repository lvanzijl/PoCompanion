namespace PoTool.Core.Health;

/// <summary>
/// Readiness score for a single PBI (Product Backlog Item).
/// Owner is always Team per the Backlog State Model.
/// </summary>
/// <param name="TfsId">TFS identifier of the PBI.</param>
/// <param name="Score">Readiness score: 0 (no description), 75 (description, no effort), or 100 (description + effort).</param>
public sealed record PbiReadinessScore(int TfsId, int Score);

/// <summary>
/// Refinement score and ownership state for a single Feature.
/// </summary>
/// <param name="TfsId">TFS identifier of the Feature.</param>
/// <param name="Score">Refinement score: 0 (no description), 25 (no PBIs), or average PBI score otherwise.</param>
/// <param name="OwnerState">Dynamic ownership state reflecting current refinement responsibility.</param>
public sealed record FeatureRefinementScore(int TfsId, int Score, FeatureOwnerState OwnerState);

/// <summary>
/// Refinement score for a single Epic.
/// Owner is always Product Owner per the Backlog State Model.
/// </summary>
/// <param name="TfsId">TFS identifier of the Epic.</param>
/// <param name="Score">Refinement score: 0 (no description), 30 (no Features), or average Feature score otherwise.</param>
public sealed record EpicRefinementScore(int TfsId, int Score);
