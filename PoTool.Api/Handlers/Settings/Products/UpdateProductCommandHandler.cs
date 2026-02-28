using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for updating an existing product.
/// </summary>
public class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand, ProductDto>
{
    private readonly IProductRepository _repository;

    public UpdateProductCommandHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<ProductDto> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        return await _repository.UpdateProductAsync(
            command.Id,
            command.Name,
            command.BacklogRootWorkItemIds,
            command.PictureType,
            command.DefaultPictureId,
            command.CustomPicturePath,
            cancellationToken);
    }
}
