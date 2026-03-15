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
        if (sprintId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sprintId), "Sprint ID must be greater than zero.");
        }

        if (productId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be greater than zero.");
        }

        ValidateCount(plannedCount, nameof(plannedCount));
        ValidateCount(plannedEffort, nameof(plannedEffort));
        ValidateStoryPoints(plannedStoryPoints, nameof(plannedStoryPoints));
        ValidateCount(workedCount, nameof(workedCount));
        ValidateCount(workedEffort, nameof(workedEffort));
        ValidateCount(bugsPlannedCount, nameof(bugsPlannedCount));
        ValidateCount(bugsWorkedCount, nameof(bugsWorkedCount));
        ValidateCount(completedPbiCount, nameof(completedPbiCount));
        ValidateCount(completedPbiEffort, nameof(completedPbiEffort));
        ValidateStoryPoints(completedPbiStoryPoints, nameof(completedPbiStoryPoints));
        ValidateCount(spilloverCount, nameof(spilloverCount));
        ValidateCount(spilloverEffort, nameof(spilloverEffort));
        ValidateStoryPoints(spilloverStoryPoints, nameof(spilloverStoryPoints));
        ValidateCount(bugsCreatedCount, nameof(bugsCreatedCount));
        ValidateCount(bugsClosedCount, nameof(bugsClosedCount));
        ValidateCount(missingEffortCount, nameof(missingEffortCount));
        ValidateCount(missingStoryPointCount, nameof(missingStoryPointCount));
        ValidateCount(derivedStoryPointCount, nameof(derivedStoryPointCount));
        ValidateStoryPoints(derivedStoryPoints, nameof(derivedStoryPoints));
        ValidateCount(unestimatedDeliveryCount, nameof(unestimatedDeliveryCount));

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

    private static void ValidateCount(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Counts and effort values must be zero or greater.");
        }
    }

    private static void ValidateStoryPoints(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Story-point values must be finite and zero or greater.");
        }
    }
}
