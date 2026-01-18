using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for retrieving work item state classifications.
/// </summary>
public sealed class GetStateClassificationsQueryHandler
    : IRequestHandler<GetStateClassificationsQuery, GetStateClassificationsResponse>
{
    private readonly IWorkItemStateClassificationService _classificationService;
    private readonly ILogger<GetStateClassificationsQueryHandler> _logger;

    public GetStateClassificationsQueryHandler(
        IWorkItemStateClassificationService classificationService,
        ILogger<GetStateClassificationsQueryHandler> logger)
    {
        _classificationService = classificationService;
        _logger = logger;
    }

    public async ValueTask<GetStateClassificationsResponse> Handle(
        GetStateClassificationsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving work item state classifications");

        var response = await _classificationService.GetClassificationsAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} state classifications (IsDefault: {IsDefault})",
            response.Classifications.Count, response.IsDefault);

        return response;
    }
}
