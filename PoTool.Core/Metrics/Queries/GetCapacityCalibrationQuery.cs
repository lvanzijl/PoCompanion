using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to compute capacity calibration metrics across a selected sprint range.
/// </summary>
/// <param name="ProductOwnerId">Product Owner (Profile) ID — scopes data to this owner's products.</param>
/// <param name="SprintIds">Sprint IDs to include, ordered chronologically.</param>
/// <param name="ProductIds">
///   Optional product filter. When null or empty, all products owned by the ProductOwner are aggregated.
/// </param>
public sealed record GetCapacityCalibrationQuery(
    int ProductOwnerId,
    IReadOnlyList<int> SprintIds,
    int[]? ProductIds = null
) : IQuery<CapacityCalibrationDto>;
