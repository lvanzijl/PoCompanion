using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Settings.Projects;

/// <summary>
/// Handler for getting a project by alias or internal identifier.
/// </summary>
public class GetProjectByAliasQueryHandler : IQueryHandler<GetProjectByAliasQuery, ProjectDto?>
{
    private readonly IProjectRepository _repository;

    public GetProjectByAliasQueryHandler(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<ProjectDto?> Handle(GetProjectByAliasQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetProjectByAliasOrIdAsync(query.AliasOrId, cancellationToken);
    }
}
