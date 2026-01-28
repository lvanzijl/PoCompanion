using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineRunsForProductsQuery.
/// Returns all pipeline runs for specified products from the last 6 months.
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
        // Filter by product IDs to get allowed pipeline IDs
        IEnumerable<int> allowedPipelineIds;
        
        if (query.ProductIds != null && query.ProductIds.Count > 0)
        {
            // Get pipeline definitions for the specified products
            var allowedPipelineIdSet = new HashSet<int>();
            foreach (var productId in query.ProductIds)
            {
                var definitions = await _pipelineReadProvider.GetDefinitionsByProductIdAsync(productId, cancellationToken);
                foreach (var def in definitions)
                {
                    allowedPipelineIdSet.Add(def.PipelineDefinitionId);
                }
            }
            
            allowedPipelineIds = allowedPipelineIdSet;
            
            // If no pipelines found for these products, return empty
            if (!allowedPipelineIds.Any())
            {
                return Enumerable.Empty<PipelineRunDto>();
            }
        }
        else
        {
            // No product filter - get all pipelines
            var allPipelines = await _pipelineReadProvider.GetAllAsync(cancellationToken);
            allowedPipelineIds = allPipelines.Select(p => p.Id).ToList();
        }

        // Fetch runs for the filtered pipeline IDs from last 6 months on main branch
        var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-6);
        var runs = await _pipelineReadProvider.GetRunsForPipelinesAsync(
            allowedPipelineIds,
            branchName: "refs/heads/main",
            minStartTime: sixMonthsAgo,
            top: 100,
            cancellationToken);
        
        return runs;
    }
}
