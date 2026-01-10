using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get all products for a Product Owner.
/// </summary>
/// <param name="ProductOwnerId">ID of the Product Owner (Profile)</param>
public sealed record GetProductsByOwnerQuery(int ProductOwnerId) : IQuery<IEnumerable<ProductDto>>;
