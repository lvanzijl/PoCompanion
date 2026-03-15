using PoTool.Core.Domain.Statistics;

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
    double HHI(IEnumerable<double> shares);
}

/// <summary>
/// Canonical implementation of the stable effort diagnostics statistical primitives.
/// </summary>
public sealed class CanonicalEffortDiagnosticsStatistics : EffortDiagnosticsStatistics
{
    /// <inheritdoc />
    public double Mean(IEnumerable<double> values)
    {
        return StatisticsMath.Mean(values);
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
        return StatisticsMath.Median(values);
    }

    /// <inheritdoc />
    public double Variance(IEnumerable<double> values)
    {
        return StatisticsMath.Variance(values);
    }

    /// <inheritdoc />
    public double CoefficientOfVariation(double variance, double mean)
    {
        return mean > 0 ? Math.Sqrt(variance) / mean : 0;
    }

    /// <inheritdoc />
    public double HHI(IEnumerable<double> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);

        var shareValues = shares.ToArray();
        if (shareValues.Any(share => share is < 0 or > 1))
        {
            throw new ArgumentOutOfRangeException(nameof(shares), "Shares must fall within the range [0, 1].");
        }

        return shareValues.Sum(share => Math.Pow(share, 2));
    }
}
