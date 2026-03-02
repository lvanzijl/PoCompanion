using Mediator;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve a grouped validation queue for a specific category.
/// Returns all rule groups with item counts for the selected category, sorted by count descending.
/// Used by the Validation Queue page.
/// </summary>
/// <param name="CategoryKey">Category filter: "SI", "RR", "RC", or "EFF".</param>
/// <param name="ProductIds">Optional list of product IDs to filter by. If null or empty, uses all products for the active profile.</param>
public sealed record GetValidationQueueQuery(string CategoryKey, int[]? ProductIds = null) : IQuery<ValidationQueueDto>;
