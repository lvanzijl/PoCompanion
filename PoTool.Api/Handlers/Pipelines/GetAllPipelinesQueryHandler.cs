using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetAllPipelinesQuery.
/// Returns all pipelines from the cached analytical read model.
/// </summary>
public sealed class GetAllPipelinesQueryHandler : IQueryHandler<GetAllPipelinesQuery, IEnumerable<PipelineDto>>
{
    private readonly IPipelineReadProvider _pipelineReadProvider;

    public GetAllPipelinesQueryHandler(IPipelineReadProvider pipelineReadProvider)
    {
        _pipelineReadProvider = pipelineReadProvider;
    }

    public async ValueTask<IEnumerable<PipelineDto>> Handle(
        GetAllPipelinesQuery query,
        CancellationToken cancellationToken)
    {
        return await _pipelineReadProvider.GetAllAsync(cancellationToken);
    }
}
