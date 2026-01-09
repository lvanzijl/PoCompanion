using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Validators;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetMultiIterationBacklogHealthQuery.
/// Aggregates backlog health across multiple iterations and provides trend analysis.
/// </summary>
public sealed class GetMultiIterationBacklogHealthQueryHandler 
    : IQueryHandler<GetMultiIterationBacklogHealthQuery, MultiIterationBacklogHealthDto>
{
    private readonly IWorkItemRepository _repository;
    private readonly IWorkItemValidator _validator;
    private readonly ILogger<GetMultiIterationBacklogHealthQueryHandler> _logger;

    public GetMultiIterationBacklogHealthQueryHandler(
        IWorkItemRepository repository,
        IWorkItemValidator validator,
        ILogger<GetMultiIterationBacklogHealthQueryHandler> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async ValueTask<MultiIterationBacklogHealthDto> Handle(
        GetMultiIterationBacklogHealthQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetMultiIterationBacklogHealthQuery with AreaPath: {AreaPath}, MaxIterations: {MaxIterations}", 
            query.AreaPath ?? "All", 
            query.MaxIterations);

        var allWorkItems = await _repository.GetAllAsync(cancellationToken);
        
        // Filter by area path if specified
        if (!string.IsNullOrWhiteSpace(query.AreaPath))
        {
            allWorkItems = allWorkItems
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Get distinct iteration paths, sorted
        var iterationPaths = allWorkItems
            .Where(wi => !string.IsNullOrWhiteSpace(wi.IterationPath))
            .Select(wi => wi.IterationPath)
            .Distinct()
            .OrderByDescending(path => path) // Most recent first
            .Take(query.MaxIterations)
            .ToList();

        _logger.LogDebug("Found {Count} iteration paths", iterationPaths.Count);

        // Calculate health for each iteration
        var iterationHealthList = new List<BacklogHealthDto>();
        foreach (var iterationPath in iterationPaths)
        {
            var health = await CalculateIterationHealth(iterationPath, allWorkItems, cancellationToken);
            if (health != null)
            {
                iterationHealthList.Add(health);
            }
        }

        // Calculate trend
        var trend = CalculateTrend(iterationHealthList);

        // Calculate totals
        var totalWorkItems = iterationHealthList.Sum(h => h.TotalWorkItems);
        var totalIssues = iterationHealthList.Sum(h => 
            h.WorkItemsWithoutEffort + 
            h.ParentProgressIssues + 
            h.BlockedItems);

        return new MultiIterationBacklogHealthDto(
            IterationHealth: iterationHealthList,
            Trend: trend,
            TotalWorkItems: totalWorkItems,
            TotalIssues: totalIssues,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private async ValueTask<BacklogHealthDto?> CalculateIterationHealth(
        string iterationPath,
        IEnumerable<WorkItemDto> allWorkItems,
        CancellationToken cancellationToken)
    {
        var iterationWorkItems = allWorkItems
            .Where(wi => wi.IterationPath.Equals(iterationPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!iterationWorkItems.Any())
        {
            return null;
        }

        // Run validators
        var validationResults = _validator.ValidateWorkItems(iterationWorkItems);

        // Extract sprint metadata
        var sprintName = ExtractSprintName(iterationPath);
        var (startDate, endDate) = ExtractSprintDates(iterationWorkItems);

        // Calculate metrics
        var totalWorkItems = iterationWorkItems.Count;
        var workItemsWithoutEffort = iterationWorkItems.Count(wi => !wi.Effort.HasValue);
        var workItemsInProgressWithoutEffort = iterationWorkItems
            .Count(wi => wi.State.Equals("In Progress", StringComparison.OrdinalIgnoreCase) && !wi.Effort.HasValue);
        
        var allIssues = validationResults.Values.SelectMany(issues => issues).ToList();
        var parentProgressIssues = allIssues.Count(issue => 
            issue.Message.Contains("Parent", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("Ancestor", StringComparison.OrdinalIgnoreCase));
        var blockedItems = CountBlockedItems(iterationWorkItems);
        var inProgressAtIterationEnd = CountInProgressAtEnd(iterationWorkItems, endDate);
        var validationIssuesSummary = GroupValidationIssues(validationResults);

        return new BacklogHealthDto(
            IterationPath: iterationPath,
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

    private static BacklogHealthTrend CalculateTrend(List<BacklogHealthDto> iterations)
    {
        if (iterations.Count < 2)
        {
            return new BacklogHealthTrend(
                EffortTrend: TrendDirection.Unknown,
                ValidationTrend: TrendDirection.Unknown,
                BlockerTrend: TrendDirection.Unknown,
                Summary: "Insufficient data for trend analysis"
            );
        }

        // Compare most recent sprint to previous sprint
        var recent = iterations[0];
        var previous = iterations[1];

        // Calculate effort trend
        var effortTrend = CalculateTrendDirection(
            recent.WorkItemsWithoutEffort,
            previous.WorkItemsWithoutEffort,
            recent.TotalWorkItems,
            previous.TotalWorkItems
        );

        // Calculate validation trend
        var recentValidationIssues = recent.ValidationIssues.Sum(v => v.Count);
        var previousValidationIssues = previous.ValidationIssues.Sum(v => v.Count);
        var validationTrend = CalculateTrendDirection(
            recentValidationIssues,
            previousValidationIssues,
            recent.TotalWorkItems,
            previous.TotalWorkItems
        );

        // Calculate blocker trend
        var blockerTrend = CalculateTrendDirection(
            recent.BlockedItems,
            previous.BlockedItems,
            recent.TotalWorkItems,
            previous.TotalWorkItems
        );

        // Generate summary
        var summary = GenerateTrendSummary(effortTrend, validationTrend, blockerTrend);

        return new BacklogHealthTrend(
            EffortTrend: effortTrend,
            ValidationTrend: validationTrend,
            BlockerTrend: blockerTrend,
            Summary: summary
        );
    }

    private static TrendDirection CalculateTrendDirection(
        int recentCount,
        int previousCount,
        int recentTotal,
        int previousTotal)
    {
        if (recentTotal == 0 || previousTotal == 0)
        {
            return TrendDirection.Unknown;
        }

        var recentPercentage = (double)recentCount / recentTotal;
        var previousPercentage = (double)previousCount / previousTotal;
        var change = recentPercentage - previousPercentage;

        return change switch
        {
            < -0.05 => TrendDirection.Improving, // 5% improvement
            > 0.05 => TrendDirection.Degrading,  // 5% degradation
            _ => TrendDirection.Stable
        };
    }

    private static string GenerateTrendSummary(
        TrendDirection effortTrend,
        TrendDirection validationTrend,
        TrendDirection blockerTrend)
    {
        var trends = new[] { effortTrend, validationTrend, blockerTrend };
        var improving = trends.Count(t => t == TrendDirection.Improving);
        var degrading = trends.Count(t => t == TrendDirection.Degrading);

        if (improving > degrading)
        {
            return "Overall backlog health is improving";
        }
        else if (degrading > improving)
        {
            return "Overall backlog health is degrading - attention needed";
        }
        else
        {
            return "Backlog health is stable";
        }
    }

    private static string ExtractSprintName(string iterationPath)
    {
        var parts = iterationPath.Split('\\', '/');
        return parts.Length > 0 ? parts[^1] : iterationPath;
    }

    private static (DateTimeOffset?, DateTimeOffset?) ExtractSprintDates(List<WorkItemDto> workItems)
    {
        return (null, null);
    }

    private static int CountBlockedItems(List<WorkItemDto> workItems)
    {
        return workItems.Count(wi => 
            wi.State.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ||
            wi.State.Contains("On Hold", StringComparison.OrdinalIgnoreCase));
    }

    private static int CountInProgressAtEnd(List<WorkItemDto> workItems, DateTimeOffset? endDate)
    {
        if (!endDate.HasValue || endDate.Value > DateTimeOffset.UtcNow)
        {
            return 0;
        }

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
