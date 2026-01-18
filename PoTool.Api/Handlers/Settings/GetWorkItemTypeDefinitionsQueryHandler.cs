using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for retrieving work item type definitions from TFS.
/// </summary>
public sealed class GetWorkItemTypeDefinitionsQueryHandler
    : IRequestHandler<GetWorkItemTypeDefinitionsQuery, IEnumerable<WorkItemTypeDefinitionDto>>
{
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<GetWorkItemTypeDefinitionsQueryHandler> _logger;

    public GetWorkItemTypeDefinitionsQueryHandler(
        ITfsClient tfsClient,
        ILogger<GetWorkItemTypeDefinitionsQueryHandler> logger)
    {
        _tfsClient = tfsClient;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemTypeDefinitionDto>> Handle(
        GetWorkItemTypeDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving work item type definitions from TFS");

        var definitions = await _tfsClient.GetWorkItemTypeDefinitionsAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully retrieved {Count} work item type definitions",
            definitions.Count());

        return definitions;
    }
}
