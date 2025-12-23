using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetValidationViolationHistoryQuery.
/// Retrieves historical validation violations for tracking patterns over time.
/// </summary>
public sealed class GetValidationViolationHistoryQueryHandler 
    : IQueryHandler<GetValidationViolationHistoryQuery, IEnumerable<ValidationViolationHistoryDto>>
{
    private readonly IWorkItemRepository _repository;
    private readonly IWorkItemValidator _validator;
    private readonly ILogger<GetValidationViolationHistoryQueryHandler> _logger;

    public GetValidationViolationHistoryQueryHandler(
        IWorkItemRepository repository,
        IWorkItemValidator validator,
        ILogger<GetValidationViolationHistoryQueryHandler> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<ValidationViolationHistoryDto>> Handle(
        GetValidationViolationHistoryQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetValidationViolationHistoryQuery with AreaPathFilter={AreaPath}, StartDate={StartDate}, EndDate={EndDate}",
            query.AreaPathFilter, query.StartDate, query.EndDate);

        // Get all work items
        var workItems = await _repository.GetAllAsync(cancellationToken);
        var workItemsList = workItems.ToList();

        // Apply area path filter if provided
        if (!string.IsNullOrEmpty(query.AreaPathFilter))
        {
            workItemsList = workItemsList
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPathFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Run validation
        var validationResults = _validator.ValidateWorkItems(workItemsList);

        // Convert validation results to history records
        var historyRecords = new List<ValidationViolationHistoryDto>();
        
        foreach (var (workItemId, issues) in validationResults)
        {
            var workItem = workItemsList.FirstOrDefault(wi => wi.TfsId == workItemId);
            if (workItem == null) continue;

            // Apply date filter based on when work item was retrieved
            if (query.StartDate.HasValue && workItem.RetrievedAt < query.StartDate.Value)
                continue;
            if (query.EndDate.HasValue && workItem.RetrievedAt > query.EndDate.Value)
                continue;

            foreach (var issue in issues)
            {
                // Apply violation type filter if provided
                if (!string.IsNullOrEmpty(query.ViolationType) &&
                    !issue.Message.Contains(query.ViolationType, StringComparison.OrdinalIgnoreCase))
                    continue;

                historyRecords.Add(new ValidationViolationHistoryDto(
                    WorkItemId: workItem.TfsId,
                    WorkItemType: workItem.Type,
                    WorkItemTitle: workItem.Title,
                    ValidationType: "ParentProgress",
                    Severity: issue.Severity,
                    ViolationMessage: issue.Message,
                    AreaPath: workItem.AreaPath,
                    IterationPath: workItem.IterationPath,
                    DetectedAt: workItem.RetrievedAt
                ));
            }
        }

        _logger.LogInformation("Retrieved {Count} validation violation history records", historyRecords.Count);
        return historyRecords.OrderByDescending(r => r.DetectedAt);
    }
}
