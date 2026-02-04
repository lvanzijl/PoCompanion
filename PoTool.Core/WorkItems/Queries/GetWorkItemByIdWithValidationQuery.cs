using Mediator;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve a single work item by TFS ID with validation results.
/// Optionally filtered by product IDs for efficient loading.
/// </summary>
/// <param name="TfsId">The TFS ID of the work item to retrieve.</param>
/// <param name="ProductIds">Optional list of product IDs to filter by. Used to optimize retrieval from cache.</param>
public sealed record GetWorkItemByIdWithValidationQuery(
    int TfsId,
    int[]? ProductIds = null) : IQuery<WorkItemWithValidationDto?>;
