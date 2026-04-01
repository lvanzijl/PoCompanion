using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Settings.Projects;

/// <summary>
/// Handler for getting all projects.
/// </summary>
public class GetAllProjectsQueryHandler : IQueryHandler<GetAllProjectsQuery, IEnumerable<ProjectDto>>
{
    private readonly IProjectRepository _repository;

    public GetAllProjectsQueryHandler(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<ProjectDto>> Handle(GetAllProjectsQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetAllProjectsAsync(cancellationToken);
    }
}
