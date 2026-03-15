namespace PoTool.Core.Metrics.EffortDiagnostics;

/// <summary>
/// Canonical bucket-level risk classification for effort imbalance as defined in
/// <c>docs/domain/effort_diagnostics_domain_model.md</c>.
/// </summary>
public enum ImbalanceRiskLevel
{
    /// <summary>
    /// Indicates that the bucket or analysis shows low imbalance risk.
    /// </summary>
    Low,

    /// <summary>
    /// Indicates that the bucket or analysis shows medium imbalance risk.
    /// </summary>
    Medium,

    /// <summary>
    /// Indicates that the bucket or analysis shows high imbalance risk.
    /// </summary>
    High,

    /// <summary>
    /// Indicates that the bucket or analysis shows critical imbalance risk.
    /// </summary>
    Critical
}

/// <summary>
/// Represents one canonical effort imbalance bucket defined in
/// <c>docs/domain/effort_diagnostics_domain_model.md</c>.
/// </summary>
public sealed record EffortImbalanceBucket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EffortImbalanceBucket"/> class.
    /// </summary>
    public EffortImbalanceBucket(
        string bucketKey,
        double effortAmount,
        double meanEffort,
        double deviationFromMean,
        ImbalanceRiskLevel riskLevel)
    {
        BucketKey = bucketKey;
        EffortAmount = effortAmount;
        MeanEffort = meanEffort;
        DeviationFromMean = deviationFromMean;
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
    /// Gets the mean effort across peer buckets.
    /// </summary>
    public double MeanEffort { get; }

    /// <summary>
    /// Gets the absolute relative deviation from the mean effort.
    /// </summary>
    public double DeviationFromMean { get; }

    /// <summary>
    /// Gets the canonical imbalance risk level for the bucket.
    /// </summary>
    public ImbalanceRiskLevel RiskLevel { get; }
}

/// <summary>
/// Represents the canonical effort imbalance analysis defined in
/// <c>docs/domain/effort_diagnostics_domain_model.md</c>.
/// </summary>
public sealed record EffortImbalanceAnalysis
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EffortImbalanceAnalysis"/> class.
    /// </summary>
    public EffortImbalanceAnalysis(
        IReadOnlyList<EffortImbalanceBucket> areaBuckets,
        IReadOnlyList<EffortImbalanceBucket> iterationBuckets,
        ImbalanceRiskLevel overallRiskLevel,
        double imbalanceScore)
    {
        ArgumentNullException.ThrowIfNull(areaBuckets);
        ArgumentNullException.ThrowIfNull(iterationBuckets);

        AreaBuckets = areaBuckets.ToArray();
        IterationBuckets = iterationBuckets.ToArray();
        OverallRiskLevel = overallRiskLevel;
        ImbalanceScore = imbalanceScore;
    }

    /// <summary>
    /// Gets the analyzed area-path buckets.
    /// </summary>
    public IReadOnlyList<EffortImbalanceBucket> AreaBuckets { get; }

    /// <summary>
    /// Gets the analyzed iteration-path buckets.
    /// </summary>
    public IReadOnlyList<EffortImbalanceBucket> IterationBuckets { get; }

    /// <summary>
    /// Gets the overall imbalance risk level for the analysis.
    /// </summary>
    public ImbalanceRiskLevel OverallRiskLevel { get; }

    /// <summary>
    /// Gets the weighted imbalance score for the analysis.
    /// </summary>
    public double ImbalanceScore { get; }
}
