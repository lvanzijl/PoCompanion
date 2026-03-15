namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Represents bounded forward progress gained within a sprint on a 0-100 scale.
/// </summary>
public readonly record struct ProgressionDelta
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressionDelta"/> struct.
    /// </summary>
    /// <param name="percentage">The bounded percentage value in the range [0, 100].</param>
    public ProgressionDelta(double percentage)
    {
        DeliveryTrendModelValidation.ValidateBoundedPercentage(percentage, nameof(percentage), "Progression delta");
        Percentage = percentage;
    }

    /// <summary>
    /// Gets the bounded progress gain percentage.
    /// </summary>
    public double Percentage { get; }
}
