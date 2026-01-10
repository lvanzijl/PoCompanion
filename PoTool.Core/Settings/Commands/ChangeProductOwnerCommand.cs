using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to change or remove the Product Owner for a product.
/// </summary>
/// <param name="ProductId">ID of the product to update</param>
/// <param name="NewProductOwnerId">New Product Owner ID, or null to make orphan</param>
public sealed record ChangeProductOwnerCommand(
    int ProductId,
    int? NewProductOwnerId
) : ICommand<ProductDto>;
