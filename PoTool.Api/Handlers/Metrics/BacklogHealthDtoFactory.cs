using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Rules;
using PoTool.Shared.Metrics;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.Metrics;

internal static class BacklogHealthDtoFactory
{
    public static BacklogHealthDto Create(
        string iterationPath,
        IReadOnlyList<WorkItemDto> workItems,
        BacklogQualityAnalysisResult analysis,
        DateTimeOffset? iterationStart,
        DateTimeOffset? iterationEnd)
    {
        ArgumentNullException.ThrowIfNull(iterationPath);
        ArgumentNullException.ThrowIfNull(workItems);
        ArgumentNullException.ThrowIfNull(analysis);

        return new BacklogHealthDto(
            IterationPath: iterationPath,
            SprintName: ExtractSprintName(iterationPath),
            TotalWorkItems: workItems.Count,
            WorkItemsWithoutEffort: workItems.Count(wi => !wi.Effort.HasValue),
            WorkItemsInProgressWithoutEffort: CountInProgressWithoutEffort(workItems),
            ParentProgressIssues: analysis.IntegrityFindings.Count,
            BlockedItems: CountBlockedItems(workItems),
            InProgressAtIterationEnd: CountInProgressAtEnd(workItems, iterationEnd),
            IterationStart: iterationStart,
            IterationEnd: iterationEnd,
            ValidationIssues: GroupValidationIssues(analysis),
            RefinementBlockers: CountRefinementBlockers(analysis),
            RefinementNeeded: CountRefinementNeeded(analysis));
    }

    public static string ExtractSprintName(string iterationPath)
    {
        var parts = iterationPath.Split('\\', '/');
        return parts.Length > 0 ? parts[^1] : iterationPath;
    }

    private static int CountInProgressWithoutEffort(IReadOnlyList<WorkItemDto> workItems)
    {
        return workItems.Count(wi =>
            wi.State.Equals("In Progress", StringComparison.OrdinalIgnoreCase) &&
            !wi.Effort.HasValue);
    }

    private static int CountBlockedItems(IReadOnlyList<WorkItemDto> workItems)
    {
        return workItems.Count(wi =>
            wi.State.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ||
            wi.State.Contains("On Hold", StringComparison.OrdinalIgnoreCase));
    }

    private static int CountInProgressAtEnd(IReadOnlyList<WorkItemDto> workItems, DateTimeOffset? endDate)
    {
        if (!endDate.HasValue || endDate.Value > DateTimeOffset.UtcNow)
        {
            return 0;
        }

        return workItems.Count(wi =>
            wi.State.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
            wi.State.Equals("Active", StringComparison.OrdinalIgnoreCase));
    }

    private static int CountRefinementBlockers(BacklogQualityAnalysisResult analysis)
    {
        return analysis.Findings.Count(finding => finding.Rule.Family == RuleFamily.RefinementReadiness);
    }

    private static int CountRefinementNeeded(BacklogQualityAnalysisResult analysis)
    {
        return analysis.Findings.Count(finding =>
            finding.Rule.Family == RuleFamily.ImplementationReadiness &&
            !string.Equals(finding.Rule.SemanticTag, "MissingEffort", StringComparison.Ordinal));
    }

    private static IReadOnlyList<ValidationIssueSummary> GroupValidationIssues(BacklogQualityAnalysisResult analysis)
    {
        var summaries = new List<ValidationIssueSummary>();

        AddSummary("Structural Integrity", analysis.IntegrityFindings.Select(finding => finding.WorkItemId));
        AddSummary(
            "Refinement Blocker",
            analysis.Findings
                .Where(finding => finding.Rule.Family == RuleFamily.RefinementReadiness)
                .Select(finding => finding.WorkItemId));
        AddSummary(
            "Refinement Needed",
            analysis.Findings
                .Where(finding => finding.Rule.Family == RuleFamily.ImplementationReadiness)
                .Where(finding => !string.Equals(finding.Rule.SemanticTag, "MissingEffort", StringComparison.Ordinal))
                .Select(finding => finding.WorkItemId));

        return summaries;

        void AddSummary(string validationType, IEnumerable<int> workItemIds)
        {
            var uniqueIds = workItemIds.Distinct().ToList();
            if (uniqueIds.Count == 0)
            {
                return;
            }

            summaries.Add(new ValidationIssueSummary(
                ValidationType: validationType,
                Count: uniqueIds.Count,
                AffectedWorkItemIds: uniqueIds));
        }
    }
}
