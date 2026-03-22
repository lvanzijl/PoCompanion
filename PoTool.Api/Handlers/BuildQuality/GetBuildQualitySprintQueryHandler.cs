using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Services.BuildQuality;
using PoTool.Core.BuildQuality.Queries;
using PoTool.Shared.BuildQuality;

namespace PoTool.Api.Handlers.BuildQuality;

/// <summary>
/// Handler for sprint-window BuildQuality queries.
/// </summary>
public sealed class GetBuildQualitySprintQueryHandler
    : IQueryHandler<GetBuildQualitySprintQuery, DeliveryBuildQualityDto>
{
    private readonly PoToolDbContext _context;
    private readonly BuildQualityScopeLoader _scopeLoader;
    private readonly IBuildQualityProvider _buildQualityProvider;

    public GetBuildQualitySprintQueryHandler(
        PoToolDbContext context,
        BuildQualityScopeLoader scopeLoader,
        IBuildQualityProvider buildQualityProvider)
    {
        _context = context;
        _scopeLoader = scopeLoader;
        _buildQualityProvider = buildQualityProvider;
    }

    public async ValueTask<DeliveryBuildQualityDto> Handle(
        GetBuildQualitySprintQuery query,
        CancellationToken cancellationToken)
    {
        var sprint = await _context.Sprints
            .AsNoTracking()
            .FirstOrDefaultAsync(sprint => sprint.Id == query.SprintId, cancellationToken);

        if (sprint is null || !sprint.StartDateUtc.HasValue || !sprint.EndDateUtc.HasValue)
        {
            return BuildEmptyResult(query.ProductOwnerId, query.SprintId);
        }

        var selection = await _scopeLoader.LoadAsync(
            query.ProductOwnerId,
            sprint.StartDateUtc.Value,
            sprint.EndDateUtc.Value,
            repositoryId: null,
            pipelineDefinitionId: null,
            cancellationToken);

        return new DeliveryBuildQualityDto
        {
            ProductOwnerId = query.ProductOwnerId,
            SprintId = sprint.Id,
            SprintName = sprint.Name,
            TeamId = sprint.TeamId,
            SprintStartUtc = sprint.StartDateUtc,
            SprintEndUtc = sprint.EndDateUtc,
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

    private DeliveryBuildQualityDto BuildEmptyResult(int productOwnerId, int sprintId)
    {
        return new DeliveryBuildQualityDto
        {
            ProductOwnerId = productOwnerId,
            SprintId = sprintId,
            Summary = _buildQualityProvider.Compute(Array.Empty<BuildQualityBuildFact>(), Array.Empty<BuildQualityTestRunFact>(), Array.Empty<BuildQualityCoverageFact>())
        };
    }
}
