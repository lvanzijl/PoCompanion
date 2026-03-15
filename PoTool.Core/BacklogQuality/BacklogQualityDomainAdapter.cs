using PoTool.Core.Contracts;
using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Rules;
using PoTool.Shared.Health;
using PoTool.Shared.WorkItems;
using DomainStateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Core.BacklogQuality;

internal static class BacklogQualityDomainAdapter
{
    public static BacklogGraph CreateGraph(
        IEnumerable<WorkItemDto> workItems,
        Func<WorkItemDto, DomainStateClassification> classify)
    {
        ArgumentNullException.ThrowIfNull(workItems);
        ArgumentNullException.ThrowIfNull(classify);

        return new BacklogGraph(workItems.Select(item => new WorkItemSnapshot(
            item.TfsId,
            item.Type,
            item.ParentTfsId,
            item.Description,
            item.Effort,
            classify(item))));
    }

    public static DomainStateClassification Classify(
        IWorkItemStateClassificationService stateClassificationService,
        WorkItemDto workItem)
    {
        ArgumentNullException.ThrowIfNull(stateClassificationService);
        ArgumentNullException.ThrowIfNull(workItem);

        return stateClassificationService.GetClassificationAsync(workItem.Type, workItem.State).GetAwaiter().GetResult() switch
        {
            Shared.Settings.StateClassification.Done => DomainStateClassification.Done,
            Shared.Settings.StateClassification.Removed => DomainStateClassification.Removed,
            Shared.Settings.StateClassification.InProgress => DomainStateClassification.InProgress,
            _ => DomainStateClassification.New
        };
    }

    public static FeatureOwnerState ToFeatureOwnerState(ReadinessOwnerState ownerState)
    {
        return ownerState switch
        {
            ReadinessOwnerState.PO => FeatureOwnerState.PO,
            ReadinessOwnerState.Team => FeatureOwnerState.Team,
            _ => FeatureOwnerState.Ready
        };
    }

    public static PoTool.Shared.WorkItems.ValidationRuleResult ToLegacyValidationResult(
        PoTool.Core.Domain.BacklogQuality.Models.ValidationRuleResult finding,
        string? additionalContext = null)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return new PoTool.Shared.WorkItems.ValidationRuleResult(
            CreateLegacyRule(finding.Rule, finding.Message),
            finding.WorkItemId,
            IsViolated: true,
            additionalContext);
    }

    public static ValidationRule CreateLegacyRule(RuleMetadata metadata, string message)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new ValidationRule(
            metadata.RuleId,
            metadata.Family switch
            {
                RuleFamily.StructuralIntegrity => ValidationCategory.StructuralIntegrity,
                RuleFamily.RefinementReadiness => ValidationCategory.RefinementReadiness,
                _ => string.Equals(metadata.SemanticTag, "MissingEffort", StringComparison.Ordinal)
                    ? ValidationCategory.MissingEffort
                    : ValidationCategory.RefinementCompleteness
            },
            metadata.Family switch
            {
                RuleFamily.StructuralIntegrity => ValidationConsequence.BacklogHealthProblem,
                RuleFamily.RefinementReadiness => ValidationConsequence.RefinementBlocker,
                _ => ValidationConsequence.IncompleteRefinement
            },
            metadata.ResponsibleParty switch
            {
                RuleResponsibleParty.Process => ResponsibleParty.Process,
                RuleResponsibleParty.Team => ResponsibleParty.DevelopmentTeam,
                _ => ResponsibleParty.ProductOwner
            },
            message);
    }
}
