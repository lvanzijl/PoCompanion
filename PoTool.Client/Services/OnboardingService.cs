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
    public async Task<bool> HasCompletedOnboardingAsync()
    {
        var completed = await _preferencesService.GetBoolAsync(OnboardingCompletedKey, false);
        var skipped = await _preferencesService.GetBoolAsync(OnboardingSkippedKey, false);
        return completed || skipped;
    }

    /// <summary>
    /// Marks the onboarding wizard as completed.
    /// </summary>
    public async Task MarkOnboardingCompletedAsync()
    {
        await _preferencesService.SetBoolAsync(OnboardingCompletedKey, true);
        await _preferencesService.SetBoolAsync(OnboardingSkippedKey, false);
    }

    /// <summary>
    /// Marks the onboarding wizard as skipped.
    /// </summary>
    public async Task MarkOnboardingSkippedAsync()
    {
        await _preferencesService.SetBoolAsync(OnboardingSkippedKey, true);
        await _preferencesService.SetBoolAsync(OnboardingCompletedKey, false);
    }

    /// <summary>
    /// Resets the onboarding state, allowing the wizard to be shown again.
    /// </summary>
    public async Task ResetOnboardingAsync()
    {
        await _preferencesService.RemoveAsync(OnboardingCompletedKey);
        await _preferencesService.RemoveAsync(OnboardingSkippedKey);
    }
}
