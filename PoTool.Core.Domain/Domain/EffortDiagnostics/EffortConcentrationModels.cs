namespace PoTool.Core.Domain.EffortDiagnostics;

/// <summary>
/// Canonical bucket-level risk classification for effort concentration.
/// </summary>
public enum ConcentrationRiskLevel
{
    None,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Represents one analyzed concentration bucket in the canonical effort concentration model.
/// </summary>
public sealed record EffortConcentrationBucket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EffortConcentrationBucket"/> class.
    /// </summary>
    public EffortConcentrationBucket(
        string bucketKey,
        double effortAmount,
        double effortShare,
        ConcentrationRiskLevel riskLevel)
    {
        if (string.IsNullOrWhiteSpace(bucketKey))
        {
            throw new ArgumentException("Bucket key is required.", nameof(bucketKey));
        }

        if (effortAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(effortAmount), "Effort amount must be zero or greater.");
        }

        if (effortShare < 0 || effortShare > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(effortShare), "Effort share must fall within the range [0, 1].");
        }

        BucketKey = bucketKey;
        EffortAmount = effortAmount;
        EffortShare = effortShare;
        RiskLevel = riskLevel;
    }

    /// <summary>
    /// Gets the canonical area path or iteration path bucket identifier.
    /// </summary>
    public string BucketKey { get; }

    /// <summary>
    /// Gets the observed effort amount in this bucket.
    /// </summary>
    public double EffortAmount { get; }

    /// <summary>
    /// Gets the share of total effort carried by this bucket.
    /// </summary>
    public double EffortShare { get; }

    /// <summary>
    /// Gets the canonical concentration risk classification for this bucket.
    /// </summary>
    public ConcentrationRiskLevel RiskLevel { get; }
}

/// <summary>
/// Represents the stable effort concentration analysis for area path and iteration path buckets.
/// </summary>
public sealed record EffortConcentrationAnalysis
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EffortConcentrationAnalysis"/> class.
    /// </summary>
    public EffortConcentrationAnalysis(
        IReadOnlyList<EffortConcentrationBucket> areaPathBuckets,
        IReadOnlyList<EffortConcentrationBucket> iterationPathBuckets,
        ConcentrationRiskLevel overallRiskLevel,
        double concentrationIndex)
    {
        ArgumentNullException.ThrowIfNull(areaPathBuckets);
        ArgumentNullException.ThrowIfNull(iterationPathBuckets);

        if (concentrationIndex < 0 || concentrationIndex > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(concentrationIndex), "Concentration index must fall within the range [0, 100].");
        }

        AreaPathBuckets = areaPathBuckets.ToArray();
        IterationPathBuckets = iterationPathBuckets.ToArray();
        OverallRiskLevel = overallRiskLevel;
        ConcentrationIndex = concentrationIndex;
    }

    /// <summary>
    /// Gets the analyzed area path buckets.
    /// </summary>
    public IReadOnlyList<EffortConcentrationBucket> AreaPathBuckets { get; }

    /// <summary>
    /// Gets the analyzed iteration path buckets.
    /// </summary>
    public IReadOnlyList<EffortConcentrationBucket> IterationPathBuckets { get; }

    /// <summary>
    /// Gets the overall concentration risk level for the analysis.
    /// </summary>
    public ConcentrationRiskLevel OverallRiskLevel { get; }

    /// <summary>
    /// Gets the normalized HHI concentration index in the range [0, 100].
    /// </summary>
    public double ConcentrationIndex { get; }
}
