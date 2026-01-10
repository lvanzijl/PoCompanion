using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to reorder products for a Product Owner.
/// </summary>
/// <param name="ProductOwnerId">ID of the Product Owner</param>
/// <param name="ProductIds">Ordered list of product IDs (first = order 0, etc.)</param>
public sealed record ReorderProductsCommand(
    int ProductOwnerId,
    List<int> ProductIds
) : ICommand<List<ProductDto>>;
