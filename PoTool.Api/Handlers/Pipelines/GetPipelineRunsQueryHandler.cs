using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineRunsQuery.
/// Returns cached pipeline runs for a specific pipeline.
/// </summary>
public sealed class GetPipelineRunsQueryHandler : IQueryHandler<GetPipelineRunsQuery, IEnumerable<PipelineRunDto>>
{
    private readonly IPipelineReadProvider _pipelineReadProvider;

    public GetPipelineRunsQueryHandler(IPipelineReadProvider pipelineReadProvider)
    {
        _pipelineReadProvider = pipelineReadProvider;
    }

    public async ValueTask<IEnumerable<PipelineRunDto>> Handle(
        GetPipelineRunsQuery query,
        CancellationToken cancellationToken)
    {
        return await _pipelineReadProvider.GetRunsAsync(query.PipelineId, query.Top, cancellationToken);
    }
}
