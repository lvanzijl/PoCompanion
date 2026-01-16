using Mediator;
using PoTool.Api.Repositories;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings.Repositories;

/// <summary>
/// Handler for retrieving all repositories for a product.
/// </summary>
public class GetRepositoriesByProductQueryHandler : IQueryHandler<GetRepositoriesByProductQuery, IEnumerable<RepositoryDto>>
{
    private readonly RepositoryRepository _repository;

    public GetRepositoriesByProductQueryHandler(RepositoryRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<RepositoryDto>> Handle(GetRepositoriesByProductQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetRepositoriesByProductAsync(query.ProductId, cancellationToken);
    }
}
