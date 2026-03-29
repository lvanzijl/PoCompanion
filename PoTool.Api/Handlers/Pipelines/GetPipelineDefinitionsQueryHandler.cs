using Mediator;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines.Queries;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineDefinitionsQuery.
/// Retrieves pipeline definitions for configuration and discovery.
/// This handler resolves the explicit live provider and does not use the default
/// cache-backed analytical pipeline provider.
/// </summary>
public sealed class GetPipelineDefinitionsQueryHandler : IQueryHandler<GetPipelineDefinitionsQuery, IEnumerable<PipelineDefinitionDto>>
{
    private readonly IPipelineReadProvider _livePipelineReadProvider;
    private readonly ILogger<GetPipelineDefinitionsQueryHandler> _logger;

    public GetPipelineDefinitionsQueryHandler(
        IServiceProvider serviceProvider,
        ILogger<GetPipelineDefinitionsQueryHandler> logger)
    {
        _livePipelineReadProvider = serviceProvider.GetRequiredKeyedService<IPipelineReadProvider>("Live");
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

        IEnumerable<PipelineDefinitionDto> definitions;

        if (query.ProductId.HasValue)
        {
            definitions = await _livePipelineReadProvider.GetDefinitionsByProductIdAsync(query.ProductId.Value, cancellationToken);
        }
        else if (query.RepositoryId.HasValue)
        {
            definitions = await _livePipelineReadProvider.GetDefinitionsByRepositoryIdAsync(query.RepositoryId.Value, cancellationToken);
        }
        else
        {
            // This should never happen due to the check above, but satisfies the compiler
            throw new InvalidOperationException(
                "Either ProductId or RepositoryId must be provided when retrieving pipeline definitions.");
        }

        var definitionsList = definitions.ToList();
        _logger.LogInformation("Retrieved {Count} pipeline definitions", definitionsList.Count);

        return definitionsList;
    }
}
