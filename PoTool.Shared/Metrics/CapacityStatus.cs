namespace PoTool.Shared.Metrics;

/// <summary>
/// Status indicating if effort is within, near, or exceeding capacity.
/// </summary>
public enum CapacityStatus
{
    Unknown = 0,
    Underutilized = 1,
    Normal = 2,
    NearCapacity = 3,
    OverCapacity = 4
}
