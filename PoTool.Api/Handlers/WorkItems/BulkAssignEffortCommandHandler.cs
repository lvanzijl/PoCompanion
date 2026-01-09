using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Commands;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for BulkAssignEffortCommand.
/// Assigns effort estimates to multiple work items in batch via TFS updates.
/// Uses bulk update method to prevent N+1 query pattern.
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

        var validUpdates = new List<WorkItemEffortUpdate>();
        var validationResults = new List<BulkEffortAssignmentItemResult>();
        var validationFailureCount = 0;

        // Step 1: Validate all assignments first
        foreach (var assignment in command.Assignments)
        {
            // Validate the work item exists locally
            var workItem = await _repository.GetByTfsIdAsync(assignment.WorkItemId, cancellationToken);
            if (workItem == null)
            {
                var errorMsg = $"Work item {assignment.WorkItemId} not found in local cache";
                _logger.LogWarning(errorMsg);
                validationResults.Add(new BulkEffortAssignmentItemResult(assignment.WorkItemId, false, errorMsg));
                validationFailureCount++;
                continue;
            }

            // Validate effort value
            if (assignment.EffortValue < 0)
            {
                var errorMsg = $"Invalid effort value {assignment.EffortValue} (must be >= 0)";
                _logger.LogWarning(errorMsg);
                validationResults.Add(new BulkEffortAssignmentItemResult(assignment.WorkItemId, false, errorMsg));
                validationFailureCount++;
                continue;
            }

            // Add to valid updates list for bulk processing
            validUpdates.Add(new WorkItemEffortUpdate(assignment.WorkItemId, assignment.EffortValue));
        }

        // Step 2: Use bulk method to update all valid work items in a single call.
        // This prevents the N+1 pattern where we would call UpdateWorkItemEffortAsync
        // for each work item individually.
        var bulkResult = await _tfsClient.UpdateWorkItemsEffortAsync(validUpdates, cancellationToken);

        // Log performance instrumentation showing call reduction
        _logger.LogInformation(
            "Bulk effort update completed with {TfsCallCount} TFS call(s) for {Count} work items",
            bulkResult.TfsCallCount,
            validUpdates.Count);

        // Combine validation failures with TFS update results
        var allResults = new List<BulkEffortAssignmentItemResult>(validationResults);
        foreach (var updateResult in bulkResult.Results)
        {
            allResults.Add(new BulkEffortAssignmentItemResult(
                updateResult.WorkItemId,
                updateResult.Success,
                updateResult.ErrorMessage
            ));
        }

        var totalSuccess = bulkResult.SuccessfulUpdates;
        var totalFailure = validationFailureCount + bulkResult.FailedUpdates;

        var result = new BulkEffortAssignmentResultDto(
            TotalRequested: command.Assignments.Count,
            SuccessfulUpdates: totalSuccess,
            FailedUpdates: totalFailure,
            Results: allResults
        );

        _logger.LogInformation("Bulk effort assignment complete: {Success} successful, {Failed} failed out of {Total}",
            totalSuccess, totalFailure, command.Assignments.Count);

        return result;
    }
}
