namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Represents canonical epic progress derived from child feature rollups.
/// </summary>
public sealed record EpicProgress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EpicProgress"/> class.
    /// </summary>
    public EpicProgress(
        int epicId,
        string epicTitle,
        int productId,
        int progressPercent,
        double totalScopeStoryPoints,
        double deliveredStoryPoints,
        int featureCount,
        int doneFeatureCount,
        int donePbiCount,
        bool isDone,
        double sprintDeliveredStoryPoints,
        ProgressionDelta sprintProgressionDelta,
        int sprintEffortDelta,
        int sprintCompletedPbiCount,
        int sprintCompletedFeatureCount)
    {
        if (epicId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(epicId), "Epic ID must be greater than zero.");
        }

        if (productId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(epicTitle))
        {
            throw new ArgumentException("Epic title is required.", nameof(epicTitle));
        }

        if (progressPercent < 0 || progressPercent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(progressPercent), "Progress percent must fall within the range [0, 100].");
        }

        ValidateStoryPoints(totalScopeStoryPoints, nameof(totalScopeStoryPoints));
        ValidateStoryPoints(deliveredStoryPoints, nameof(deliveredStoryPoints));
        ValidateCount(featureCount, nameof(featureCount));
        ValidateCount(doneFeatureCount, nameof(doneFeatureCount));
        ValidateCount(donePbiCount, nameof(donePbiCount));
        ValidateStoryPoints(sprintDeliveredStoryPoints, nameof(sprintDeliveredStoryPoints));
        ValidateCount(sprintCompletedPbiCount, nameof(sprintCompletedPbiCount));
        ValidateCount(sprintCompletedFeatureCount, nameof(sprintCompletedFeatureCount));

        EpicId = epicId;
        EpicTitle = epicTitle;
        ProductId = productId;
        ProgressPercent = progressPercent;
        TotalScopeStoryPoints = totalScopeStoryPoints;
        DeliveredStoryPoints = deliveredStoryPoints;
        FeatureCount = featureCount;
        DoneFeatureCount = doneFeatureCount;
        DonePbiCount = donePbiCount;
        IsDone = isDone;
        SprintDeliveredStoryPoints = sprintDeliveredStoryPoints;
        SprintProgressionDelta = sprintProgressionDelta;
        SprintEffortDelta = sprintEffortDelta;
        SprintCompletedPbiCount = sprintCompletedPbiCount;
        SprintCompletedFeatureCount = sprintCompletedFeatureCount;
    }

    public int EpicId { get; }

    public string EpicTitle { get; }

    public int ProductId { get; }

    public int ProgressPercent { get; }

    public double TotalScopeStoryPoints { get; }

    public double DeliveredStoryPoints { get; }

    public int FeatureCount { get; }

    public int DoneFeatureCount { get; }

    public int DonePbiCount { get; }

    public bool IsDone { get; }

    public double SprintDeliveredStoryPoints { get; }

    public ProgressionDelta SprintProgressionDelta { get; }

    public int SprintEffortDelta { get; }

    public int SprintCompletedPbiCount { get; }

    public int SprintCompletedFeatureCount { get; }

    private static void ValidateCount(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Counts must be zero or greater.");
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
