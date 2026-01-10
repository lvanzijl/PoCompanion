using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for reordering products.
/// </summary>
public class ReorderProductsCommandHandler : ICommandHandler<ReorderProductsCommand, List<ProductDto>>
{
    private readonly IProductRepository _repository;

    public ReorderProductsCommandHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<List<ProductDto>> Handle(ReorderProductsCommand command, CancellationToken cancellationToken)
    {
        var products = await _repository.ReorderProductsAsync(
            command.ProductOwnerId,
            command.ProductIds,
            cancellationToken);

        return products.ToList();
    }
}
