using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for linking a team to a product.
/// </summary>
public class LinkTeamToProductCommandHandler : ICommandHandler<LinkTeamToProductCommand, bool>
{
    private readonly IProductRepository _repository;

    public LinkTeamToProductCommandHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<bool> Handle(LinkTeamToProductCommand command, CancellationToken cancellationToken)
    {
        return await _repository.LinkTeamAsync(command.ProductId, command.TeamId, cancellationToken);
    }
}
