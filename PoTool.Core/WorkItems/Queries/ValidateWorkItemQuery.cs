using Mediator;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to validate a work item by ID directly from TFS (bypasses cache).
/// Used specifically for validating backlog root work item IDs in product creation/editing.
/// 
/// This query is designed to ensure that work item validation is always accurate and up-to-date:
/// - Bypasses the local SQLite cache (WorkItemRepository)
/// - Calls ITfsClient.GetWorkItemByIdAsync() which queries TFS directly
/// - Works correctly even when cache is empty or stale
/// - Returns failure if work item doesn't exist in TFS, regardless of cache state
/// </summary>
public sealed record ValidateWorkItemQuery(int WorkItemId) : IQuery<ValidateWorkItemResponse>;
