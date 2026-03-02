using Mediator;
using PoTool.Shared.Health;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to get the product-scoped backlog state (refinement scores per Epic/Feature/PBI).
/// Returns null when the product is not found or has no backlog roots configured.
/// </summary>
public sealed record GetProductBacklogStateQuery(
    int ProductId
) : IQuery<ProductBacklogStateDto?>;
