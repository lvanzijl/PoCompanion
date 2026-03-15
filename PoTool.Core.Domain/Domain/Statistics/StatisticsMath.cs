namespace PoTool.Core.Domain.Statistics;

/// <summary>
/// Repository-shared pure-math statistical primitives with deterministic contracts.
/// </summary>
public static class StatisticsMath
{
    /// <summary>
    /// Calculates the arithmetic mean of the supplied unsorted values.
    /// Returns <c>0</c> when the sample is empty.
    /// </summary>
    public static double Mean(IEnumerable<double> values)
    {
        var sample = Materialize(values);
        return sample.Length == 0 ? 0 : sample.Average();
    }

    /// <summary>
    /// Calculates the population variance of the supplied unsorted values.
    /// Returns <c>0</c> when the sample is empty.
    /// </summary>
    public static double Variance(IEnumerable<double> values)
    {
        var sample = Materialize(values);
        if (sample.Length == 0)
        {
            return 0;
        }

        var mean = Mean(sample);
        return sample.Average(value =>
        {
            var difference = value - mean;
            return difference * difference;
        });
    }

    /// <summary>
    /// Calculates the population standard deviation of the supplied unsorted values.
    /// Returns <c>0</c> when the sample is empty.
    /// </summary>
    public static double StandardDeviation(IEnumerable<double> values)
    {
        return Math.Sqrt(Variance(values));
    }

    /// <summary>
    /// Calculates the median of the supplied unsorted values.
    /// Returns <c>0</c> when the sample is empty.
    /// For even-sized samples, returns the arithmetic mean of the two middle values as a <see cref="double"/>.
    /// </summary>
    public static double Median(IEnumerable<double> values)
    {
        var sample = Materialize(values);
        if (sample.Length == 0)
        {
            return 0;
        }

        Array.Sort(sample);

        var midpoint = sample.Length / 2;
        return sample.Length % 2 == 0
            ? (sample[midpoint - 1] + sample[midpoint]) / 2.0
            : sample[midpoint];
    }

    private static double[] Materialize(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values as double[] ?? values.ToArray();
    }
}
