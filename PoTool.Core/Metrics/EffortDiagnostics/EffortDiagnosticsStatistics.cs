namespace PoTool.Core.Metrics.EffortDiagnostics;

/// <summary>
/// Shared statistical helpers for the stable effort diagnostics subset.
/// </summary>
public static class EffortDiagnosticsStatistics
{
    public static double Mean(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var materializedValues = values.ToArray();
        return materializedValues.Length == 0 ? 0 : materializedValues.Average();
    }

    public static double DeviationFromMean(double value, double mean)
    {
        return mean > 0 ? Math.Abs(value - mean) / mean : 0;
    }

    public static double ShareOfTotal(double value, double total)
    {
        return total > 0 ? value / total : 0;
    }

    public static double Median(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var orderedValues = values
            .OrderBy(value => value)
            .ToArray();

        if (orderedValues.Length == 0)
        {
            return 0;
        }

        var midpoint = orderedValues.Length / 2;
        return orderedValues.Length % 2 == 0
            ? (orderedValues[midpoint - 1] + orderedValues[midpoint]) / 2.0
            : orderedValues[midpoint];
    }

    public static double Variance(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var materializedValues = values.ToArray();
        if (materializedValues.Length == 0)
        {
            return 0;
        }

        var mean = materializedValues.Average();
        return materializedValues.Average(value => Math.Pow(value - mean, 2));
    }

    public static double CoefficientOfVariation(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var materializedValues = values.ToArray();
        if (materializedValues.Length == 0)
        {
            return 0;
        }

        var mean = materializedValues.Average();
        if (mean <= 0)
        {
            return 0;
        }

        var variance = materializedValues.Average(value => Math.Pow(value - mean, 2));
        return Math.Sqrt(variance) / mean;
    }

    public static double HHI(IEnumerable<double> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);

        var shareValues = shares.ToArray();
        if (shareValues.Any(share => share < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(shares), "Shares must be non-negative.");
        }

        // Herfindahl-Hirschman Index on normalized shares (0-1), scaled to a 0-100 score.
        var hhi = shareValues.Sum(share => Math.Pow(share, 2));
        return Math.Min(100, hhi * 100);
    }

    public static double CalculateDeviationFromMean(double value, double mean)
    {
        return DeviationFromMean(value, mean);
    }

    public static double CalculateShareOfTotal(double value, double total)
    {
        return ShareOfTotal(value, total);
    }

    public static double CalculateWeightedDeviationScore(IEnumerable<double> deviationPercentages)
    {
        ArgumentNullException.ThrowIfNull(deviationPercentages);

        var deviations = deviationPercentages.ToArray();
        if (deviations.Length == 0)
        {
            return 0;
        }

        var maxDeviation = deviations.Max() / 100.0;
        var averageDeviation = deviations.Average() / 100.0;
        return (maxDeviation * 0.6 + averageDeviation * 0.4) * 100;
    }

    public static double CalculateNormalizedHerfindahlIndex(IEnumerable<double> percentageShares)
    {
        ArgumentNullException.ThrowIfNull(percentageShares);

        return HHI(percentageShares.Select(share => share / 100.0));
    }
}
