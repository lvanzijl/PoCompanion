using Mediator;
using PoTool.Core.Delivery.Filters;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Queries;

/// <summary>
/// Query to compute capacity calibration metrics across a selected sprint range.
/// </summary>
public sealed record GetCapacityCalibrationQuery(
    DeliveryEffectiveFilter EffectiveFilter
) : IQuery<CapacityCalibrationDto>;
