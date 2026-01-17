using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineRunsQuery.
/// Returns pipeline runs for a specific pipeline from the configured data source.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetPipelineRunsQueryHandler : IQueryHandler<GetPipelineRunsQuery, IEnumerable<PipelineRunDto>>
{
    private readonly PipelineReadProviderFactory _providerFactory;

    public GetPipelineRunsQueryHandler(PipelineReadProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public async ValueTask<IEnumerable<PipelineRunDto>> Handle(
        GetPipelineRunsQuery query,
        CancellationToken cancellationToken)
    {
        var provider = _providerFactory.Create();
        return await provider.GetRunsAsync(query.PipelineId, query.Top, cancellationToken);
    }
}
