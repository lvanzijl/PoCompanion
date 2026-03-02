using Mediator;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="GetValidationQueueQuery"/>.
/// Delegates to <see cref="GetAllWorkItemsWithValidationQuery"/> for data loading, then
/// groups items by rule ID for the requested category and returns them sorted by count descending.
/// Rule short titles are sourced from <see cref="ValidationRuleDescriptions"/> (single source of truth).
/// </summary>
public sealed class GetValidationQueueQueryHandler
    : IQueryHandler<GetValidationQueueQuery, ValidationQueueDto>
{
    private readonly IMediator _mediator;
    private readonly ILogger<GetValidationQueueQueryHandler> _logger;

    public GetValidationQueueQueryHandler(
        IMediator mediator,
        ILogger<GetValidationQueueQueryHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<ValidationQueueDto> Handle(
        GetValidationQueueQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetValidationQueueQuery for category {CategoryKey} with ProductIds: {ProductIds}",
            query.CategoryKey,
            query.ProductIds != null ? string.Join(", ", query.ProductIds) : "null (all products)");

        var (categoryLabel, ruleIdFilter) = ResolveCategoryMeta(query.CategoryKey);

        // Reuse existing validated work-item loading to avoid duplication
        var workItems = await _mediator.Send(
            new GetAllWorkItemsWithValidationQuery(query.ProductIds),
            cancellationToken);

        // Group affected work items by rule ID, restricted to the requested category
        var ruleItemSets = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var wi in workItems)
        {
            if (wi.ValidationIssues is null) continue;

            foreach (var issue in wi.ValidationIssues)
            {
                if (string.IsNullOrEmpty(issue.RuleId)) continue;
                if (!ruleIdFilter(issue.RuleId)) continue;

                if (!ruleItemSets.TryGetValue(issue.RuleId, out var set))
                {
                    set = new HashSet<int>();
                    ruleItemSets[issue.RuleId] = set;
                }
                set.Add(wi.TfsId);
            }
        }

        var allAffected = new HashSet<int>(ruleItemSets.Values.SelectMany(s => s));

        var ruleGroups = ruleItemSets
            .OrderByDescending(kvp => kvp.Value.Count)
            .Select(kvp => new ValidationQueueRuleGroupDto(
                kvp.Key,
                ValidationRuleDescriptions.GetTitle(kvp.Key),
                kvp.Value.Count))
            .ToList();

        return new ValidationQueueDto(query.CategoryKey, categoryLabel, allAffected.Count, ruleGroups);
    }

    /// <summary>
    /// Returns the human-readable category label and a predicate that decides
    /// which rule IDs belong to the requested category.
    /// </summary>
    private static (string Label, Func<string, bool> Filter) ResolveCategoryMeta(string categoryKey)
    {
        return categoryKey.ToUpperInvariant() switch
        {
            "SI"  => ("Structural Integrity",    ruleId => ruleId.StartsWith("SI-",  StringComparison.OrdinalIgnoreCase)),
            "RR"  => ("Refinement Readiness",    ruleId => ruleId.StartsWith("RR-",  StringComparison.OrdinalIgnoreCase)),
            "RC"  => ("Refinement Completeness", ruleId => ruleId.StartsWith("RC-",  StringComparison.OrdinalIgnoreCase)),
            "EFF" => ("Missing Effort",          ruleId => ruleId.Equals("RC-2",     StringComparison.OrdinalIgnoreCase)),
            _     => (categoryKey,               ruleId => ruleId.StartsWith(categoryKey + "-", StringComparison.OrdinalIgnoreCase))
        };
    }
}

