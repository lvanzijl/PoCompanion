namespace PoTool.Core.Domain.EffortDiagnostics;

/// <summary>
/// Defines the pure mathematical operations used by the stable effort diagnostics subset.
/// </summary>
public interface EffortDiagnosticsStatistics
{
    /// <summary>
    /// Calculates the arithmetic mean of the supplied values.
    /// </summary>
    double Mean(IEnumerable<double> values);

    /// <summary>
    /// Calculates the absolute relative deviation from the specified mean.
    /// </summary>
    double DeviationFromMean(double value, double mean);

    /// <summary>
    /// Calculates the share of the specified value within the supplied total.
    /// </summary>
    double ShareOfTotal(double value, double total);

    /// <summary>
    /// Calculates the median of the supplied values.
    /// </summary>
    double Median(IEnumerable<double> values);

    /// <summary>
    /// Calculates the population variance of the supplied values.
    /// </summary>
    double Variance(IEnumerable<double> values);

    /// <summary>
    /// Calculates the coefficient of variation from the supplied variance and mean.
    /// </summary>
    double CoefficientOfVariation(double variance, double mean);

    /// <summary>
    /// Calculates the Herfindahl-Hirschman Index from normalized shares in the range [0, 1].
    /// </summary>
    double Hhi(IEnumerable<double> shares);
}

/// <summary>
/// Canonical implementation of the stable effort diagnostics statistical primitives.
/// </summary>
public sealed class CanonicalEffortDiagnosticsStatistics : EffortDiagnosticsStatistics
{
    /// <inheritdoc />
    public double Mean(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var materializedValues = values.ToArray();
        return materializedValues.Length == 0 ? 0 : materializedValues.Average();
    }

    /// <inheritdoc />
    public double DeviationFromMean(double value, double mean)
    {
        return mean > 0 ? Math.Abs(value - mean) / mean : 0;
    }

    /// <inheritdoc />
    public double ShareOfTotal(double value, double total)
    {
        return total > 0 ? value / total : 0;
    }

    /// <inheritdoc />
    public double Median(IEnumerable<double> values)
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

    /// <inheritdoc />
    public double Variance(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var materializedValues = values.ToArray();
        if (materializedValues.Length == 0)
        {
            return 0;
        }

        var mean = Mean(materializedValues);
        return materializedValues.Average(value => Math.Pow(value - mean, 2));
    }

    /// <inheritdoc />
    public double CoefficientOfVariation(double variance, double mean)
    {
        return mean > 0 ? Math.Sqrt(variance) / mean : 0;
    }

    /// <inheritdoc />
    public double Hhi(IEnumerable<double> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);

        return shares.Sum(share => Math.Pow(Math.Max(0, share), 2));
    }
}
