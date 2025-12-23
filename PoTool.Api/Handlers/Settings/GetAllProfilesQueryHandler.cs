using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for getting all profiles.
/// </summary>
public class GetAllProfilesQueryHandler : IQueryHandler<GetAllProfilesQuery, IEnumerable<ProfileDto>>
{
    private readonly IProfileRepository _repository;

    public GetAllProfilesQueryHandler(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<ProfileDto>> Handle(GetAllProfilesQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetAllProfilesAsync(cancellationToken);
    }
}
