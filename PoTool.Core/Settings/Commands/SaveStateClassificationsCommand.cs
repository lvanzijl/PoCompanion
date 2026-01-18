using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to save work item state classifications.
/// </summary>
public sealed record SaveStateClassificationsCommand(SaveStateClassificationsRequest Request) : IRequest<bool>
{
}
