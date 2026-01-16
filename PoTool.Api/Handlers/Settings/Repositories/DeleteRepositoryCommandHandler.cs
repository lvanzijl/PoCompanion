using Mediator;
using PoTool.Api.Repositories;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Repositories;

/// <summary>
/// Handler for deleting a repository configuration.
/// </summary>
public class DeleteRepositoryCommandHandler : ICommandHandler<DeleteRepositoryCommand>
{
    private readonly RepositoryRepository _repository;

    public DeleteRepositoryCommandHandler(RepositoryRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<Unit> Handle(DeleteRepositoryCommand command, CancellationToken cancellationToken)
    {
        await _repository.DeleteRepositoryAsync(command.RepositoryId, cancellationToken);
        return Unit.Value;
    }
}
