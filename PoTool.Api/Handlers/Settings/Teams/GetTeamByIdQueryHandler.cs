using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings.Teams;

/// <summary>
/// Handler for getting a team by ID.
/// </summary>
public class GetTeamByIdQueryHandler : IQueryHandler<GetTeamByIdQuery, TeamDto?>
{
    private readonly ITeamRepository _repository;

    public GetTeamByIdQueryHandler(ITeamRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<TeamDto?> Handle(GetTeamByIdQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetTeamByIdAsync(query.Id, cancellationToken);
    }
}
