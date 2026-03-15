namespace PoTool.Core.Domain.BacklogQuality.Rules;

/// <summary>
/// Canonical metadata for a backlog-quality rule.
/// </summary>
public sealed record RuleMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuleMetadata"/> class.
    /// </summary>
    public RuleMetadata(
        string ruleId,
        RuleFamily family,
        string semanticTag,
        string description,
        RuleResponsibleParty responsibleParty,
        RuleFindingClass findingClass,
        IReadOnlyList<string> applicableWorkItemTypes)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            throw new ArgumentException("Rule ID is required.", nameof(ruleId));
        }

        if (string.IsNullOrWhiteSpace(semanticTag))
        {
            throw new ArgumentException("Semantic tag is required.", nameof(semanticTag));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        ArgumentNullException.ThrowIfNull(applicableWorkItemTypes);

        var workItemTypes = applicableWorkItemTypes
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (workItemTypes.Length == 0)
        {
            throw new ArgumentException("At least one applicable work item type is required.", nameof(applicableWorkItemTypes));
        }

        RuleId = ruleId;
        Family = family;
        SemanticTag = semanticTag;
        Description = description;
        ResponsibleParty = responsibleParty;
        FindingClass = findingClass;
        ApplicableWorkItemTypes = workItemTypes;
    }

    /// <summary>
    /// Gets the stable rule identifier.
    /// </summary>
    public string RuleId { get; }

    /// <summary>
    /// Gets the canonical rule family.
    /// </summary>
    public RuleFamily Family { get; }

    /// <summary>
    /// Gets the stable semantic tag for adapter grouping and analysis.
    /// </summary>
    public string SemanticTag { get; }

    /// <summary>
    /// Gets the canonical rule description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the party responsible for resolving the finding.
    /// </summary>
    public RuleResponsibleParty ResponsibleParty { get; }

    /// <summary>
    /// Gets the canonical finding class.
    /// </summary>
    public RuleFindingClass FindingClass { get; }

    /// <summary>
    /// Gets the work item types to which the rule applies.
    /// </summary>
    public IReadOnlyList<string> ApplicableWorkItemTypes { get; }
}
