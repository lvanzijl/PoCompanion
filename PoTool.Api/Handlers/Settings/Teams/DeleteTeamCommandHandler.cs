using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Teams;

/// <summary>
/// Handler for permanently deleting a team.
/// </summary>
public class DeleteTeamCommandHandler : ICommandHandler<DeleteTeamCommand, bool>
{
    private readonly ITeamRepository _repository;

    public DeleteTeamCommandHandler(ITeamRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<bool> Handle(DeleteTeamCommand command, CancellationToken cancellationToken)
    {
        return await _repository.DeleteTeamAsync(command.Id, cancellationToken);
    }
}
