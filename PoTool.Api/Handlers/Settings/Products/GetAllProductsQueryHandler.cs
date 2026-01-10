using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for getting all products.
/// </summary>
public class GetAllProductsQueryHandler : IQueryHandler<GetAllProductsQuery, IEnumerable<ProductDto>>
{
    private readonly IProductRepository _repository;

    public GetAllProductsQueryHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<ProductDto>> Handle(GetAllProductsQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetAllProductsAsync(cancellationToken);
    }
}
