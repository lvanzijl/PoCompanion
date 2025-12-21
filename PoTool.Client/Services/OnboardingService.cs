namespace PoTool.Client.Services;

/// <summary>
/// Service for managing onboarding state using preferences storage.
/// In MAUI Hybrid, this uses MAUI Preferences. For WebAssembly, this would use local storage.
/// </summary>
public class OnboardingService : IOnboardingService
{
    private const string OnboardingCompletedKey = "OnboardingCompleted";
    private const string OnboardingSkippedKey = "OnboardingSkipped";

    private readonly IPreferencesService _preferencesService;

    public OnboardingService(IPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
    }

    /// <summary>
    /// Checks if the user has completed or skipped the onboarding wizard.
    /// </summary>
    public bool HasCompletedOnboarding()
    {
        return _preferencesService.GetBool(OnboardingCompletedKey, false) || 
               _preferencesService.GetBool(OnboardingSkippedKey, false);
    }

    /// <summary>
    /// Marks the onboarding wizard as completed.
    /// </summary>
    public void MarkOnboardingCompleted()
    {
        _preferencesService.SetBool(OnboardingCompletedKey, true);
        _preferencesService.SetBool(OnboardingSkippedKey, false);
    }

    /// <summary>
    /// Marks the onboarding wizard as skipped.
    /// </summary>
    public void MarkOnboardingSkipped()
    {
        _preferencesService.SetBool(OnboardingSkippedKey, true);
        _preferencesService.SetBool(OnboardingCompletedKey, false);
    }

    /// <summary>
    /// Resets the onboarding state, allowing the wizard to be shown again.
    /// </summary>
    public void ResetOnboarding()
    {
        _preferencesService.Remove(OnboardingCompletedKey);
        _preferencesService.Remove(OnboardingSkippedKey);
    }
}
