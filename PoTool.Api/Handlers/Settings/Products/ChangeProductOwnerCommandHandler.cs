using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for changing product owner.
/// </summary>
public class ChangeProductOwnerCommandHandler : ICommandHandler<ChangeProductOwnerCommand, ProductDto>
{
    private readonly IProductRepository _repository;

    public ChangeProductOwnerCommandHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<ProductDto> Handle(ChangeProductOwnerCommand command, CancellationToken cancellationToken)
    {
        return await _repository.ChangeProductOwnerAsync(command.ProductId, command.NewProductOwnerId, cancellationToken);
    }
}
