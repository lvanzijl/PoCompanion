using Mediator;
using PoTool.Shared.Health;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Returns a lightweight Health workspace summary for a single product.
/// </summary>
public sealed record GetHealthWorkspaceProductSummaryQuery(
    int ProductId
) : IQuery<HealthWorkspaceProductSummaryDto?>;
