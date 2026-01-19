using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for retrieving work item type definitions from TFS.
/// Filters to only return work item types defined in the WorkItemType class.
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

        var allDefinitions = await _tfsClient.GetWorkItemTypeDefinitionsAsync(cancellationToken);

        // Filter to only supported work item types from WorkItemType class
        var supportedTypes = WorkItemType.AllTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filteredDefinitions = allDefinitions
            .Where(def => supportedTypes.Contains(def.TypeName))
            .ToList();

        _logger.LogInformation(
            "Successfully retrieved {TotalCount} work item type definitions, filtered to {FilteredCount} supported types",
            allDefinitions.Count(), filteredDefinitions.Count);

        return filteredDefinitions;
    }
}
