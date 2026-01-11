using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Shared.Exceptions;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for ValidateWorkItemQuery.
/// Validates a work item by ID directly from TFS (bypasses cache).
/// </summary>
public sealed class ValidateWorkItemQueryHandler : IQueryHandler<ValidateWorkItemQuery, ValidateWorkItemResponse>
{
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<ValidateWorkItemQueryHandler> _logger;

    public ValidateWorkItemQueryHandler(
        ITfsClient tfsClient,
        ILogger<ValidateWorkItemQueryHandler> logger)
    {
        _tfsClient = tfsClient;
        _logger = logger;
    }

    public async ValueTask<ValidateWorkItemResponse> Handle(
        ValidateWorkItemQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Validating work item {WorkItemId} directly from TFS", query.WorkItemId);

        try
        {
            // Query TFS directly (bypasses cache)
            var workItem = await _tfsClient.GetWorkItemByIdAsync(query.WorkItemId, cancellationToken);

            if (workItem == null)
            {
                _logger.LogDebug("Work item {WorkItemId} not found in TFS", query.WorkItemId);
                return new ValidateWorkItemResponse
                {
                    Exists = false,
                    Id = query.WorkItemId,
                    Title = null,
                    Type = null,
                    ErrorMessage = null
                };
            }

            _logger.LogInformation("Work item {WorkItemId} validated successfully: {Title}", query.WorkItemId, workItem.Title);
            return new ValidateWorkItemResponse
            {
                Exists = true,
                Id = workItem.TfsId,
                Title = workItem.Title,
                Type = workItem.Type,
                ErrorMessage = null
            };
        }
        catch (TfsAuthenticationException ex)
        {
            _logger.LogError(ex, "Authentication failed while validating work item {WorkItemId}", query.WorkItemId);
            return new ValidateWorkItemResponse
            {
                Exists = false,
                Id = query.WorkItemId,
                Title = null,
                Type = null,
                ErrorMessage = "Not authorized to access TFS"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Connection failed while validating work item {WorkItemId}", query.WorkItemId);
            return new ValidateWorkItemResponse
            {
                Exists = false,
                Id = query.WorkItemId,
                Title = null,
                Type = null,
                ErrorMessage = "Unable to connect to TFS server"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating work item {WorkItemId}", query.WorkItemId);
            return new ValidateWorkItemResponse
            {
                Exists = false,
                Id = query.WorkItemId,
                Title = null,
                Type = null,
                ErrorMessage = $"Error validating work item: {ex.Message}"
            };
        }
    }
}
