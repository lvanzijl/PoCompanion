using Mediator;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to validate a work item by ID directly from TFS (bypasses cache).
/// Used specifically for validating backlog root work item IDs in product creation/editing.
/// </summary>
public sealed record ValidateWorkItemQuery(int WorkItemId) : IQuery<ValidateWorkItemResponse>;
