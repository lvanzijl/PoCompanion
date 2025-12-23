using Mediator;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to delete a profile.
/// </summary>
public sealed record DeleteProfileCommand(int Id) : ICommand<bool>;
