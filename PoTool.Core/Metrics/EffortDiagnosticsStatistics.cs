namespace PoTool.Core.Metrics;

/// <summary>
/// Backwards-compatible forwarding wrapper for effort diagnostics statistical helpers.
/// </summary>
public static class EffortDiagnosticsStatistics
{
    public static double Mean(IEnumerable<double> values)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.Mean(values);
    }

    public static double DeviationFromMean(double value, double mean)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.DeviationFromMean(value, mean);
    }

    public static double ShareOfTotal(double value, double total)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.ShareOfTotal(value, total);
    }

    public static double Median(IEnumerable<double> values)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.Median(values);
    }

    public static double Variance(IEnumerable<double> values)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.Variance(values);
    }

    public static double CoefficientOfVariation(IEnumerable<double> values)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.CoefficientOfVariation(values);
    }

    public static double HHI(IEnumerable<double> shares)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.HHI(shares);
    }

    public static double CalculateDeviationFromMean(double value, double mean)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.CalculateDeviationFromMean(value, mean);
    }

    public static double CalculateShareOfTotal(double value, double total)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.CalculateShareOfTotal(value, total);
    }

    public static double CalculateWeightedDeviationScore(IEnumerable<double> deviationPercentages)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.CalculateWeightedDeviationScore(deviationPercentages);
    }

    public static double CalculateNormalizedHerfindahlIndex(IEnumerable<double> percentageShares)
    {
        return EffortDiagnostics.EffortDiagnosticsStatistics.CalculateNormalizedHerfindahlIndex(percentageShares);
    }
}
