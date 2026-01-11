using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get products selectable by a specific Product Owner.
/// Returns products owned by this owner plus all orphaned products.
/// </summary>
/// <param name="ProductOwnerId">ID of the Product Owner</param>
public sealed record GetSelectableProductsQuery(int ProductOwnerId) : IQuery<IEnumerable<ProductDto>>;
