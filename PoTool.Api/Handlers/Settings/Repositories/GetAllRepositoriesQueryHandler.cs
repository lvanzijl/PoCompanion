using Mediator;
using PoTool.Api.Repositories;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings.Repositories;

/// <summary>
/// Handler for retrieving all repositories in the system.
/// </summary>
public class GetAllRepositoriesQueryHandler : IQueryHandler<GetAllRepositoriesQuery, IEnumerable<RepositoryDto>>
{
    private readonly RepositoryRepository _repository;

    public GetAllRepositoriesQueryHandler(RepositoryRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<RepositoryDto>> Handle(GetAllRepositoriesQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetAllRepositoriesAsync(cancellationToken);
    }
}
