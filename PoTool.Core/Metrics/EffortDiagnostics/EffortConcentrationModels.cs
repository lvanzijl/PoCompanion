namespace PoTool.Core.Metrics.EffortDiagnostics;

/// <summary>
/// Canonical bucket-level risk classification for effort concentration as defined in
/// <c>docs/domain/effort_diagnostics_domain_model.md</c>.
/// </summary>
public enum ConcentrationRiskLevel
{
    /// <summary>
    /// Indicates that the bucket or analysis shows no concentration risk.
    /// </summary>
    None,

    /// <summary>
    /// Indicates that the bucket or analysis shows low concentration risk.
    /// </summary>
    Low,

    /// <summary>
    /// Indicates that the bucket or analysis shows medium concentration risk.
    /// </summary>
    Medium,

    /// <summary>
    /// Indicates that the bucket or analysis shows high concentration risk.
    /// </summary>
    High,

    /// <summary>
    /// Indicates that the bucket or analysis shows critical concentration risk.
    /// </summary>
    Critical
}

/// <summary>
/// Represents one canonical effort concentration bucket defined in
/// <c>docs/domain/effort_diagnostics_domain_model.md</c>.
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
        BucketKey = bucketKey;
        EffortAmount = effortAmount;
        EffortShare = effortShare;
        RiskLevel = riskLevel;
    }

    /// <summary>
    /// Gets the canonical bucket identifier.
    /// </summary>
    public string BucketKey { get; }

    /// <summary>
    /// Gets the observed effort amount in the bucket.
    /// </summary>
    public double EffortAmount { get; }

    /// <summary>
    /// Gets the share of total effort carried by the bucket.
    /// </summary>
    public double EffortShare { get; }

    /// <summary>
    /// Gets the canonical concentration risk level for the bucket.
    /// </summary>
    public ConcentrationRiskLevel RiskLevel { get; }
}

/// <summary>
/// Represents the canonical effort concentration analysis defined in
/// <c>docs/domain/effort_diagnostics_domain_model.md</c>.
/// </summary>
public sealed record EffortConcentrationAnalysis
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EffortConcentrationAnalysis"/> class.
    /// </summary>
    public EffortConcentrationAnalysis(
        IReadOnlyList<EffortConcentrationBucket> areaBuckets,
        IReadOnlyList<EffortConcentrationBucket> iterationBuckets,
        ConcentrationRiskLevel overallRiskLevel,
        double concentrationIndex)
    {
        ArgumentNullException.ThrowIfNull(areaBuckets);
        ArgumentNullException.ThrowIfNull(iterationBuckets);

        AreaBuckets = areaBuckets.ToArray();
        IterationBuckets = iterationBuckets.ToArray();
        OverallRiskLevel = overallRiskLevel;
        ConcentrationIndex = concentrationIndex;
    }

    /// <summary>
    /// Gets the analyzed area-path buckets.
    /// </summary>
    public IReadOnlyList<EffortConcentrationBucket> AreaBuckets { get; }

    /// <summary>
    /// Gets the analyzed iteration-path buckets.
    /// </summary>
    public IReadOnlyList<EffortConcentrationBucket> IterationBuckets { get; }

    /// <summary>
    /// Gets the overall concentration risk level for the analysis.
    /// </summary>
    public ConcentrationRiskLevel OverallRiskLevel { get; }

    /// <summary>
    /// Gets the normalized concentration index for the analysis.
    /// </summary>
    public double ConcentrationIndex { get; }
}
