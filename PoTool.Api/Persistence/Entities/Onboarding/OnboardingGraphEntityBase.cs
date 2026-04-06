namespace PoTool.Api.Persistence.Entities.Onboarding;

public abstract class OnboardingGraphEntityBase : OnboardingEntityBase
{
    private const int SoftDeleteReasonMaxLength = 2048;

    public DateTime? DeletedAtUtc { get; set; }

    public string? DeletionReason { get; set; }

    public bool IsDeleted { get; set; }

    public void SoftDelete(DateTime deletedAtUtc, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Deletion reason is required.", nameof(reason));
        }

        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > SoftDeleteReasonMaxLength)
        {
            throw new ArgumentException($"Deletion reason must be {SoftDeleteReasonMaxLength} characters or fewer.", nameof(reason));
        }

        DeletedAtUtc = deletedAtUtc;
        DeletionReason = trimmedReason;
        IsDeleted = true;
        UpdatedAtUtc = deletedAtUtc;
    }
}
