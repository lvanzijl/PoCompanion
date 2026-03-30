using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings.Teams;

/// <summary>
/// Handler for permanently deleting a team.
/// 
/// DEAD CODE WARNING: This handler is unused. Only handles obsolete DeleteTeamCommand.
/// UI uses ArchiveTeamCommandHandler (soft delete) instead.
/// See docs/reports/2026-03-30-cleanup-phase3-handler-usage-report.md section 3.3
/// </summary>
public class DeleteTeamCommandHandler : ICommandHandler<DeleteTeamCommand, bool>
{
    private readonly ITeamRepository _repository;

    public DeleteTeamCommandHandler(ITeamRepository repository)
    {
        _repository = repository;
    }

#pragma warning disable CS0618, CS0619 // Type or member is obsolete (repository method is obsolete)
    public async ValueTask<bool> Handle(DeleteTeamCommand command, CancellationToken cancellationToken)
    {
        return await _repository.DeleteTeamAsync(command.Id, cancellationToken);
    }
#pragma warning restore CS0618, CS0619
}
