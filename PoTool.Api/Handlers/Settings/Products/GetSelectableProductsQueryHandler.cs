using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for getting selectable products for a Product Owner.
/// </summary>
public class GetSelectableProductsQueryHandler : IQueryHandler<GetSelectableProductsQuery, IEnumerable<ProductDto>>
{
    private readonly IProductRepository _repository;

    public GetSelectableProductsQueryHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<ProductDto>> Handle(GetSelectableProductsQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetSelectableProductsAsync(query.ProductOwnerId, cancellationToken);
    }
}
