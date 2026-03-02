using Mediator;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="GetValidationTriageSummaryQuery"/>.
/// Delegates to <see cref="GetAllWorkItemsWithValidationQuery"/> for data loading, then
/// groups the results by validation category and rule ID for the Triage page.
/// </summary>
public sealed class GetValidationTriageSummaryQueryHandler
    : IQueryHandler<GetValidationTriageSummaryQuery, ValidationTriageSummaryDto>
{
    private const int TopRuleGroupCount = 3;
    private const string EffortRuleIdPrefix = "RC-2";

    private readonly IMediator _mediator;
    private readonly ILogger<GetValidationTriageSummaryQueryHandler> _logger;

    public GetValidationTriageSummaryQueryHandler(
        IMediator mediator,
        ILogger<GetValidationTriageSummaryQueryHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<ValidationTriageSummaryDto> Handle(
        GetValidationTriageSummaryQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetValidationTriageSummaryQuery with ProductIds: {ProductIds}",
            query.ProductIds != null ? string.Join(", ", query.ProductIds) : "null (all products)");

        // Reuse existing validated work-item loading to avoid duplication
        var workItems = await _mediator.Send(
            new GetAllWorkItemsWithValidationQuery(query.ProductIds),
            cancellationToken);

        // Build per-category item sets keyed by rule ID
        var siItems = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var rrItems = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var rcItems = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var effItems = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var wi in workItems)
        {
            if (wi.ValidationIssues is null) continue;

            foreach (var issue in wi.ValidationIssues)
            {
                if (string.IsNullOrEmpty(issue.RuleId)) continue;

                if (issue.RuleId.StartsWith("SI-", StringComparison.OrdinalIgnoreCase))
                {
                    AddItem(siItems, issue.RuleId, wi.TfsId);
                }
                else if (issue.RuleId.StartsWith("RR-", StringComparison.OrdinalIgnoreCase))
                {
                    AddItem(rrItems, issue.RuleId, wi.TfsId);
                }
                else if (issue.RuleId.StartsWith("RC-", StringComparison.OrdinalIgnoreCase))
                {
                    // EFF tile = RC-2 (missing effort) is a separate focus area, not in the RC tile
                    if (issue.RuleId.Equals(EffortRuleIdPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        AddItem(effItems, issue.RuleId, wi.TfsId);
                    }
                    else
                    {
                        AddItem(rcItems, issue.RuleId, wi.TfsId);
                    }
                }
            }
        }

        return new ValidationTriageSummaryDto(
            BuildCategory("SI", "Structural Integrity", siItems),
            BuildCategory("RR", "Refinement Readiness", rrItems),
            BuildCategory("RC", "Refinement Completeness", rcItems),
            BuildCategory("EFF", "Missing Effort", effItems)
        );
    }

    private static void AddItem(Dictionary<string, HashSet<int>> dict, string ruleId, int tfsId)
    {
        if (!dict.TryGetValue(ruleId, out var set))
        {
            set = new HashSet<int>();
            dict[ruleId] = set;
        }
        set.Add(tfsId);
    }

    private static ValidationCategoryTriageDto BuildCategory(
        string key,
        string label,
        Dictionary<string, HashSet<int>> ruleItemSets)
    {
        var allAffectedItems = new HashSet<int>(ruleItemSets.Values.SelectMany(s => s));
        var topGroups = ruleItemSets
            .OrderByDescending(kvp => kvp.Value.Count)
            .Take(TopRuleGroupCount)
            .Select(kvp => new ValidationRuleGroupDto(kvp.Key, kvp.Value.Count))
            .ToList();

        return new ValidationCategoryTriageDto(key, label, allAffectedItems.Count, topGroups);
    }
}
