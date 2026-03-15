namespace PoTool.Core.Metrics;

/// <summary>
/// Shared statistical helpers for the stable effort diagnostics subset.
/// </summary>
public static class EffortDiagnosticsStatistics
{
    public static double CalculateDeviationFromMean(double value, double mean)
    {
        return mean > 0 ? Math.Abs(value - mean) / mean : 0;
    }

    public static double CalculateShareOfTotal(double value, double total)
    {
        return total > 0 ? value / total : 0;
    }

    public static double CalculateWeightedDeviationScore(IEnumerable<double> deviationPercentages)
    {
        var deviations = deviationPercentages.ToList();
        if (deviations.Count == 0)
        {
            return 0;
        }

        var maxDeviation = deviations.Max() / 100.0;
        var averageDeviation = deviations.Average() / 100.0;
        return (maxDeviation * 0.6 + averageDeviation * 0.4) * 100;
    }

    public static double CalculateNormalizedHerfindahlIndex(IEnumerable<double> percentageShares)
    {
        var shares = percentageShares
            .Select(share => share / 100.0)
            .ToList();

        if (shares.Count == 0)
        {
            return 0;
        }

        var hhi = shares.Sum(share => Math.Pow(share, 2)) * 10000;
        return Math.Min(100, hhi / 100);
    }
}
