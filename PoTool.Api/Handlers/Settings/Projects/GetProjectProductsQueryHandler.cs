using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Settings.Projects;

/// <summary>
/// Handler for getting all products for a project resolved by alias or internal identifier.
/// </summary>
public class GetProjectProductsQueryHandler : IQueryHandler<GetProjectProductsQuery, IEnumerable<ProductDto>>
{
    private readonly IProjectRepository _repository;

    public GetProjectProductsQueryHandler(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<ProductDto>> Handle(GetProjectProductsQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetProjectProductsAsync(query.AliasOrId, cancellationToken);
    }
}
