using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings.Teams;

/// <summary>
/// Handler for getting all teams.
/// </summary>
public class GetAllTeamsQueryHandler : IQueryHandler<GetAllTeamsQuery, IEnumerable<TeamDto>>
{
    private readonly ITeamRepository _repository;

    public GetAllTeamsQueryHandler(ITeamRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<TeamDto>> Handle(GetAllTeamsQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetAllTeamsAsync(query.IncludeArchived, cancellationToken);
    }
}
