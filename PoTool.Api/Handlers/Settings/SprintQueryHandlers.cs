using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for GetSprintsForTeamQuery.
/// </summary>
public sealed class GetSprintsForTeamQueryHandler : IQueryHandler<GetSprintsForTeamQuery, IEnumerable<SprintDto>>
{
    private readonly ISprintRepository _sprintRepository;

    public GetSprintsForTeamQueryHandler(ISprintRepository sprintRepository)
    {
        _sprintRepository = sprintRepository;
    }

    public async ValueTask<IEnumerable<SprintDto>> Handle(GetSprintsForTeamQuery query, CancellationToken cancellationToken)
    {
        var sprints = await _sprintRepository.GetSprintsForTeamAsync(query.TeamId);
        // Return sprints ordered by start date descending (most recent first)
        return sprints.OrderByDescending(s => s.StartUtc.HasValue ? s.StartUtc.Value.UtcDateTime : DateTime.MinValue);
    }
}

/// <summary>
/// Handler for GetCurrentSprintForTeamQuery.
/// </summary>
public sealed class GetCurrentSprintForTeamQueryHandler : IQueryHandler<GetCurrentSprintForTeamQuery, SprintDto?>
{
    private readonly ISprintRepository _sprintRepository;

    public GetCurrentSprintForTeamQueryHandler(ISprintRepository sprintRepository)
    {
        _sprintRepository = sprintRepository;
    }

    public async ValueTask<SprintDto?> Handle(GetCurrentSprintForTeamQuery query, CancellationToken cancellationToken)
    {
        return await _sprintRepository.GetCurrentSprintForTeamAsync(query.TeamId);
    }
}
