using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines.Queries;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineDefinitionsQuery.
/// Retrieves pipeline definitions from the configured data source with optional filtering.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetPipelineDefinitionsQueryHandler : IQueryHandler<GetPipelineDefinitionsQuery, IEnumerable<PipelineDefinitionDto>>
{
    private readonly PipelineReadProviderFactory _providerFactory;
    private readonly ILogger<GetPipelineDefinitionsQueryHandler> _logger;

    public GetPipelineDefinitionsQueryHandler(
        PipelineReadProviderFactory providerFactory,
        ILogger<GetPipelineDefinitionsQueryHandler> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PipelineDefinitionDto>> Handle(
        GetPipelineDefinitionsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving pipeline definitions (ProductId={ProductId}, RepositoryId={RepositoryId})",
            query.ProductId, query.RepositoryId);

        var provider = _providerFactory.Create();
        IEnumerable<PipelineDefinitionDto> definitions;

        if (query.ProductId.HasValue)
        {
            definitions = await provider.GetDefinitionsByProductIdAsync(query.ProductId.Value, cancellationToken);
        }
        else if (query.RepositoryId.HasValue)
        {
            definitions = await provider.GetDefinitionsByRepositoryIdAsync(query.RepositoryId.Value, cancellationToken);
        }
        else
        {
            definitions = await provider.GetAllDefinitionsAsync(cancellationToken);
        }

        var definitionsList = definitions.ToList();
        _logger.LogInformation("Retrieved {Count} pipeline definitions", definitionsList.Count);

        return definitionsList;
    }
}
