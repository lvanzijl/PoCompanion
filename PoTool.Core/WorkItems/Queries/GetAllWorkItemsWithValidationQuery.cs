using Mediator;

using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve cached work items with validation results.
/// Optionally filtered by product IDs.
/// </summary>
/// <param name="ProductIds">Optional list of product IDs to filter by. If null or empty, uses all products for the active profile.</param>
public sealed record GetAllWorkItemsWithValidationQuery(int[]? ProductIds = null) : IQuery<IEnumerable<WorkItemWithValidationDto>>;
