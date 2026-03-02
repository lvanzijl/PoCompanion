using Mediator;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve a validation fix session for a specific rule.
/// Returns all work items that violate the rule, ordered by TFS ID.
/// Used by the Validation Fix Session page.
/// </summary>
/// <param name="RuleId">The rule identifier to fix (e.g. "SI-1", "RC-2").</param>
/// <param name="CategoryKey">Category key for the rule (e.g. "SI", "RR", "RC", "EFF").</param>
/// <param name="ProductIds">Optional list of product IDs to filter by. If null or empty, uses all products for the active profile.</param>
public sealed record GetValidationFixSessionQuery(
    string RuleId,
    string CategoryKey,
    int[]? ProductIds = null
) : IQuery<ValidationFixSessionDto>;
