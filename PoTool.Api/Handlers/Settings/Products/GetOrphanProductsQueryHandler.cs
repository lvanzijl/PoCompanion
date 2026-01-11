using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for getting orphan products.
/// </summary>
public class GetOrphanProductsQueryHandler : IQueryHandler<GetOrphanProductsQuery, IEnumerable<ProductDto>>
{
    private readonly IProductRepository _repository;

    public GetOrphanProductsQueryHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<ProductDto>> Handle(GetOrphanProductsQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetOrphanProductsAsync(cancellationToken);
    }
}
