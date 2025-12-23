using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Commands;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for BulkAssignEffortCommand.
/// Assigns effort estimates to multiple work items in batch via TFS updates.
/// </summary>
public sealed class BulkAssignEffortCommandHandler 
    : ICommandHandler<BulkAssignEffortCommand, BulkEffortAssignmentResultDto>
{
    private readonly ITfsClient _tfsClient;
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<BulkAssignEffortCommandHandler> _logger;

    public BulkAssignEffortCommandHandler(
        ITfsClient tfsClient,
        IWorkItemRepository repository,
        ILogger<BulkAssignEffortCommandHandler> logger)
    {
        _tfsClient = tfsClient;
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<BulkEffortAssignmentResultDto> Handle(
        BulkAssignEffortCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling BulkAssignEffortCommand with {Count} assignments", command.Assignments.Count);

        var results = new List<BulkEffortAssignmentItemResult>();
        var successCount = 0;
        var failureCount = 0;

        foreach (var assignment in command.Assignments)
        {
            try
            {
                _logger.LogDebug("Attempting to assign effort {Effort} to work item {WorkItemId}",
                    assignment.EffortValue, assignment.WorkItemId);

                // Validate the work item exists locally
                var workItem = await _repository.GetByTfsIdAsync(assignment.WorkItemId, cancellationToken);
                if (workItem == null)
                {
                    var errorMsg = $"Work item {assignment.WorkItemId} not found in local cache";
                    _logger.LogWarning(errorMsg);
                    results.Add(new BulkEffortAssignmentItemResult(assignment.WorkItemId, false, errorMsg));
                    failureCount++;
                    continue;
                }

                // Validate effort value
                if (assignment.EffortValue < 0)
                {
                    var errorMsg = $"Invalid effort value {assignment.EffortValue} (must be >= 0)";
                    _logger.LogWarning(errorMsg);
                    results.Add(new BulkEffortAssignmentItemResult(assignment.WorkItemId, false, errorMsg));
                    failureCount++;
                    continue;
                }

                // Apply effort update to TFS
                var success = await _tfsClient.UpdateWorkItemEffortAsync(
                    assignment.WorkItemId,
                    assignment.EffortValue,
                    cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Successfully updated work item {WorkItemId} effort to {Effort}", 
                        assignment.WorkItemId, assignment.EffortValue);
                    results.Add(new BulkEffortAssignmentItemResult(assignment.WorkItemId, true));
                    successCount++;
                }
                else
                {
                    var errorMsg = $"Failed to update work item {assignment.WorkItemId} - TFS update returned false";
                    _logger.LogWarning(errorMsg);
                    results.Add(new BulkEffortAssignmentItemResult(assignment.WorkItemId, false, errorMsg));
                    failureCount++;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Exception updating work item {assignment.WorkItemId}: {ex.Message}";
                _logger.LogError(ex, "Error assigning effort for work item {WorkItemId}", assignment.WorkItemId);
                results.Add(new BulkEffortAssignmentItemResult(assignment.WorkItemId, false, errorMsg));
                failureCount++;
            }
        }

        var result = new BulkEffortAssignmentResultDto(
            TotalRequested: command.Assignments.Count,
            SuccessfulUpdates: successCount,
            FailedUpdates: failureCount,
            Results: results
        );

        _logger.LogInformation("Bulk effort assignment complete: {Success} successful, {Failed} failed out of {Total}",
            successCount, failureCount, command.Assignments.Count);

        return result;
    }
}
