using Mediator;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve a grouped validation triage summary for display on the Validation Triage page.
/// Groups validation issues by category and rule ID, returning counts and top rule groups per category.
/// </summary>
/// <param name="ProductIds">Optional list of product IDs to filter by. If null or empty, uses all products for the active profile.</param>
public sealed record GetValidationTriageSummaryQuery(int[]? ProductIds = null) : IQuery<ValidationTriageSummaryDto>;
