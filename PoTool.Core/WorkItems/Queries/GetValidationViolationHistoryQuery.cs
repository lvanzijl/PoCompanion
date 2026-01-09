using Mediator;

using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve historical validation violations.
/// Enables tracking of violation patterns over time per team/area.
/// </summary>
public sealed record GetValidationViolationHistoryQuery(
    string? AreaPathFilter = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    string? ViolationType = null
) : IQuery<IEnumerable<ValidationViolationHistoryDto>>;
