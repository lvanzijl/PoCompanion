using Mediator;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to delete a repository configuration.
/// </summary>
/// <param name="RepositoryId">ID of the repository to delete</param>
public sealed record DeleteRepositoryCommand(
    int RepositoryId
) : ICommand;
