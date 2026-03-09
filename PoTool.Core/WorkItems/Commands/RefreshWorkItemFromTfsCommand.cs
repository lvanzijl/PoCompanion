using Mediator;

namespace PoTool.Core.WorkItems.Commands;

/// <summary>
/// Command to re-fetch a single work item from TFS and update the local DB.
/// Used by the Fix Session "Refresh from TFS" feature.
/// </summary>
/// <param name="TfsId">TFS work item ID to refresh.</param>
public sealed record RefreshWorkItemFromTfsCommand(int TfsId) : ICommand<bool>;

/// <summary>
/// Command to re-fetch a product-scoped work item hierarchy from TFS and update the local DB cache.
/// Used by the Plan Board "Refresh from TFS" feature.
/// </summary>
/// <param name="RootIds">Root work item IDs that define the hierarchy to refresh.</param>
public sealed record RefreshWorkItemsByRootIdsFromTfsCommand(int[] RootIds) : ICommand<int>;
