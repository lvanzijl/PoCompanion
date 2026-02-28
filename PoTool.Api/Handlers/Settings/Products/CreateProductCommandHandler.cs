using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for creating a new product.
/// </summary>
public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, ProductDto>
{
    private readonly IProductRepository _repository;

    public CreateProductCommandHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<ProductDto> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        return await _repository.CreateProductAsync(
            command.ProductOwnerId,
            command.Name,
            command.BacklogRootWorkItemIds,
            command.PictureType,
            command.DefaultPictureId,
            command.CustomPicturePath,
            cancellationToken);
    }
}
