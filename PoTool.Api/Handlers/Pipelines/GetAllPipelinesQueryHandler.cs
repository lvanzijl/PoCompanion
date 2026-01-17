using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetAllPipelinesQuery.
/// Returns all pipelines from the configured data source.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetAllPipelinesQueryHandler : IQueryHandler<GetAllPipelinesQuery, IEnumerable<PipelineDto>>
{
    private readonly PipelineReadProviderFactory _providerFactory;

    public GetAllPipelinesQueryHandler(PipelineReadProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public async ValueTask<IEnumerable<PipelineDto>> Handle(
        GetAllPipelinesQuery query,
        CancellationToken cancellationToken)
    {
        var provider = _providerFactory.Create();
        return await provider.GetAllAsync(cancellationToken);
    }
}
