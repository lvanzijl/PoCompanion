namespace PoTool.Client.Services;

/// <summary>
/// Interface for managing onboarding state.
/// </summary>
public interface IOnboardingService
{
    /// <summary>
    /// Checks if the user has completed or skipped the onboarding wizard.
    /// </summary>
    Task<bool> HasCompletedOnboardingAsync();

    /// <summary>
    /// Marks the onboarding wizard as completed.
    /// </summary>
    Task MarkOnboardingCompletedAsync();

    /// <summary>
    /// Marks the onboarding wizard as skipped.
    /// </summary>
    Task MarkOnboardingSkippedAsync();

    /// <summary>
    /// Resets the onboarding state, allowing the wizard to be shown again.
    /// </summary>
    Task ResetOnboardingAsync();
}
