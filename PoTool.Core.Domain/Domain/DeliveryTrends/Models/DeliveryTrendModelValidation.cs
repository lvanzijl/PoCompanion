namespace PoTool.Core.Domain.DeliveryTrends.Models;

internal static class DeliveryTrendModelValidation
{
    public static void ValidatePositiveId(int value, string paramName, string displayName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{displayName} must be greater than zero.");
        }
    }

    public static void ValidateRequiredText(string? value, string paramName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} is required.", paramName);
        }
    }

    public static void ValidateBoundedPercentage(double value, string paramName, string displayName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 100)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{displayName} must fall within the range [0, 100].");
        }
    }

    public static void ValidateCount(int value, string paramName, string displayName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{displayName} must be zero or greater.");
        }
    }

    public static void ValidateNonNegativeStoryPoints(double value, string paramName, string displayName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{displayName} must be finite and zero or greater.");
        }
    }
}
