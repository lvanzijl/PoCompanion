using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get all products that belong to a project resolved by alias or internal identifier.
/// </summary>
public sealed record GetProjectProductsQuery(string AliasOrId) : IQuery<IEnumerable<ProductDto>>;
