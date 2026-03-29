using Mediator;
using PoTool.Api.Services.BuildQuality;
using PoTool.Core.BuildQuality.Queries;
using PoTool.Shared.BuildQuality;

namespace PoTool.Api.Handlers.BuildQuality;

/// <summary>
/// Handler for pipeline- or repository-scoped BuildQuality detail queries.
/// </summary>
public sealed class GetBuildQualityPipelineDetailQueryHandler
    : IQueryHandler<GetBuildQualityPipelineDetailQuery, PipelineBuildQualityDto>
{
    private readonly IBuildQualityReadStore _readStore;
    private readonly IBuildQualityProvider _buildQualityProvider;

    public GetBuildQualityPipelineDetailQueryHandler(
        IBuildQualityReadStore readStore,
        IBuildQualityProvider buildQualityProvider)
    {
        _readStore = readStore;
        _buildQualityProvider = buildQualityProvider;
    }

    public async ValueTask<PipelineBuildQualityDto> Handle(
        GetBuildQualityPipelineDetailQuery query,
        CancellationToken cancellationToken)
    {
        var sprint = await _readStore.GetSprintWindowAsync(query.SprintId, cancellationToken);

        if (sprint is null || !sprint.StartDateUtc.HasValue || !sprint.EndDateUtc.HasValue)
        {
            return BuildEmptyResult(query);
        }

        var selection = await _readStore.GetScopeSelectionAsync(
            query.ProductOwnerId,
            sprint.StartDateUtc.Value,
            sprint.EndDateUtc.Value,
            query.RepositoryId,
            query.PipelineDefinitionId,
            cancellationToken);

        var selectedProduct = selection.Products.SingleOrDefault();
        var selectedPipeline = query.PipelineDefinitionId.HasValue
            ? selection.PipelineDefinitions.SingleOrDefault(definition => definition.ExternalPipelineDefinitionId == query.PipelineDefinitionId.Value)
            : null;
        var selectedRepository = query.RepositoryId.HasValue
            ? selection.PipelineDefinitions.FirstOrDefault(definition => definition.RepositoryId == query.RepositoryId.Value)
            : null;

        return new PipelineBuildQualityDto
        {
            ProductOwnerId = query.ProductOwnerId,
            SprintId = sprint.Id,
            SprintName = sprint.Name,
            TeamId = sprint.TeamId,
            SprintStartUtc = sprint.StartDateUtc,
            SprintEndUtc = sprint.EndDateUtc,
            ProductId = selectedProduct?.ProductId,
            RepositoryId = query.RepositoryId,
            RepositoryName = selectedRepository?.RepositoryName,
            PipelineDefinitionId = query.PipelineDefinitionId,
            PipelineName = selectedPipeline?.PipelineName,
            DefaultBranches = selection.DefaultBranches,
            Result = _buildQualityProvider.Compute(selection.Builds, selection.TestRuns, selection.Coverages)
        };
    }

    private PipelineBuildQualityDto BuildEmptyResult(GetBuildQualityPipelineDetailQuery query)
    {
        return new PipelineBuildQualityDto
        {
            ProductOwnerId = query.ProductOwnerId,
            SprintId = query.SprintId,
            RepositoryId = query.RepositoryId,
            PipelineDefinitionId = query.PipelineDefinitionId,
            Result = _buildQualityProvider.Compute(Array.Empty<BuildQualityBuildFact>(), Array.Empty<BuildQualityTestRunFact>(), Array.Empty<BuildQualityCoverageFact>())
        };
    }
}
