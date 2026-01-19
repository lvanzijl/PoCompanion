using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get work item state classifications for the configured TFS project.
/// </summary>
public sealed record GetStateClassificationsQuery : IRequest<GetStateClassificationsResponse>
{
    // No parameters needed - uses the configured TFS project
}
