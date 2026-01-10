using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get all orphaned products (products with no Product Owner).
/// </summary>
public sealed record GetOrphanProductsQuery() : IQuery<IEnumerable<ProductDto>>;
