using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get all products in the system, regardless of owner.
/// </summary>
public sealed record GetAllProductsQuery() : IQuery<IEnumerable<ProductDto>>;
