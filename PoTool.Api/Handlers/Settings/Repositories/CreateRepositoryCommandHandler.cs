using Mediator;
using PoTool.Api.Repositories;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Repositories;

/// <summary>
/// Handler for creating a new repository configuration.
/// </summary>
public class CreateRepositoryCommandHandler : ICommandHandler<CreateRepositoryCommand, RepositoryDto>
{
    private readonly RepositoryRepository _repository;

    public CreateRepositoryCommandHandler(RepositoryRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<RepositoryDto> Handle(CreateRepositoryCommand command, CancellationToken cancellationToken)
    {
        return await _repository.CreateRepositoryAsync(
            command.ProductId,
            command.Name,
            cancellationToken);
    }
}
