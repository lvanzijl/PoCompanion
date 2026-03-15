using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Rules;

namespace PoTool.Core.Domain.BacklogQuality.Services;

/// <summary>
/// Manual catalog of canonical backlog-quality rules and metadata.
/// </summary>
public sealed class RuleCatalog
{
    private readonly IReadOnlyList<IBacklogQualityRule> _rules;
    private readonly IReadOnlyDictionary<string, IBacklogQualityRule> _rulesById;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleCatalog"/> class.
    /// </summary>
    public RuleCatalog()
    {
        _rules = CreateRules();
        _rulesById = _rules.ToDictionary(rule => rule.Metadata.RuleId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all registered rules in canonical execution order.
    /// </summary>
    public IReadOnlyList<IBacklogQualityRule> Rules => _rules;

    /// <summary>
    /// Gets rules for a specific family while preserving canonical order.
    /// </summary>
    public IReadOnlyList<IBacklogQualityRule> GetByFamily(RuleFamily family)
    {
        return _rules.Where(rule => rule.Metadata.Family == family).ToArray();
    }

    /// <summary>
    /// Gets a rule by its stable identifier.
    /// </summary>
    public bool TryGetRule(string ruleId, out IBacklogQualityRule? rule)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            rule = null;
            return false;
        }

        return _rulesById.TryGetValue(ruleId, out rule);
    }

    private static IReadOnlyList<IBacklogQualityRule> CreateRules()
    {
        return
        [
            new DoneParentWithUnfinishedDescendantsRule(),
            new RemovedParentWithUnfinishedDescendantsRule(),
            new NewParentWithStartedDescendantsRule(),
            new EpicDescriptionRule(),
            new FeatureDescriptionRule(),
            new EpicMissingChildrenRule(),
            new PbiDescriptionRule(),
            new PbiMissingEffortRule(),
            new FeatureMissingChildrenRule()
        ];
    }
}
