using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get a product by ID.
/// </summary>
public sealed record GetProductByIdQuery(int Id) : IQuery<ProductDto?>;
