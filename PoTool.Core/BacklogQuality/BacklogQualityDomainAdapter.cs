using PoTool.Core.Contracts;
using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Rules;
using PoTool.Shared.Health;
using PoTool.Shared.Settings;
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
        IReadOnlyDictionary<(string WorkItemType, string StateName), DomainStateClassification> classifications,
        WorkItemDto workItem)
    {
        ArgumentNullException.ThrowIfNull(classifications);
        ArgumentNullException.ThrowIfNull(workItem);

        return classifications.TryGetValue(
            (Normalize(workItem.Type), Normalize(workItem.State)),
            out var classification)
            ? classification
            : DomainStateClassification.New;
    }

    public static IReadOnlyDictionary<(string WorkItemType, string StateName), DomainStateClassification> CreateClassificationLookup(
        IWorkItemStateClassificationService stateClassificationService)
    {
        ArgumentNullException.ThrowIfNull(stateClassificationService);

        return CreateClassificationLookup(stateClassificationService.GetClassificationsAsync().GetAwaiter().GetResult().Classifications);
    }

    public static IReadOnlyDictionary<(string WorkItemType, string StateName), DomainStateClassification> CreateClassificationLookup(
        IEnumerable<WorkItemStateClassificationDto> classifications)
    {
        ArgumentNullException.ThrowIfNull(classifications);

        return classifications
            .GroupBy(item => (Normalize(item.WorkItemType), Normalize(item.StateName)))
            .ToDictionary(
                group => group.Key,
                group => group.Last().Classification switch
                {
                    Shared.Settings.StateClassification.Done => DomainStateClassification.Done,
                    Shared.Settings.StateClassification.Removed => DomainStateClassification.Removed,
                    Shared.Settings.StateClassification.InProgress => DomainStateClassification.InProgress,
                    _ => DomainStateClassification.New
                });
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
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
