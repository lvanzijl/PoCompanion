namespace PoTool.Core.Domain.EffortDiagnostics;

/// <summary>
/// Canonical bucket-level risk classification for effort imbalance.
/// </summary>
public enum ImbalanceRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Represents one analyzed effort bucket in the canonical imbalance model.
/// </summary>
public sealed record EffortImbalanceBucket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EffortImbalanceBucket"/> class.
    /// </summary>
    public EffortImbalanceBucket(
        string bucketKey,
        double observedEffort,
        double meanEffort,
        double deviationFromMean,
        ImbalanceRiskLevel riskLevel)
    {
        if (string.IsNullOrWhiteSpace(bucketKey))
        {
            throw new ArgumentException("Bucket key is required.", nameof(bucketKey));
        }

        if (observedEffort < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(observedEffort), "Observed effort must be zero or greater.");
        }

        if (meanEffort < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(meanEffort), "Mean effort must be zero or greater.");
        }

        if (deviationFromMean < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviationFromMean), "Deviation from mean must be zero or greater.");
        }

        BucketKey = bucketKey;
        ObservedEffort = observedEffort;
        MeanEffort = meanEffort;
        DeviationFromMean = deviationFromMean;
        RiskLevel = riskLevel;
    }

    /// <summary>
    /// Gets the canonical area path or iteration path bucket identifier.
    /// </summary>
    public string BucketKey { get; }

    /// <summary>
    /// Gets the observed effort in this bucket.
    /// </summary>
    public double ObservedEffort { get; }

    /// <summary>
    /// Gets the mean effort across the peer buckets in the same analysis.
    /// </summary>
    public double MeanEffort { get; }

    /// <summary>
    /// Gets the absolute relative deviation from the mean.
    /// </summary>
    public double DeviationFromMean { get; }

    /// <summary>
    /// Gets the canonical imbalance risk classification for this bucket.
    /// </summary>
    public ImbalanceRiskLevel RiskLevel { get; }
}

/// <summary>
/// Represents the stable effort imbalance analysis for area path and iteration path buckets.
/// </summary>
public sealed record EffortImbalanceAnalysis
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EffortImbalanceAnalysis"/> class.
    /// </summary>
    public EffortImbalanceAnalysis(
        IReadOnlyList<EffortImbalanceBucket> areaPathBuckets,
        IReadOnlyList<EffortImbalanceBucket> iterationPathBuckets,
        ImbalanceRiskLevel overallRiskLevel,
        double imbalanceScore)
    {
        ArgumentNullException.ThrowIfNull(areaPathBuckets);
        ArgumentNullException.ThrowIfNull(iterationPathBuckets);

        if (imbalanceScore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imbalanceScore), "Imbalance score must be zero or greater.");
        }

        AreaPathBuckets = areaPathBuckets.ToArray();
        IterationPathBuckets = iterationPathBuckets.ToArray();
        OverallRiskLevel = overallRiskLevel;
        ImbalanceScore = imbalanceScore;
    }

    /// <summary>
    /// Gets the analyzed area path buckets.
    /// </summary>
    public IReadOnlyList<EffortImbalanceBucket> AreaPathBuckets { get; }

    /// <summary>
    /// Gets the analyzed iteration path buckets.
    /// </summary>
    public IReadOnlyList<EffortImbalanceBucket> IterationPathBuckets { get; }

    /// <summary>
    /// Gets the overall imbalance risk level for the analysis.
    /// </summary>
    public ImbalanceRiskLevel OverallRiskLevel { get; }

    /// <summary>
    /// Gets the canonical weighted imbalance score.
    /// </summary>
    public double ImbalanceScore { get; }
}
