using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to refresh cached validation results for all Epics on the board.
/// </summary>
public sealed record RefreshValidationCacheCommand : ICommand<ValidationCacheResultDto>;
