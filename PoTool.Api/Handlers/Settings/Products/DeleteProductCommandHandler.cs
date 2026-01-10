using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for deleting a product.
/// </summary>
public class DeleteProductCommandHandler : ICommandHandler<DeleteProductCommand, bool>
{
    private readonly IProductRepository _repository;

    public DeleteProductCommandHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<bool> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        return await _repository.DeleteProductAsync(command.Id, cancellationToken);
    }
}
