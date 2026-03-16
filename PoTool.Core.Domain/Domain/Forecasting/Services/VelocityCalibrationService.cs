using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Shared.Statistics;

namespace PoTool.Core.Domain.Forecasting.Services;

public interface IVelocityCalibrationService
{
    VelocityCalibration Calibrate(IReadOnlyList<VelocityCalibrationSample> samples);
}

public sealed class VelocityCalibrationService : IVelocityCalibrationService
{
    public VelocityCalibration Calibrate(IReadOnlyList<VelocityCalibrationSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            return new VelocityCalibration(
                Array.Empty<VelocityCalibrationEntry>(),
                0,
                0,
                0,
                0,
                Array.Empty<string>());
        }

        var entries = samples
            .Select(sample =>
            {
                var committedStoryPoints = Math.Max(0d, sample.PlannedStoryPoints - sample.DerivedStoryPoints);
                var hoursPerStoryPoint = sample.CompletedStoryPoints > 0
                    ? (double)sample.CompletedEffort / sample.CompletedStoryPoints
                    : 0d;
                var predictabilityRatio = committedStoryPoints > 0
                    ? sample.CompletedStoryPoints / committedStoryPoints
                    : 0d;

                return new VelocityCalibrationEntry(
                    sample.SprintName,
                    Math.Round(committedStoryPoints, 3),
                    Math.Round(sample.CompletedStoryPoints, 3),
                    sample.CompletedEffort,
                    Math.Round(hoursPerStoryPoint, 3),
                    predictabilityRatio);
            })
            .ToList();

        var velocities = entries
            .Select(static entry => entry.DeliveredStoryPoints)
            .OrderBy(static velocity => velocity)
            .ToList();

        var medianVelocity = PercentileMath.LinearInterpolation(velocities, 50);
        var p25Velocity = PercentileMath.LinearInterpolation(velocities, 25);
        var p75Velocity = PercentileMath.LinearInterpolation(velocities, 75);
        var p10Velocity = PercentileMath.LinearInterpolation(velocities, 10);
        var p90Velocity = PercentileMath.LinearInterpolation(velocities, 90);

        var outlierSprintNames = entries
            .Where(entry => entry.DeliveredStoryPoints < p10Velocity || entry.DeliveredStoryPoints > p90Velocity)
            .Select(static entry => entry.SprintName)
            .ToList();

        var predictabilityValues = entries
            .Where(static entry => entry.CommittedStoryPoints > 0)
            .Select(static entry => entry.PredictabilityRatio)
            .OrderBy(static ratio => ratio)
            .ToList();

        var medianPredictability = predictabilityValues.Count > 0
            ? PercentileMath.LinearInterpolation(predictabilityValues, 50)
            : 0d;

        return new VelocityCalibration(
            entries.AsReadOnly(),
            Math.Round(medianVelocity, 1),
            Math.Round(p25Velocity, 1),
            Math.Round(p75Velocity, 1),
            Math.Round(medianPredictability, 3),
            outlierSprintNames.AsReadOnly());
    }
}
