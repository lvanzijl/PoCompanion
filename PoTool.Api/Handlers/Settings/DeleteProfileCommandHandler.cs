using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for deleting a profile.
/// </summary>
public class DeleteProfileCommandHandler : ICommandHandler<DeleteProfileCommand, bool>
{
    private readonly IProfileRepository _repository;

    public DeleteProfileCommandHandler(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<bool> Handle(DeleteProfileCommand command, CancellationToken cancellationToken)
    {
        return await _repository.DeleteProfileAsync(command.Id, cancellationToken);
    }
}
