namespace PoTool.Client.Services;

/// <summary>
/// Format type for displaying metric values.
/// </summary>
public enum MetricValueFormat
{
    /// <summary>
    /// Display as hours (with automatic conversion to days/hours/minutes).
    /// </summary>
    Hours,

    /// <summary>
    /// Display as days.
    /// </summary>
    Days,

    /// <summary>
    /// Display as raw count/number.
    /// </summary>
    Count
}
