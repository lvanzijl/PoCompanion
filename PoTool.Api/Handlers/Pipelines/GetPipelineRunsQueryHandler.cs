using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineRunsQuery.
/// Returns pipeline runs for a specific pipeline.
/// </summary>
public sealed class GetPipelineRunsQueryHandler : IQueryHandler<GetPipelineRunsQuery, IEnumerable<PipelineRunDto>>
{
    private readonly IPipelineRepository _repository;

    public GetPipelineRunsQueryHandler(IPipelineRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IEnumerable<PipelineRunDto>> Handle(
        GetPipelineRunsQuery query,
        CancellationToken cancellationToken)
    {
        return await _repository.GetRunsAsync(query.PipelineId, query.Top, cancellationToken);
    }
}
