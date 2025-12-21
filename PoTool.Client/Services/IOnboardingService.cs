namespace PoTool.Client.Services;

/// <summary>
/// Interface for managing onboarding state.
/// </summary>
public interface IOnboardingService
{
    /// <summary>
    /// Checks if the user has completed or skipped the onboarding wizard.
    /// </summary>
    bool HasCompletedOnboarding();

    /// <summary>
    /// Marks the onboarding wizard as completed.
    /// </summary>
    void MarkOnboardingCompleted();

    /// <summary>
    /// Marks the onboarding wizard as skipped.
    /// </summary>
    void MarkOnboardingSkipped();

    /// <summary>
    /// Resets the onboarding state, allowing the wizard to be shown again.
    /// </summary>
    void ResetOnboarding();
}
