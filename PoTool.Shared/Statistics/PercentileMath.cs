namespace PoTool.Shared.Statistics;

/// <summary>
/// Repository-shared percentile helper with a canonical linear-interpolation contract.
/// </summary>
public static class PercentileMath
{
    /// <summary>
    /// Calculates the requested percentile from a pre-sorted ascending sample using linear interpolation.
    /// The input must already be sorted in ascending order; this helper does not sort or validate ordering.
    /// Returns <c>0</c> when the sample is empty and returns the only sample value when exactly one value is supplied.
    /// <paramref name="percentile"/> must be within the inclusive range <c>[0, 100]</c>.
    /// </summary>
    public static double LinearInterpolation(IReadOnlyList<double> sortedValues, double percentile)
    {
        ArgumentNullException.ThrowIfNull(sortedValues);

        if (double.IsNaN(percentile) || double.IsInfinity(percentile) || percentile < 0 || percentile > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentile),
                percentile,
                "Percentile must be within the inclusive range [0, 100].");
        }

        if (sortedValues.Count == 0)
        {
            return 0;
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var rank = (percentile / 100.0) * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var lowerValue = sortedValues[lowerIndex];
        var upperValue = sortedValues[upperIndex];
        var fraction = rank - lowerIndex;

        return lowerValue + ((upperValue - lowerValue) * fraction);
    }
}
