using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for getting a profile by ID.
/// </summary>
public class GetProfileByIdQueryHandler : IQueryHandler<GetProfileByIdQuery, ProfileDto?>
{
    private readonly IProfileRepository _repository;

    public GetProfileByIdQueryHandler(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<ProfileDto?> Handle(GetProfileByIdQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetProfileByIdAsync(query.Id, cancellationToken);
    }
}
