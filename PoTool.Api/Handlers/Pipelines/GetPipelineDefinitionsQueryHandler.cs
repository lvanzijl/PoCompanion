using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines.Queries;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for GetPipelineDefinitionsQuery.
/// Retrieves pipeline definitions from the database with optional filtering.
/// </summary>
public sealed class GetPipelineDefinitionsQueryHandler : IQueryHandler<GetPipelineDefinitionsQuery, IEnumerable<PipelineDefinitionDto>>
{
    private readonly IPipelineRepository _repository;
    private readonly ILogger<GetPipelineDefinitionsQueryHandler> _logger;

    public GetPipelineDefinitionsQueryHandler(
        IPipelineRepository repository,
        ILogger<GetPipelineDefinitionsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PipelineDefinitionDto>> Handle(
        GetPipelineDefinitionsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving pipeline definitions (ProductId={ProductId}, RepositoryId={RepositoryId})",
            query.ProductId, query.RepositoryId);

        IEnumerable<PipelineDefinitionDto> definitions;

        if (query.ProductId.HasValue)
        {
            definitions = await _repository.GetDefinitionsByProductIdAsync(query.ProductId.Value, cancellationToken);
        }
        else if (query.RepositoryId.HasValue)
        {
            definitions = await _repository.GetDefinitionsByRepositoryIdAsync(query.RepositoryId.Value, cancellationToken);
        }
        else
        {
            definitions = await _repository.GetAllDefinitionsAsync(cancellationToken);
        }

        var definitionsList = definitions.ToList();
        _logger.LogInformation("Retrieved {Count} pipeline definitions", definitionsList.Count);

        return definitionsList;
    }
}
