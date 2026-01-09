using Mediator;

using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to analyze the impact of validation violations.
/// Shows blocked work items and provides workflow recommendations.
/// </summary>
public sealed record GetValidationImpactAnalysisQuery(
    string? AreaPathFilter = null,
    string? IterationPathFilter = null
) : IQuery<ValidationImpactAnalysisDto>;
