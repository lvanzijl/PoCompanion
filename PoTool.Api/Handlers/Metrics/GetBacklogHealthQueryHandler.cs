using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Validators;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetBacklogHealthQuery.
/// Calculates backlog health metrics for a specific iteration.
/// Uses Live provider to fetch work items directly from TFS.
/// </summary>
public sealed class GetBacklogHealthQueryHandler
    : IQueryHandler<GetBacklogHealthQuery, BacklogHealthDto?>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IWorkItemValidator _validator;
    private readonly ILogger<GetBacklogHealthQueryHandler> _logger;

    public GetBacklogHealthQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        IWorkItemValidator validator,
        ILogger<GetBacklogHealthQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _validator = validator;
        _logger = logger;
    }

    public async ValueTask<BacklogHealthDto?> Handle(
        GetBacklogHealthQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetBacklogHealthQuery for iteration: {IterationPath}", query.IterationPath);

        var allWorkItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
        var iterationWorkItems = allWorkItems
            .Where(wi => wi.IterationPath.Equals(query.IterationPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!iterationWorkItems.Any())
        {
            _logger.LogDebug("No work items found for iteration: {IterationPath}", query.IterationPath);
            return null;
        }

        // Run validators
        var validationResults = _validator.ValidateWorkItems(iterationWorkItems);

        // Extract sprint metadata
        var sprintName = ExtractSprintName(query.IterationPath);
        var (startDate, endDate) = ExtractSprintDates(iterationWorkItems);

        // Calculate metrics
        var totalWorkItems = iterationWorkItems.Count;
        var workItemsWithoutEffort = iterationWorkItems.Count(wi => !wi.Effort.HasValue);
        var workItemsInProgressWithoutEffort = iterationWorkItems
            .Count(wi => wi.State.Equals("In Progress", StringComparison.OrdinalIgnoreCase) && !wi.Effort.HasValue);

        // Count validation issues by type
        var allIssues = validationResults.Values.SelectMany(issues => issues).ToList();
        var parentProgressIssues = allIssues.Count(issue =>
            issue.Message.Contains("Parent", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("Ancestor", StringComparison.OrdinalIgnoreCase));

        // Count blocked items (from JSON payload if possible, otherwise estimate)
        var blockedItems = CountBlockedItems(iterationWorkItems);

        // Count items still in progress at iteration end (if iteration has ended)
        var inProgressAtIterationEnd = CountInProgressAtEnd(iterationWorkItems, endDate);

        // Group validation issues by type
        var validationIssuesSummary = GroupValidationIssues(validationResults);

        return new BacklogHealthDto(
            IterationPath: query.IterationPath,
            SprintName: sprintName,
            TotalWorkItems: totalWorkItems,
            WorkItemsWithoutEffort: workItemsWithoutEffort,
            WorkItemsInProgressWithoutEffort: workItemsInProgressWithoutEffort,
            ParentProgressIssues: parentProgressIssues,
            BlockedItems: blockedItems,
            InProgressAtIterationEnd: inProgressAtIterationEnd,
            IterationStart: startDate,
            IterationEnd: endDate,
            ValidationIssues: validationIssuesSummary
        );
    }

    private static string ExtractSprintName(string iterationPath)
    {
        // Extract last part of iteration path (e.g., "Project\2025\Sprint 1" -> "Sprint 1")
        var parts = iterationPath.Split('\\', '/');
        return parts.Length > 0 ? parts[^1] : iterationPath;
    }

    private static (DateTimeOffset?, DateTimeOffset?) ExtractSprintDates(List<WorkItemDto> workItems)
    {
        // For now, return null - in future, could parse from JsonPayload
        // or maintain separate sprint metadata
        return (null, null);
    }

    private static int CountBlockedItems(List<WorkItemDto> workItems)
    {
        // Count items with "Blocked" state or similar
        return workItems.Count(wi =>
            wi.State.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ||
            wi.State.Contains("On Hold", StringComparison.OrdinalIgnoreCase));
    }

    private static int CountInProgressAtEnd(List<WorkItemDto> workItems, DateTimeOffset? endDate)
    {
        // If iteration hasn't ended yet, return 0
        if (!endDate.HasValue || endDate.Value > DateTimeOffset.UtcNow)
        {
            return 0;
        }

        // Count items still in "In Progress" state
        return workItems.Count(wi =>
            wi.State.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
            wi.State.Equals("Active", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<ValidationIssueSummary> GroupValidationIssues(
        Dictionary<int, List<ValidationIssue>> validationResults)
    {
        // Group by severity since we don't have a status property
        var issuesBySeverity = new Dictionary<string, HashSet<int>>();

        foreach (var (workItemId, issues) in validationResults)
        {
            foreach (var issue in issues)
            {
                var severity = issue.Severity;
                if (!issuesBySeverity.ContainsKey(severity))
                {
                    issuesBySeverity[severity] = new HashSet<int>();
                }
                issuesBySeverity[severity].Add(workItemId);
            }
        }

        return issuesBySeverity.Select(kvp => new ValidationIssueSummary(
            ValidationType: kvp.Key,
            Count: kvp.Value.Count,
            AffectedWorkItemIds: kvp.Value.ToList()
        )).ToList();
    }
}
