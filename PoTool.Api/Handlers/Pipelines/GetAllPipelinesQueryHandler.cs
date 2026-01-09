using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetAllPipelinesQuery.
/// Returns all cached pipelines.
/// </summary>
public sealed class GetAllPipelinesQueryHandler : IQueryHandler<GetAllPipelinesQuery, IEnumerable<PipelineDto>>
{
    private readonly IPipelineRepository _repository;

    public GetAllPipelinesQueryHandler(IPipelineRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<PipelineDto>> Handle(
        GetAllPipelinesQuery query,
        CancellationToken cancellationToken)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}
