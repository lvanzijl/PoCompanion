using Mediator;

using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve all cached work items with validation results.
/// </summary>
public sealed record GetAllWorkItemsWithValidationQuery : IQuery<IEnumerable<WorkItemWithValidationDto>>;
