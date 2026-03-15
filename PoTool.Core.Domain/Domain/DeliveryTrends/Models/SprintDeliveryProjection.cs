namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Represents the canonical sprint delivery projection for one sprint/product combination.
/// </summary>
public sealed record SprintDeliveryProjection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SprintDeliveryProjection"/> class.
    /// </summary>
    public SprintDeliveryProjection(
        int sprintId,
        int productId,
        int plannedCount,
        int plannedEffort,
        double plannedStoryPoints,
        int workedCount,
        int workedEffort,
        int bugsPlannedCount,
        int bugsWorkedCount,
        int completedPbiCount,
        int completedPbiEffort,
        double completedPbiStoryPoints,
        int spilloverCount,
        int spilloverEffort,
        double spilloverStoryPoints,
        ProgressionDelta progressionDelta,
        int bugsCreatedCount,
        int bugsClosedCount,
        int missingEffortCount,
        int missingStoryPointCount,
        int derivedStoryPointCount,
        double derivedStoryPoints,
        int unestimatedDeliveryCount,
        bool isApproximate)
    {
        DeliveryTrendModelValidation.ValidatePositiveId(sprintId, nameof(sprintId), "Sprint ID");
        DeliveryTrendModelValidation.ValidatePositiveId(productId, nameof(productId), "Product ID");
        DeliveryTrendModelValidation.ValidateCount(plannedCount, nameof(plannedCount), "Planned count");
        DeliveryTrendModelValidation.ValidateCount(plannedEffort, nameof(plannedEffort), "Planned effort");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(plannedStoryPoints, nameof(plannedStoryPoints), "Planned story points");
        DeliveryTrendModelValidation.ValidateCount(workedCount, nameof(workedCount), "Worked count");
        DeliveryTrendModelValidation.ValidateCount(workedEffort, nameof(workedEffort), "Worked effort");
        DeliveryTrendModelValidation.ValidateCount(bugsPlannedCount, nameof(bugsPlannedCount), "Planned bug count");
        DeliveryTrendModelValidation.ValidateCount(bugsWorkedCount, nameof(bugsWorkedCount), "Worked bug count");
        DeliveryTrendModelValidation.ValidateCount(completedPbiCount, nameof(completedPbiCount), "Completed PBI count");
        DeliveryTrendModelValidation.ValidateCount(completedPbiEffort, nameof(completedPbiEffort), "Completed PBI effort");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(completedPbiStoryPoints, nameof(completedPbiStoryPoints), "Completed PBI story points");
        DeliveryTrendModelValidation.ValidateCount(spilloverCount, nameof(spilloverCount), "Spillover count");
        DeliveryTrendModelValidation.ValidateCount(spilloverEffort, nameof(spilloverEffort), "Spillover effort");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(spilloverStoryPoints, nameof(spilloverStoryPoints), "Spillover story points");
        DeliveryTrendModelValidation.ValidateCount(bugsCreatedCount, nameof(bugsCreatedCount), "Created bug count");
        DeliveryTrendModelValidation.ValidateCount(bugsClosedCount, nameof(bugsClosedCount), "Closed bug count");
        DeliveryTrendModelValidation.ValidateCount(missingEffortCount, nameof(missingEffortCount), "Missing effort count");
        DeliveryTrendModelValidation.ValidateCount(missingStoryPointCount, nameof(missingStoryPointCount), "Missing story-point count");
        DeliveryTrendModelValidation.ValidateCount(derivedStoryPointCount, nameof(derivedStoryPointCount), "Derived story-point count");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(derivedStoryPoints, nameof(derivedStoryPoints), "Derived story points");
        DeliveryTrendModelValidation.ValidateCount(unestimatedDeliveryCount, nameof(unestimatedDeliveryCount), "Unestimated delivery count");

        SprintId = sprintId;
        ProductId = productId;
        PlannedCount = plannedCount;
        PlannedEffort = plannedEffort;
        PlannedStoryPoints = plannedStoryPoints;
        WorkedCount = workedCount;
        WorkedEffort = workedEffort;
        BugsPlannedCount = bugsPlannedCount;
        BugsWorkedCount = bugsWorkedCount;
        CompletedPbiCount = completedPbiCount;
        CompletedPbiEffort = completedPbiEffort;
        CompletedPbiStoryPoints = completedPbiStoryPoints;
        SpilloverCount = spilloverCount;
        SpilloverEffort = spilloverEffort;
        SpilloverStoryPoints = spilloverStoryPoints;
        ProgressionDelta = progressionDelta;
        BugsCreatedCount = bugsCreatedCount;
        BugsClosedCount = bugsClosedCount;
        MissingEffortCount = missingEffortCount;
        MissingStoryPointCount = missingStoryPointCount;
        DerivedStoryPointCount = derivedStoryPointCount;
        DerivedStoryPoints = derivedStoryPoints;
        UnestimatedDeliveryCount = unestimatedDeliveryCount;
        IsApproximate = isApproximate;
    }

    public int SprintId { get; }

    public int ProductId { get; }

    public int PlannedCount { get; }

    public int PlannedEffort { get; }

    public double PlannedStoryPoints { get; }

    public int WorkedCount { get; }

    public int WorkedEffort { get; }

    public int BugsPlannedCount { get; }

    public int BugsWorkedCount { get; }

    public int CompletedPbiCount { get; }

    public int CompletedPbiEffort { get; }

    public double CompletedPbiStoryPoints { get; }

    public int SpilloverCount { get; }

    public int SpilloverEffort { get; }

    public double SpilloverStoryPoints { get; }

    public ProgressionDelta ProgressionDelta { get; }

    public int BugsCreatedCount { get; }

    public int BugsClosedCount { get; }

    public int MissingEffortCount { get; }

    public int MissingStoryPointCount { get; }

    public int DerivedStoryPointCount { get; }

    public double DerivedStoryPoints { get; }

    public int UnestimatedDeliveryCount { get; }

    public bool IsApproximate { get; }
}
