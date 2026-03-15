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
            CreateRule(
                "SI-1",
                RuleFamily.StructuralIntegrity,
                "DoneParentWithUnfinishedDescendants",
                "Parent in Done with any descendant not in Done or Removed is invalid.",
                RuleResponsibleParty.Process,
                RuleFindingClass.StructuralWarning,
                BacklogWorkItemTypes.StructuralIntegrityTypes),
            CreateRule(
                "SI-2",
                RuleFamily.StructuralIntegrity,
                "RemovedParentWithUnfinishedDescendants",
                "Parent in Removed with any descendant not in Done or Removed is invalid.",
                RuleResponsibleParty.Process,
                RuleFindingClass.StructuralWarning,
                BacklogWorkItemTypes.StructuralIntegrityTypes),
            CreateRule(
                "SI-3",
                RuleFamily.StructuralIntegrity,
                "NewParentWithStartedDescendants",
                "Parent in New with any descendant in progress or done is invalid.",
                RuleResponsibleParty.Process,
                RuleFindingClass.StructuralWarning,
                BacklogWorkItemTypes.StructuralIntegrityTypes),
            CreateRule(
                "RR-1",
                RuleFamily.RefinementReadiness,
                "MissingDescription",
                "Epic description must be present and at least 10 characters.",
                RuleResponsibleParty.ProductOwner,
                RuleFindingClass.RefinementBlocker,
                [BacklogWorkItemTypes.Epic]),
            CreateRule(
                "RR-2",
                RuleFamily.RefinementReadiness,
                "MissingDescription",
                "Feature description must be present and at least 10 characters.",
                RuleResponsibleParty.ProductOwner,
                RuleFindingClass.RefinementBlocker,
                [BacklogWorkItemTypes.Feature]),
            CreateRule(
                "RR-3",
                RuleFamily.RefinementReadiness,
                "MissingChildren",
                "Epic must have at least one active Feature child.",
                RuleResponsibleParty.ProductOwner,
                RuleFindingClass.RefinementBlocker,
                [BacklogWorkItemTypes.Epic]),
            CreateRule(
                "RC-1",
                RuleFamily.ImplementationReadiness,
                "MissingDescription",
                "PBI description must be present.",
                RuleResponsibleParty.Team,
                RuleFindingClass.ImplementationBlocker,
                BacklogWorkItemTypes.PbiTypes),
            CreateRule(
                "RC-2",
                RuleFamily.ImplementationReadiness,
                "MissingEffort",
                "PBI effort must be present and greater than zero.",
                RuleResponsibleParty.Team,
                RuleFindingClass.ImplementationBlocker,
                BacklogWorkItemTypes.PbiTypes),
            CreateRule(
                "RC-3",
                RuleFamily.ImplementationReadiness,
                "MissingChildren",
                "Feature must have at least one active PBI child.",
                RuleResponsibleParty.Team,
                RuleFindingClass.ImplementationBlocker,
                [BacklogWorkItemTypes.Feature])
        ];
    }

    private static IBacklogQualityRule CreateRule(
        string ruleId,
        RuleFamily family,
        string semanticTag,
        string description,
        RuleResponsibleParty responsibleParty,
        RuleFindingClass findingClass,
        IReadOnlyList<string> applicableWorkItemTypes)
    {
        return new PlaceholderBacklogQualityRule(new RuleMetadata(
            ruleId,
            family,
            semanticTag,
            description,
            responsibleParty,
            findingClass,
            applicableWorkItemTypes));
    }
}
