namespace PoTool.Core.Domain.BacklogQuality.Rules;

/// <summary>
/// Metadata-backed placeholder rule used until executable domain rule implementations are introduced.
/// </summary>
public sealed class PlaceholderBacklogQualityRule : IBacklogQualityRule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlaceholderBacklogQualityRule"/> class.
    /// </summary>
    public PlaceholderBacklogQualityRule(RuleMetadata metadata)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    /// <inheritdoc />
    public RuleMetadata Metadata { get; }

    /// <inheritdoc />
    public IReadOnlyList<Models.ValidationRuleResult> Evaluate(Models.BacklogGraph backlogGraph)
    {
        ArgumentNullException.ThrowIfNull(backlogGraph);
        return Array.Empty<Models.ValidationRuleResult>();
    }
}
