namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Represents canonical feature progress derived from child PBIs.
/// </summary>
public sealed record FeatureProgress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureProgress"/> class.
    /// </summary>
    public FeatureProgress(
        int featureId,
        string featureTitle,
        int productId,
        int? epicId,
        string? epicTitle,
        int progressPercent,
        double totalScopeStoryPoints,
        double deliveredStoryPoints,
        int donePbiCount,
        bool isDone,
        double sprintDeliveredStoryPoints,
        ProgressionDelta sprintProgressionDelta,
        int sprintEffortDelta,
        int sprintCompletedPbiCount,
        bool sprintCompletedInSprint)
    {
        if (featureId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(featureId), "Feature ID must be greater than zero.");
        }

        if (productId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(featureTitle))
        {
            throw new ArgumentException("Feature title is required.", nameof(featureTitle));
        }

        if (epicId.HasValue && epicId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(epicId), "Epic ID must be greater than zero when provided.");
        }

        if (epicId.HasValue != !string.IsNullOrWhiteSpace(epicTitle))
        {
            throw new ArgumentException("Epic ID and epic title must either both be provided or both be omitted.", nameof(epicTitle));
        }

        if (progressPercent < 0 || progressPercent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(progressPercent), "Progress percent must fall within the range [0, 100].");
        }

        ValidateStoryPoints(totalScopeStoryPoints, nameof(totalScopeStoryPoints));
        ValidateStoryPoints(deliveredStoryPoints, nameof(deliveredStoryPoints));
        ValidateCount(donePbiCount, nameof(donePbiCount));
        ValidateStoryPoints(sprintDeliveredStoryPoints, nameof(sprintDeliveredStoryPoints));
        ValidateCount(sprintCompletedPbiCount, nameof(sprintCompletedPbiCount));

        FeatureId = featureId;
        FeatureTitle = featureTitle;
        ProductId = productId;
        EpicId = epicId;
        EpicTitle = epicTitle;
        ProgressPercent = progressPercent;
        TotalScopeStoryPoints = totalScopeStoryPoints;
        DeliveredStoryPoints = deliveredStoryPoints;
        DonePbiCount = donePbiCount;
        IsDone = isDone;
        SprintDeliveredStoryPoints = sprintDeliveredStoryPoints;
        SprintProgressionDelta = sprintProgressionDelta;
        SprintEffortDelta = sprintEffortDelta;
        SprintCompletedPbiCount = sprintCompletedPbiCount;
        SprintCompletedInSprint = sprintCompletedInSprint;
    }

    public int FeatureId { get; }

    public string FeatureTitle { get; }

    public int ProductId { get; }

    public int? EpicId { get; }

    public string? EpicTitle { get; }

    public int ProgressPercent { get; }

    public double TotalScopeStoryPoints { get; }

    public double DeliveredStoryPoints { get; }

    public int DonePbiCount { get; }

    public bool IsDone { get; }

    public double SprintDeliveredStoryPoints { get; }

    public ProgressionDelta SprintProgressionDelta { get; }

    public int SprintEffortDelta { get; }

    public int SprintCompletedPbiCount { get; }

    public bool SprintCompletedInSprint { get; }

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
