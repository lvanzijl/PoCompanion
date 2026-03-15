namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Represents the canonical multi-product sprint trend metrics for a single sprint.
/// </summary>
public sealed record SprintTrendMetrics
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SprintTrendMetrics"/> class.
    /// </summary>
    public SprintTrendMetrics(
        int sprintId,
        string sprintName,
        DateTimeOffset? startUtc,
        DateTimeOffset? endUtc,
        IReadOnlyList<SprintDeliveryProjection> productProjections)
    {
        if (sprintId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sprintId), "Sprint ID must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(sprintName))
        {
            throw new ArgumentException("Sprint name is required.", nameof(sprintName));
        }

        ArgumentNullException.ThrowIfNull(productProjections);

        if (startUtc.HasValue && endUtc.HasValue && endUtc.Value < startUtc.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(endUtc), "Sprint end must be on or after sprint start.");
        }

        SprintId = sprintId;
        SprintName = sprintName;
        StartUtc = startUtc;
        EndUtc = endUtc;
        ProductProjections = productProjections.ToArray();
    }

    public int SprintId { get; }

    public string SprintName { get; }

    public DateTimeOffset? StartUtc { get; }

    public DateTimeOffset? EndUtc { get; }

    public IReadOnlyList<SprintDeliveryProjection> ProductProjections { get; }

    public int TotalPlannedCount => ProductProjections.Sum(projection => projection.PlannedCount);

    public int TotalPlannedEffort => ProductProjections.Sum(projection => projection.PlannedEffort);

    public double TotalPlannedStoryPoints => ProductProjections.Sum(projection => projection.PlannedStoryPoints);

    public int TotalWorkedCount => ProductProjections.Sum(projection => projection.WorkedCount);

    public int TotalWorkedEffort => ProductProjections.Sum(projection => projection.WorkedEffort);

    public int TotalBugsPlannedCount => ProductProjections.Sum(projection => projection.BugsPlannedCount);

    public int TotalBugsWorkedCount => ProductProjections.Sum(projection => projection.BugsWorkedCount);

    public int TotalCompletedPbiCount => ProductProjections.Sum(projection => projection.CompletedPbiCount);

    public int TotalCompletedPbiEffort => ProductProjections.Sum(projection => projection.CompletedPbiEffort);

    public double TotalCompletedPbiStoryPoints => ProductProjections.Sum(projection => projection.CompletedPbiStoryPoints);

    public int TotalSpilloverCount => ProductProjections.Sum(projection => projection.SpilloverCount);

    public int TotalSpilloverEffort => ProductProjections.Sum(projection => projection.SpilloverEffort);

    public double TotalSpilloverStoryPoints => ProductProjections.Sum(projection => projection.SpilloverStoryPoints);

    public double TotalProgressionDelta => ProductProjections.Sum(projection => projection.ProgressionDelta.Percentage);

    public int TotalBugsCreatedCount => ProductProjections.Sum(projection => projection.BugsCreatedCount);

    public int TotalBugsClosedCount => ProductProjections.Sum(projection => projection.BugsClosedCount);

    public int TotalMissingEffortCount => ProductProjections.Sum(projection => projection.MissingEffortCount);

    public int TotalMissingStoryPointCount => ProductProjections.Sum(projection => projection.MissingStoryPointCount);

    public int TotalDerivedStoryPointCount => ProductProjections.Sum(projection => projection.DerivedStoryPointCount);

    public double TotalDerivedStoryPoints => ProductProjections.Sum(projection => projection.DerivedStoryPoints);

    public int TotalUnestimatedDeliveryCount => ProductProjections.Sum(projection => projection.UnestimatedDeliveryCount);

    public bool IsApproximate => ProductProjections.Any(projection => projection.IsApproximate);
}
