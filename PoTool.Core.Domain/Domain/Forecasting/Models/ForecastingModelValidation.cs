namespace PoTool.Core.Domain.Forecasting.Models;

internal static class ForecastingModelValidation
{
    public static void ValidateNonNegative(double value, string paramName, string displayName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{displayName} must be a finite non-negative value.");
        }
    }

    public static void ValidateCount(int value, string paramName, string displayName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{displayName} must be non-negative.");
        }
    }

    public static void ValidatePercentage(double value, string paramName, string displayName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 100)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{displayName} must be between 0 and 100.");
        }
    }

    public static void ValidateProbability(double value, string paramName, string displayName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{displayName} must be between 0 and 1.");
        }
    }

    public static void ValidateQuartiles(double p25, double p50, double p75)
    {
        if (p25 > p50 || p50 > p75)
        {
            throw new ArgumentOutOfRangeException(nameof(p50), "Velocity quartiles must be ordered P25 <= P50 <= P75.");
        }
    }
}
