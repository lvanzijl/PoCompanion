using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get all repositories configured for a specific product.
/// </summary>
/// <param name="ProductId">ID of the product</param>
public sealed record GetRepositoriesByProductQuery(
    int ProductId
) : IQuery<IEnumerable<RepositoryDto>>;
