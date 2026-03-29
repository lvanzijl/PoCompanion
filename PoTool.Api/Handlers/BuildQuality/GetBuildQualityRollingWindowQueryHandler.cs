using Mediator;
using PoTool.Api.Services.BuildQuality;
using PoTool.Core.BuildQuality.Queries;
using PoTool.Shared.BuildQuality;

namespace PoTool.Api.Handlers.BuildQuality;

/// <summary>
/// Handler for rolling-window BuildQuality queries.
/// </summary>
public sealed class GetBuildQualityRollingWindowQueryHandler
    : IQueryHandler<GetBuildQualityRollingWindowQuery, BuildQualityPageDto>
{
    private readonly IBuildQualityReadStore _readStore;
    private readonly IBuildQualityProvider _buildQualityProvider;

    public GetBuildQualityRollingWindowQueryHandler(
        IBuildQualityReadStore readStore,
        IBuildQualityProvider buildQualityProvider)
    {
        _readStore = readStore;
        _buildQualityProvider = buildQualityProvider;
    }

    public async ValueTask<BuildQualityPageDto> Handle(
        GetBuildQualityRollingWindowQuery query,
        CancellationToken cancellationToken)
    {
        var selection = await _readStore.GetScopeSelectionAsync(
            query.EffectiveFilter.Context.ProductIds.Values,
            query.EffectiveFilter.RangeStartUtc?.UtcDateTime,
            query.EffectiveFilter.RangeEndUtc?.UtcDateTime,
            repositoryId: null,
            pipelineDefinitionId: null,
            cancellationToken);

        return new BuildQualityPageDto
        {
            ProductOwnerId = query.ProductOwnerId,
            WindowStartUtc = query.EffectiveFilter.RangeStartUtc?.UtcDateTime ?? default,
            WindowEndUtc = query.EffectiveFilter.RangeEndUtc?.UtcDateTime ?? default,
            ProductIds = selection.ProductIds,
            DefaultBranches = selection.DefaultBranches,
            Summary = _buildQualityProvider.Compute(selection.Builds, selection.TestRuns, selection.Coverages),
            Products = selection.Products
                .Select(product => new BuildQualityProductDto
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    PipelineDefinitionIds = product.PipelineDefinitionIds,
                    RepositoryIds = product.RepositoryIds,
                    Result = _buildQualityProvider.Compute(product.Builds, product.TestRuns, product.Coverages)
                })
                .ToArray()
        };
    }
}
