using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Settings.Projects;

/// <summary>
/// Handler for getting a read-only planning summary for a project.
/// </summary>
public sealed class GetProjectPlanningSummaryQueryHandler
    : IQueryHandler<GetProjectPlanningSummaryQuery, ProjectPlanningSummaryDto?>
{
    private readonly ProjectPlanningSummaryService _service;

    public GetProjectPlanningSummaryQueryHandler(ProjectPlanningSummaryService service)
    {
        _service = service;
    }

    public async ValueTask<ProjectPlanningSummaryDto?> Handle(GetProjectPlanningSummaryQuery query, CancellationToken cancellationToken)
        => await _service.GetSummaryAsync(query.AliasOrId, cancellationToken);
}
