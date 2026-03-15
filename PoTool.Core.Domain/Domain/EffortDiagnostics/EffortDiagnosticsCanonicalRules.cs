namespace PoTool.Core.Domain.EffortDiagnostics;

/// <summary>
/// Shared canonical rules for effort imbalance analysis.
/// </summary>
public static class EffortImbalanceCanonicalRules
{
    /// <summary>
    /// Classifies one bucket's imbalance risk using threshold-relative deviation bands.
    /// </summary>
    public static ImbalanceRiskLevel ClassifyBucketRisk(double deviationFromMean, double threshold)
    {
        if (deviationFromMean < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviationFromMean), "Deviation from mean must be zero or greater.");
        }

        if (threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be greater than zero.");
        }

        return deviationFromMean switch
        {
            var deviation when deviation < threshold => ImbalanceRiskLevel.Low,
            var deviation when deviation < threshold * 1.5 => ImbalanceRiskLevel.Medium,
            var deviation when deviation < threshold * 2.5 => ImbalanceRiskLevel.High,
            _ => ImbalanceRiskLevel.Critical
        };
    }

    /// <summary>
    /// Computes the canonical weighted imbalance score from relative deviations.
    /// </summary>
    public static double ComputeImbalanceScore(IEnumerable<double> deviationsFromMean)
    {
        ArgumentNullException.ThrowIfNull(deviationsFromMean);

        var deviations = deviationsFromMean.ToArray();
        if (deviations.Length == 0)
        {
            return 0;
        }

        if (deviations.Any(deviation => deviation < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(deviationsFromMean), "Deviation values must be zero or greater.");
        }

        var maxDeviation = deviations.Max();
        var averageDeviation = deviations.Average();
        return (maxDeviation * 0.6 + averageDeviation * 0.4) * 100;
    }

    /// <summary>
    /// Classifies the overall imbalance risk from the maximum observed deviation.
    /// </summary>
    public static ImbalanceRiskLevel ClassifyOverallRisk(double maxDeviationFromMean)
    {
        if (maxDeviationFromMean < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDeviationFromMean), "Deviation from mean must be zero or greater.");
        }

        return maxDeviationFromMean switch
        {
            < 0.3 => ImbalanceRiskLevel.Low,
            >= 0.3 and < 0.5 => ImbalanceRiskLevel.Medium,
            >= 0.5 and < 0.8 => ImbalanceRiskLevel.High,
            _ => ImbalanceRiskLevel.Critical
        };
    }
}

/// <summary>
/// Shared canonical rules for effort concentration analysis.
/// </summary>
public static class EffortConcentrationCanonicalRules
{
    /// <summary>
    /// Classifies one bucket's concentration risk from its share of total effort.
    /// </summary>
    public static ConcentrationRiskLevel ClassifyBucketRisk(double shareOfTotal)
    {
        if (shareOfTotal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shareOfTotal), "Share of total must be zero or greater.");
        }

        return shareOfTotal switch
        {
            < 0.25 => ConcentrationRiskLevel.None,
            >= 0.25 and < 0.40 => ConcentrationRiskLevel.Low,
            >= 0.40 and < 0.60 => ConcentrationRiskLevel.Medium,
            >= 0.60 and < 0.80 => ConcentrationRiskLevel.High,
            _ => ConcentrationRiskLevel.Critical
        };
    }

    /// <summary>
    /// Computes the canonical normalized HHI concentration index in the range [0, 100].
    /// </summary>
    public static double ComputeConcentrationIndex(
        IEnumerable<double> sharesOfTotal,
        EffortDiagnosticsStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(sharesOfTotal);
        ArgumentNullException.ThrowIfNull(statistics);

        var shares = sharesOfTotal.ToArray();
        if (shares.Length == 0)
        {
            return 0;
        }

        if (shares.Any(share => share < 0 || share > 1))
        {
            throw new ArgumentOutOfRangeException(nameof(sharesOfTotal), "Shares must fall within the range [0, 1].");
        }

        return Math.Min(100, statistics.Hhi(shares) * 100);
    }

    /// <summary>
    /// Classifies the overall concentration risk from the maximum observed share of total effort.
    /// </summary>
    public static ConcentrationRiskLevel ClassifyOverallRisk(double maxShareOfTotal)
    {
        if (maxShareOfTotal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxShareOfTotal), "Share of total must be zero or greater.");
        }

        return ClassifyBucketRisk(maxShareOfTotal);
    }
}
