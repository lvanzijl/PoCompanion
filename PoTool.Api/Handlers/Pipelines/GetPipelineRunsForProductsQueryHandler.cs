using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines.Filters;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineRunsForProductsQuery.
/// Returns cached pipeline runs for the requested analytical filter scope.
/// </summary>
public sealed class GetPipelineRunsForProductsQueryHandler : IQueryHandler<GetPipelineRunsForProductsQuery, IEnumerable<PipelineRunDto>>
{
    private readonly IPipelineReadProvider _pipelineReadProvider;

    public GetPipelineRunsForProductsQueryHandler(IPipelineReadProvider pipelineReadProvider)
    {
        _pipelineReadProvider = pipelineReadProvider;
    }

    public async ValueTask<IEnumerable<PipelineRunDto>> Handle(
        GetPipelineRunsForProductsQuery query,
        CancellationToken cancellationToken)
    {
        var filter = query.EffectiveFilter;
        if (filter.PipelineIds.Count == 0 || filter.RepositoryScope.Count == 0)
        {
            return Enumerable.Empty<PipelineRunDto>();
        }

        var runs = await _pipelineReadProvider.GetRunsForPipelinesAsync(
            filter.PipelineIds,
            branchName: null,
            minFinishTime: filter.RangeStartUtc,
            maxFinishTime: filter.RangeEndUtc,
            branchScope: filter.BranchScope,
            top: 100,
            cancellationToken);

        return PipelineFiltering.ApplyRunScope(runs, filter);
    }
}
