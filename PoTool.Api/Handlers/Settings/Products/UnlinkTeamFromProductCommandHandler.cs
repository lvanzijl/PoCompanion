using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Products;

/// <summary>
/// Handler for unlinking a team from a product.
/// </summary>
public class UnlinkTeamFromProductCommandHandler : ICommandHandler<UnlinkTeamFromProductCommand, bool>
{
    private readonly IProductRepository _repository;

    public UnlinkTeamFromProductCommandHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<bool> Handle(UnlinkTeamFromProductCommand command, CancellationToken cancellationToken)
    {
        return await _repository.UnlinkTeamAsync(command.ProductId, command.TeamId, cancellationToken);
    }
}
