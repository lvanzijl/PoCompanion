using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetBacklogHealthQuery.
/// Calculates backlog health metrics for a specific iteration.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetBacklogHealthQueryHandler
    : IQueryHandler<GetBacklogHealthQuery, BacklogHealthDto?>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly IHierarchicalWorkItemValidator _validator;
    private readonly ILogger<GetBacklogHealthQueryHandler> _logger;

    public GetBacklogHealthQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        IProductRepository productRepository,
        IMediator mediator,
        IHierarchicalWorkItemValidator validator,
        ILogger<GetBacklogHealthQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _productRepository = productRepository;
        _mediator = mediator;
        _validator = validator;
        _logger = logger;
    }

    public async ValueTask<BacklogHealthDto?> Handle(
        GetBacklogHealthQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetBacklogHealthQuery for iteration: {IterationPath}", query.IterationPath);

        // Load work items using product-scoped approach
        IEnumerable<WorkItemDto> allWorkItems;
        var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
        var productsList = allProducts.ToList();

        if (productsList.Count > 0)
        {
            var rootIds = productsList
                .SelectMany(p => p.BacklogRootWorkItemIds)
                .ToArray();

            if (rootIds.Length > 0)
            {
                var workItemsQuery = new GetWorkItemsByRootIdsQuery(rootIds);
                allWorkItems = await _mediator.Send(workItemsQuery, cancellationToken);
            }
            else
            {
                allWorkItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
            }
        }
        else
        {
            allWorkItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
        }

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

        // Count refinement blockers and refinement needed from hierarchical validation
        var refinementBlockers = validationResults.Sum(r => r.RefinementBlockers.Count);
        var refinementNeeded = validationResults.Sum(r => r.IncompleteRefinementIssues.Count);

        // Count structural integrity issues (for legacy compatibility)
        var structuralIntegrityIssues = validationResults.Sum(r => r.BacklogHealthProblems.Count);
        var parentProgressIssues = structuralIntegrityIssues; // Use structural integrity count

        // Count blocked items (from JSON payload if possible, otherwise estimate)
        var blockedItems = CountBlockedItems(iterationWorkItems);

        // Count items still in progress at iteration end (if iteration has ended)
        var inProgressAtIterationEnd = CountInProgressAtEnd(iterationWorkItems, endDate);

        // Group validation issues by consequence
        var validationIssuesSummary = GroupValidationIssuesByConsequence(validationResults);

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
            ValidationIssues: validationIssuesSummary,
            RefinementBlockers: refinementBlockers,
            RefinementNeeded: refinementNeeded
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

    private static IReadOnlyList<ValidationIssueSummary> GroupValidationIssuesByConsequence(
        IReadOnlyList<HierarchicalValidationResult> validationResults)
    {
        // Group by validation consequence
        var issuesByConsequence = new Dictionary<string, HashSet<int>>();

        foreach (var treeResult in validationResults)
        {
            // Group by consequence type
            void AddIssues(string consequenceType, IEnumerable<ValidationRuleResult> violations)
            {
                foreach (var violation in violations)
                {
                    if (!issuesByConsequence.ContainsKey(consequenceType))
                    {
                        issuesByConsequence[consequenceType] = new HashSet<int>();
                    }
                    issuesByConsequence[consequenceType].Add(violation.WorkItemId);
                }
            }

            AddIssues("Structural Integrity", treeResult.BacklogHealthProblems);
            AddIssues("Refinement Blocker", treeResult.RefinementBlockers);
            AddIssues("Refinement Needed", treeResult.IncompleteRefinementIssues);
        }

        return issuesByConsequence.Select(kvp => new ValidationIssueSummary(
            ValidationType: kvp.Key,
            Count: kvp.Value.Count,
            AffectedWorkItemIds: kvp.Value.ToList()
        )).ToList();
    }
}
