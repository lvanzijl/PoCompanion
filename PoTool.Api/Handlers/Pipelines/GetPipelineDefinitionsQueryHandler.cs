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
    private readonly IPipelineReadProvider _pipelineReadProvider;
    private readonly ILogger<GetPipelineDefinitionsQueryHandler> _logger;

    public GetPipelineDefinitionsQueryHandler(
        IPipelineReadProvider pipelineReadProvider,
        ILogger<GetPipelineDefinitionsQueryHandler> logger)
    {
        _pipelineReadProvider = pipelineReadProvider;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PipelineDefinitionDto>> Handle(
        GetPipelineDefinitionsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving pipeline definitions (ProductId={ProductId}, RepositoryId={RepositoryId})",
            query.ProductId, query.RepositoryId);

        // ProductId or RepositoryId must be provided
        if (!query.ProductId.HasValue && !query.RepositoryId.HasValue)
        {
            throw new InvalidOperationException(
                "Either ProductId or RepositoryId must be provided when retrieving pipeline definitions.");
        }

        // Live-only mode: use injected provider directly
        IEnumerable<PipelineDefinitionDto> definitions;

        if (query.ProductId.HasValue)
        {
            definitions = await _pipelineReadProvider.GetDefinitionsByProductIdAsync(query.ProductId.Value, cancellationToken);
        }
        else
        {
            definitions = await _pipelineReadProvider.GetDefinitionsByRepositoryIdAsync(query.RepositoryId!.Value, cancellationToken);
        }

        var definitionsList = definitions.ToList();
        _logger.LogInformation("Retrieved {Count} pipeline definitions", definitionsList.Count);

        return definitionsList;
    }
}
